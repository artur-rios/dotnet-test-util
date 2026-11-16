using System.Globalization;
using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Functional;
using ArturRios.Util.Test.Logging;
using ArturRios.Util.Test.Scopes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Exercises how a <see cref="WebApiHost{TProgram}"/> builds its host from <see cref="WebApiHostOptions"/> and a
/// derived fixture's <c>ConfigureWebHost</c>, with hosts the tests start and dispose themselves.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
public sealed class WebApiHostTests : IDisposable
{
    private const string Variable = "ASPNETCORE_ENVIRONMENT";

    private readonly string? _originalEnvironment = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _originalEnvironment);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class GreetingHost(string greeting) : WebApiHost<Program>(new WebApiHostOptions
    {
        ConfigureWebHost = builder => builder.UseSetting("Sut:Greeting", "from the options")
    })
    {
        // A primary-constructor parameter is set before the base constructor starts the host.
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("Sut:Greeting", greeting);
    }

    private sealed class NamedHost(string environmentName) : WebApiHost<Program>(environmentName);

    private sealed class CountingHost() : WebApiHost<Program>(EnvironmentType.Local)
    {
        public List<bool> DisposeCalls { get; } = [];

        protected override void Dispose(bool disposing)
        {
            DisposeCalls.Add(disposing);

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task GivenNoOptions_WhenTheHostStarts_ThenItRunsAsLocalAndSetsTheVariable()
    {
        using var host = new WebApiHost<Program>();

        var response = await host.Gateway.GetAsync<string>("/environment", Token);

        Assert.Equal("local", host.EnvironmentName);
        Assert.Equal("local", response.Body);
        Assert.Equal("local", Environment.GetEnvironmentVariable(Variable));
    }

    [Fact]
    public async Task GivenAnEnvironmentType_WhenTheHostStarts_ThenItRunsAsItsLowercaseName()
    {
        using var host = WebApiHost<Program>.Start(new WebApiHostOptions(EnvironmentType.Staging));

        var response = await host.Gateway.GetAsync<string>("/environment", Token);

        Assert.Equal("staging", response.Body);
    }

    [Fact]
    public async Task GivenAnEnvironmentName_WhenTheHostStarts_ThenItRunsAsThatNameAsGiven()
    {
        using var host = new NamedHost("Testing");

        var response = await host.Gateway.GetAsync<string>("/environment", Token);

        Assert.Equal("Testing", response.Body);
        Assert.Equal("Testing", Environment.GetEnvironmentVariable(Variable));
    }

    [Fact]
    public async Task GivenTheVariableIsNotToBeSet_WhenTheHostStarts_ThenItRunsAsItsEnvironmentAndLeavesTheVariable()
    {
        Environment.SetEnvironmentVariable(Variable, "untouched");

        using var host = WebApiHost<Program>.Start(
            new WebApiHostOptions(EnvironmentType.Production) { SetEnvironmentVariable = false });

        var response = await host.Gateway.GetAsync<string>("/environment", Token);

        Assert.Equal("production", response.Body);
        Assert.Equal("untouched", Environment.GetEnvironmentVariable(Variable));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenNoEnvironmentName_WhenTheHostIsCreated_ThenItIsRefused(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NamedHost(name!));
    }

    [Fact]
    public void GivenNoOptions_WhenTheHostIsCreated_ThenItIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => WebApiHost<Program>.Start(null!));
    }

    [Fact]
    public async Task GivenConfigurationValues_WhenTheApiReadsThem_ThenTheyOverrideAndHideItsOwn()
    {
        using var environment = new EnvironmentVariableScope(("Sut__Hidden", "from the environment"),
            ("Sut__Overridden", "from the environment"));
        var options = new WebApiHostOptions
        {
            Configuration = { ["Sut:Hidden"] = null, ["Sut:Overridden"] = "from the test", ["Sut:Greeting"] = "hello" }
        };

        using var host = WebApiHost<Program>.Start(options);

        var hidden = await host.Gateway.GetAsync<string>("/configuration/Sut:Hidden", Token);
        var overridden = await host.Gateway.GetAsync<string>("/configuration/Sut:Overridden", Token);
        var atStartup = await host.Gateway.GetAsync<string>("/configuration/startup", Token);

        Assert.Equal(string.Empty, hidden.Body);
        Assert.Equal("from the test", overridden.Body);
        Assert.Equal("hello", atStartup.Body);
    }

    [Fact]
    public async Task GivenServicesToReplace_WhenTheApiResolvesThem_ThenItGetsTheTestsOnes()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var options = new WebApiHostOptions { ConfigureServices = services => services.AddSingleton<TimeProvider>(clock) };

        using var host = WebApiHost<Program>.Start(options);

        var response = await host.Gateway.GetAsync<string>("/time", Token);

        Assert.Equal("2030-01-02T03:04:05.0000000+00:00", response.Body);
    }

    [Fact]
    public async Task GivenAFakeClock_WhenTheTestMovesIt_ThenTheApiSeesTheTimeItSet()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        using var host = WebApiHost<Program>.Start(new WebApiHostOptions { TimeProvider = clock });

        clock.Advance(TimeSpan.FromHours(1));
        var response = await host.Gateway.GetAsync<string>("/time", Token);

        Assert.Same(clock, host.Services.GetRequiredService<TimeProvider>());
        Assert.Equal("2030-01-02T04:04:05.0000000+00:00", response.Body);
    }

    [Fact]
    public async Task GivenARecordingLoggerProvider_WhenTheApiLogs_ThenEveryEntryIsKeptWithItsEventAndScope()
    {
        var logs = new RecordingLoggerProvider();
        using var host = WebApiHost<Program>.Start(new WebApiHostOptions { LoggerProviders = { logs } });

        await host.Gateway.PostAsync<string>("/log", new { }, Token);

        var entries = logs.EntriesOf<Program>();
        var warning = LogAssert.Single(entries, LogLevel.Warning, "Warned 3 times");
        LogAssert.Single(entries, LogLevel.Debug, "Debug entry"); // below the API's default Information level
        Assert.Equal(7, warning.EventId.Id);
        Assert.Equal(3, warning.GetProperty("Count"));
        Assert.Contains(warning.Scopes, scope => scope?.ToString() == "Request log");
        Assert.Contains(logs.Entries, entry => entry.Category != nameof(Program));
    }

    [Fact]
    public async Task GivenALocalPort_WhenARequestArrives_ThenItAppearsToArriveOnThatPort()
    {
        using var host = WebApiHost<Program>.Start(new WebApiHostOptions { LocalPort = 9464 });

        var response = await host.Gateway.GetAsync<int>("/port", Token);

        Assert.Equal(9464, response.Body);
    }

    [Fact]
    public void GivenClientOptions_WhenTheHostCreatesClients_ThenTheyUseThem()
    {
        var baseAddress = new Uri("http://api.test/");
        using var host = WebApiHost<Program>.Start(
            new WebApiHostOptions { ClientOptions = new WebApplicationFactoryClientOptions { BaseAddress = baseAddress } });

        using var created = host.CreateClient();
        using var other = host.CreateClient(new WebApplicationFactoryClientOptions());

        Assert.Equal(baseAddress, host.Client.BaseAddress);
        Assert.Equal(baseAddress, created.BaseAddress);
        Assert.Equal(new Uri("http://localhost"), other.BaseAddress);
    }

    [Fact]
    public async Task GivenAStartedHost_WhenItsServerServicesAndFactoryAreUsed_ThenTheyAreTheHostsOwn()
    {
        using var host = new WebApiHost<Program>();

        using var raw = host.Server.CreateClient();
        var response = await raw.GetStringAsync("/environment", Token);

        Assert.Equal("\"local\"", response);
        Assert.Same(host.Server, host.Factory.Server);
        Assert.Same(host.Services, host.Factory.Services);
        Assert.Same(TimeProvider.System, host.Services.GetService<TimeProvider>());
    }

    [Fact]
    public async Task GivenADerivedHost_WhenItStarts_ThenItsOverrideRunsAfterTheOptions()
    {
        using var host = new GreetingHost("from the override");

        var response = await host.Gateway.GetAsync<string>("/configuration/startup", Token);

        Assert.Equal("from the override", response.Body);
    }

    [Fact]
    public void GivenAHostThatFailsToStart_WhenCreated_ThenTheFailureReachesTheCaller()
    {
        var options = new WebApiHostOptions { ConfigureWebHost = _ => throw new InvalidOperationException("no host") };

        var exception = Assert.Throws<InvalidOperationException>(() => WebApiHost<Program>.Start(options));

        Assert.Equal("no host", exception.Message);
    }

    [Fact]
    public async Task GivenACultureScope_WhenTheHostStarts_ThenTheApiStartsInItsCultures()
    {
        using var culture = new CultureScope("fr-FR", "ja-JP");

        using var host = new WebApiHost<Program>();

        var response = await host.Gateway.GetAsync<string>("/culture", Token);
        Assert.Equal("fr-FR|ja-JP", response.Body);
        Assert.Equal("fr-FR", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public async Task GivenTheCallerSuppressedExecutionContextFlow_WhenTheHostStarts_ThenItStarts()
    {
        WebApiHost<Program> host;

        // No await inside: the flow is restored on the thread that suppressed it.
        using (ExecutionContext.SuppressFlow())
        {
            host = new WebApiHost<Program>();
        }

        using (host)
        {
            var response = await host.Client.GetStringAsync("/environment", Token);

            Assert.Equal("\"local\"", response);
        }
    }

    [Fact]
    public void GivenADerivedHost_WhenDisposedTwice_ThenItsOverrideRunsTwiceAndNothingThrows()
    {
        var host = new CountingHost();

        host.Dispose();
        var exception = Record.Exception(host.Dispose);

        Assert.Null(exception);
        Assert.Equal([true, true], host.DisposeCalls);
    }

    [Fact]
    public async Task GivenAHost_WhenAVariantIsStarted_ThenItRunsTheHostsOptionsAndThenItsOwnConfiguration()
    {
        using var host = WebApiHost<Program>.Start(new WebApiHostOptions
        {
            EnvironmentName = "Testing",
            LocalPort = 8080,
            ConfigureWebHost = builder => builder.UseSetting("Sut:Greeting", "from the host")
        });

        using var variant = host.StartVariant(builder => builder.UseSetting("Sut:Greeting", "from the variant"));

        var environment = await variant.Gateway.GetAsync<string>("/environment", Token);
        var port = await variant.Gateway.GetAsync<int>("/port", Token);
        var greeting = await variant.Gateway.GetAsync<string>("/configuration/startup", Token);
        var hostGreeting = await host.Gateway.GetAsync<string>("/configuration/startup", Token);

        Assert.Equal("Testing", environment.Body);
        Assert.Equal(8080, port.Body);
        Assert.Equal("from the variant", greeting.Body);
        Assert.Equal("from the host", hostGreeting.Body);
        Assert.NotSame(host.Services, variant.Services);
    }

    [Fact]
    public async Task GivenADerivedHost_WhenAVariantIsStarted_ThenItRunsTheHostsOverride()
    {
        using var host = new GreetingHost("from the override");

        using var variant = host.StartVariant(_ => { });

        var response = await variant.Gateway.GetAsync<string>("/configuration/startup", Token);

        Assert.Equal("from the override", response.Body);
    }

    [Fact]
    public async Task GivenAVariant_WhenTheHostItWasStartedFromIsDisposed_ThenTheVariantStillAnswers()
    {
        var host = new WebApiHost<Program>();
        using var variant = host.StartVariant(_ => { });

        host.Dispose();
        var response = await variant.Client.GetStringAsync("/environment", Token);

        Assert.Equal("\"local\"", response);
    }

    [Fact]
    public void GivenADisposedHostOrNoConfiguration_WhenAVariantIsStarted_ThenItIsRefused()
    {
        var host = new WebApiHost<Program>();

        Assert.Throws<ArgumentNullException>(() => host.StartVariant(null!));
        host.Dispose();
        Assert.Throws<ObjectDisposedException>(() => host.StartVariant(_ => { }));
    }

    [Fact]
    public async Task GivenASharedHost_WhenTheRateLimitBudgetIsSpent_ThenResettingTheClientDoesNotRestoreIt()
    {
        // Why a shared host is unsafe for tests that depend on what the API keeps in memory.
        using var host = new WebApiHost<Program>();

        var first = await host.Client.GetAsync("/limited", Token);
        host.Reset();
        var second = await host.Client.GetAsync("/limited", Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }
}

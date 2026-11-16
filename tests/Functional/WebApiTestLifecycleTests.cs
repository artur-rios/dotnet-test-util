using ArturRios.Configuration.Enums;
using ArturRios.Util.Http;
using ArturRios.Util.Test.Functional;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Exercises how <see cref="WebApiTest{T}"/> starts and releases its host, with hosts the tests create and dispose
/// themselves rather than the one xUnit gives a derived test class.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
public sealed class WebApiTestLifecycleTests : IDisposable
{
    private const string Variable = "ASPNETCORE_ENVIRONMENT";

    private readonly string? _originalEnvironment = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _originalEnvironment);

    /// <summary>A host as a test class deriving from <see cref="WebApiTest{T}"/> sees it.</summary>
    private sealed class SutHost(EnvironmentType environment) : WebApiTest<Program>(environment)
    {
        public HttpGateway Http => Gateway;

        public List<bool> DisposeCalls { get; } = [];

        protected override void Dispose(bool disposing)
        {
            DisposeCalls.Add(disposing);

            base.Dispose(disposing);
        }
    }

    /// <summary>A test class deriving from <see cref="WebApiTest{T}"/> with the newer constructors and members.</summary>
    private sealed class ConfiguredHost : WebApiTest<Program>
    {
        public ConfiguredHost(string environmentName) : base(environmentName)
        {
        }

        public ConfiguredHost(WebApiHostOptions options) : base(options)
        {
        }

        public string Greeting { get; init; } = "from the override";

        public HttpGateway Http => Gateway;

        public WebApplicationFactory<Program> HostFactory => Factory;

        public Microsoft.AspNetCore.TestHost.TestServer HostServer => Server;

        public IServiceProvider HostServices => Services;

        public HttpClient NewClient() => CreateClient();

        public HttpClient NewClient(WebApplicationFactoryClientOptions options) => CreateClient(options);

        public WebApiHost<Program> NewVariant(Action<IWebHostBuilder> configure) => StartVariant(configure);

        // A property initializer runs before the base constructor starts the host.
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("Sut:Greeting", Greeting);
    }

    [Fact]
    public async Task GivenAnEnvironmentName_WhenTheHostStarts_ThenTheVariableAndTheHostUseItAsGiven()
    {
        using var host = new ConfiguredHost("Testing");

        var response = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal("Testing", Environment.GetEnvironmentVariable(Variable));
        Assert.Equal("Testing", response.Body);
    }

    [Fact]
    public async Task GivenAStartedHost_WhenADerivedTestStartsAVariant_ThenItRunsTheTestsConfigurationThenItsOwn()
    {
        using var host = new ConfiguredHost("Testing");

        using var variant = host.NewVariant(_ => { });
        using var overridden = host.NewVariant(builder => builder.UseSetting("Sut:Greeting", "from the variant"));

        var environment = await variant.Gateway.GetAsync<string>("/environment", TestContext.Current.CancellationToken);
        var greeting = await variant.Gateway.GetAsync<string>("/configuration/startup",
            TestContext.Current.CancellationToken);
        var overriddenGreeting = await overridden.Gateway.GetAsync<string>("/configuration/startup",
            TestContext.Current.CancellationToken);

        Assert.Equal("Testing", environment.Body);
        Assert.Equal("from the override", greeting.Body);
        Assert.Equal("from the variant", overriddenGreeting.Body);
    }

    [Fact]
    public async Task GivenOptionsAndAnOverride_WhenTheHostStarts_ThenBothConfigureIt()
    {
        var options = new WebApiHostOptions(EnvironmentType.Development) { LocalPort = 8080 };

        using var host = new ConfiguredHost(options);

        var environment = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);
        var port = await host.Http.GetAsync<int>("/port", TestContext.Current.CancellationToken);
        var greeting = await host.Http.GetAsync<string>("/configuration/startup", TestContext.Current.CancellationToken);

        Assert.Equal("development", environment.Body);
        Assert.Equal(8080, port.Body);
        Assert.Equal("from the override", greeting.Body);
    }

    [Fact]
    public async Task GivenAStartedHost_WhenADerivedTestReachesTheFactoryServerServicesAndRawClients_ThenTheyAreTheHostsOwn()
    {
        using var host = new ConfiguredHost("local");

        using var client = host.NewClient();
        using var other = host.NewClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://api.test/") });
        var response = await client.GetStringAsync("/environment", TestContext.Current.CancellationToken);

        Assert.Equal("\"local\"", response);
        Assert.Equal(new Uri("http://api.test/"), other.BaseAddress);
        Assert.Same(host.HostServer, host.HostFactory.Server);
        Assert.Same(host.HostServices, host.HostFactory.Services);
        Assert.Same(TimeProvider.System, host.HostServices.GetService<TimeProvider>());
    }

    [Theory]
    [InlineData(EnvironmentType.Local, "local")]
    [InlineData(EnvironmentType.Development, "development")]
    [InlineData(EnvironmentType.Staging, "staging")]
    [InlineData(EnvironmentType.Production, "production")]
    public async Task GivenAnEnvironment_WhenTheHostStarts_ThenTheVariableAndTheHostUseItsLowercaseName(
        EnvironmentType environment, string expected)
    {
        using var host = new SutHost(environment);

        var response = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal(expected, Environment.GetEnvironmentVariable(Variable));
        Assert.Equal(expected, response.Body);
    }

    [Fact]
    public async Task GivenTheVariableChangesAfterTheHostStarted_WhenTheHostIsAsked_ThenItStillRunsAsItsOwnEnvironment()
    {
        using var host = new SutHost(EnvironmentType.Staging);

        Environment.SetEnvironmentVariable(Variable, "production");

        var response = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal("staging", response.Body);
    }

    [Fact]
    public void GivenADerivedHost_WhenDisposed_ThenItsDisposeOverrideRunsOnceWithDisposingTrue()
    {
        var host = new SutHost(EnvironmentType.Local);

        host.Dispose();

        Assert.Equal([true], host.DisposeCalls);
    }

    [Fact]
    public async Task GivenADisposedHost_WhenARequestIsSent_ThenItIsRefused()
    {
        var host = new SutHost(EnvironmentType.Local);

        host.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenADisposedHost_WhenDisposedAgain_ThenNothingThrows()
    {
        var host = new SutHost(EnvironmentType.Local);
        host.Dispose();

        var exception = Record.Exception(host.Dispose);

        Assert.Null(exception);
    }
}

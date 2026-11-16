using System.Runtime.CompilerServices;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Functional;
using Microsoft.AspNetCore.Hosting;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Shows that a disposed host can be collected although the API starts timers it never stops: the SUT's
/// <c>Heartbeat</c>, and ASP.NET Core's rate limiter, which every SUT host runs. Each kept every host alive for the
/// rest of the run. A timer captures the execution context it is started in: the test's, which reaches the test and
/// so the host it holds, and the one WebApplicationFactory runs the entry point in, which holds the host. The rate
/// limiter's timer also holds its middleware, and through it the whole application. A test sending a request
/// through <c>GET /limited</c> puts both limiters to work. Variants of a host, made with
/// <c>Factory.WithWebHostBuilder(...)</c> or <c>StartVariant</c>, start as their host does and are released as it
/// is. A host whose API runs a global limiter with a partitioner that holds its services (<c>Sut:GlobalLimiter</c>)
/// is collected only when it is told to dispose that limiter.
/// The tests stand an ambient value of their own for xUnit's test context: it is set while the host starts and holds
/// the host, as xUnit's context reaches the test that holds it.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
public sealed class WebApiHostLeakTests : IDisposable
{
    private const string Variable = "ASPNETCORE_ENVIRONMENT";

    private static readonly AsyncLocal<Holder?> Ambient = new();

    private readonly string? _originalEnvironment = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _originalEnvironment);

    private sealed class Holder
    {
        public object? Host { get; set; }
    }

    private sealed class SutTest() : WebApiTest<Program>(EnvironmentType.Local)
    {
        public IServiceProvider HostServices => Services;

        public HttpClient NewClient() => CreateClient();
    }

    private sealed class SutTestWithOptions(WebApiHostOptions options) : WebApiTest<Program>(options)
    {
        public IServiceProvider HostServices => Services;

        public HttpClient NewClient() => CreateClient();
    }

    [Fact]
    public async Task GivenADisposedSharedHost_WhenCollected_ThenNothingTheHostStartedKeepsItAlive()
    {
        var (host, services) = StartUseAndDispose(() =>
        {
            var started = new WebApiHost<Program>();

            return (started, started.Services, started.Client);
        });

        Assert.True(await IsCollectedAsync(services), "The disposed host's services are still alive");
        Assert.True(await IsCollectedAsync(host), "The disposed host is still alive");
    }

    [Fact]
    public async Task GivenADisposedWebApiTest_WhenCollected_ThenNothingTheHostStartedKeepsItAlive()
    {
        var (test, services) = StartUseAndDispose(() =>
        {
            var started = new SutTest();

            return (started, started.HostServices, started.NewClient());
        });

        Assert.True(await IsCollectedAsync(services), "The disposed host's services are still alive");
        Assert.True(await IsCollectedAsync(test), "The disposed test is still alive");
    }

    [Fact]
    public async Task GivenAVariantHostOfAHost_WhenTheHostIsDisposed_ThenTheVariantCanBeCollectedToo()
    {
        // A variant starts when the test first uses it, in the test's context, so nothing of the test may hold it.
        var (host, variant) = StartUseAndDispose(() =>
        {
            var started = new WebApiHost<Program>();
            var other = started.Factory.WithWebHostBuilder(builder => builder.UseSetting("Sut:Greeting", "variant"));

            return (started, other.Services, other.CreateClient());
        }, holdHost: false);

        Assert.True(await IsCollectedAsync(variant), "The disposed variant's services are still alive");
        Assert.True(await IsCollectedAsync(host), "The disposed host is still alive");
    }

    [Fact]
    public async Task GivenAVariantStartedWhileTheTestHoldsTheHost_WhenTheHostIsDisposed_ThenTheVariantCanBeCollected()
    {
        // The variant starts when the test first uses it, while the ambient value holds the host, so the timers it
        // starts would keep the host, and the variant it disposes, alive if they captured the test's context.
        var (host, variant) = StartUseAndDispose(() =>
        {
            var started = new WebApiHost<Program>();
            var other = started.Factory.WithWebHostBuilder(builder => builder.UseSetting("Sut:Greeting", "variant"));

            return (started, other.Services, other.CreateClient());
        });

        Assert.True(await IsCollectedAsync(variant), "The disposed variant's services are still alive");
        Assert.True(await IsCollectedAsync(host), "The disposed host is still alive");
    }

    [Fact]
    public async Task GivenAVariantStartedFromALiveHost_WhenTheVariantIsDisposed_ThenItCanBeCollected()
    {
        // A variant the test owns, disposed while the host it was made from, a shared fixture say, lives on.
        using var host = new WebApiHost<Program>();

        var (variant, services) = StartUseAndDispose(() =>
        {
            var started = host.StartVariant(builder => builder.UseSetting("Sut:Greeting", "variant"));

            return (started, started.Services, started.Client);
        });

        Assert.True(await IsCollectedAsync(services), "The disposed variant's services are still alive");
        Assert.True(await IsCollectedAsync(variant), "The disposed variant is still alive");
    }

    [Fact]
    public async Task GivenAVariantStartedInTheCallersContext_WhenTheHostIsDisposed_ThenTheTimerItStartedKeepsItAlive()
    {
        // The control of the variant tests: with the isolation turned off, the variant starts in the test's context,
        // as every variant made with Factory.WithWebHostBuilder(...) used to, and stays alive for the rest of the run.
        var (_, variant) = StartUseAndDispose(() =>
        {
            var started = WebApiHost<Program>.Start(new WebApiHostOptions { IsolateExecutionContext = false });
            var other = started.Factory.WithWebHostBuilder(builder => builder.UseSetting("Sut:Greeting", "variant"));

            return (started, other.Services, other.CreateClient());
        });

        Assert.False(await IsCollectedAsync(variant, attempts: 3), "The variant was collected after all");
    }

    [Fact]
    public async Task GivenAGlobalLimiterThatHoldsTheServices_WhenTheHostDisposesIt_ThenTheServicesCanBeCollected()
    {
        var (host, services) = StartUseAndDispose(() =>
        {
            var started = WebApiHost<Program>.Start(GlobalLimiterOptions(disposeGlobalRateLimiter: true));

            return (started, started.Services, started.Client);
        });

        Assert.True(await IsCollectedAsync(services), "The disposed host's services are still alive");
        Assert.True(await IsCollectedAsync(host), "The disposed host is still alive");
    }

    [Fact]
    public async Task GivenAGlobalLimiterThatHoldsTheServices_WhenTheHostLeavesIt_ThenItKeepsTheServicesAlive()
    {
        // The control of the test above, and the reason the option is off by default: the global limiter runs a timer
        // until it is disposed, which holds it and its partitioner, and the partitioner holds the services. The host
        // cannot tell a limiter the API builds for it from one the API shares between hosts, so it leaves it.
        var (_, services) = StartUseAndDispose(() =>
        {
            var started = WebApiHost<Program>.Start(GlobalLimiterOptions(disposeGlobalRateLimiter: false));

            return (started, started.Services, started.Client);
        });

        Assert.False(await IsCollectedAsync(services, attempts: 3), "The host's services were collected after all");
    }

    [Fact]
    public async Task GivenTheHostStartsInTheCallersContext_WhenDisposed_ThenTheTimerItStartedKeepsItAlive()
    {
        // The control: with the isolation turned off, the timer's captured context holds the host, so the tests
        // above would fail without it. This host stays alive for the rest of the run, which is the price.
        var (_, services) = StartUseAndDispose(() =>
        {
            var started = new SutTestWithOptions(new WebApiHostOptions { IsolateExecutionContext = false });

            return (started, started.HostServices, started.NewClient());
        });

        Assert.False(await IsCollectedAsync(services, attempts: 3), "The host was collected after all");
    }

    // A host whose API runs a global limiter with a partitioner that holds its services (sut/Program.cs).
    private static WebApiHostOptions GlobalLimiterOptions(bool disposeGlobalRateLimiter) => new()
    {
        Configuration = { ["Sut:GlobalLimiter"] = "true" },
        DisposeGlobalRateLimiter = disposeGlobalRateLimiter
    };

    // Starts a host while the ambient value holds it, sends it a request, disposes it and clears the ambient value,
    // returning weak references only: once this returns, nothing but what the host itself started can keep it alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Host, WeakReference Services) StartUseAndDispose(
        Func<(IDisposable Host, IServiceProvider Services, HttpClient Client)> start, bool holdHost = true)
    {
        var holder = new Holder();
        Ambient.Value = holdHost ? holder : null;

        try
        {
            var (host, services, client) = start();
            holder.Host = host;

            using (host)
            {
                // A request through the rate limiter, so its partition and its timer are in use too.
                client.GetAsync("/limited", TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            }

            return (new WeakReference(host), new WeakReference(services));
        }
        finally
        {
            Ambient.Value = null;
        }
    }

    private static async Task<bool> IsCollectedAsync(WeakReference reference, int attempts = 10)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!reference.IsAlive)
            {
                return true;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return false;
    }
}

using System.Runtime.CompilerServices;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Functional;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Shows that a disposed host can be collected although the API starts timers it never stops: the SUT's
/// <c>Heartbeat</c>, and ASP.NET Core's rate limiter, which every SUT host runs. Each kept every host alive for the
/// rest of the run. A timer captures the execution context it is started in: the test's, which reaches the test and
/// so the host it holds, and the one WebApplicationFactory runs the entry point in, which holds the host. The rate
/// limiter's timer also holds its middleware, and through it the whole application. A test sending a request
/// through <c>GET /limited</c> puts both limiters to work.
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

    // Starts a host while the ambient value holds it, sends it a request, disposes it and clears the ambient value,
    // returning weak references only: once this returns, nothing but what the host itself started can keep it alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Host, WeakReference Services) StartUseAndDispose(
        Func<(IDisposable Host, IServiceProvider Services, HttpClient Client)> start)
    {
        var holder = new Holder();
        Ambient.Value = holder;

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

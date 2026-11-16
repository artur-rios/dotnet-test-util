using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.Test.Logging;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Functional;

/// <summary>
/// An in-memory host of the web API under test, built with <see cref="WebApplicationFactory{TEntryPoint}"/>, with an
/// <see cref="HttpGateway"/> and helpers for authenticating and authorizing requests. Use it as an xUnit class or
/// collection fixture (<c>IClassFixture&lt;WebApiHost&lt;Program&gt;&gt;</c>) to share one host between the tests
/// of a class or collection instead of starting one per test, as <see cref="WebApiTest{T}"/> does; derive from
/// <see cref="SharedWebApiTest{TProgram}"/> to get the members <see cref="WebApiTest{T}"/> gives a test class.
/// </summary>
/// <typeparam name="TProgram">The entry point type of the web API under test (typically its <c>Program</c> class).</typeparam>
/// <remarks>
/// <para>
/// The host starts in the constructor. Unless <see cref="WebApiHostOptions.SetEnvironmentVariable"/> is turned off,
/// starting it also sets the process-wide <c>ASPNETCORE_ENVIRONMENT</c> variable, so hosts of different environments
/// cannot start in parallel; put the test classes in one xUnit collection when that comes up.
/// </para>
/// <para>
/// A shared host is only safe for tests that share nothing through it. Everything the API keeps in memory lives as
/// long as the host and is seen by every test that uses it: rate-limiter budgets (an anonymous-authentication
/// limiter that allows ten requests a minute is spent by the tenth test), caches, in-memory stores and other
/// stateful singletons, a fake clock one test moved, the entries a <see cref="RecordingLoggerProvider"/> kept.
/// <see cref="Reset"/> gives each test a fresh client, but none of that state; a test that depends on it, or changes
/// it, needs a host of its own.
/// </para>
/// <para>
/// To configure a fixture, derive from this class, passing <see cref="WebApiHostOptions"/> to the base constructor or
/// overriding <see cref="ConfigureWebHost"/>: <c>public sealed class ApiHost() : WebApiHost&lt;Program&gt;(options)</c>.
/// A test that needs a host of its own starts one with <see cref="Start"/>, and one that needs a host configured a
/// little differently from a shared one starts a variant of it with <see cref="StartVariant"/>.
/// </para>
/// </remarks>
public class WebApiHost<TProgram> : IDisposable where TProgram : class
{
    private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";

    private readonly WebApplicationFactoryClientOptions _clientOptions;
    private readonly Action<IWebHostBuilder>? _configureDerived;
    private readonly HostFactory _factory;
    private readonly List<object> _entryPointListeners = [];
    private readonly WebApiHostOptions _options;
    private bool _disposed;
    private readonly Lock _gate = new();
    private readonly List<RequestDelegate> _pipelines = [];

    /// <summary>
    /// Starts a host that runs as <see cref="EnvironmentType.Local"/>. This is the constructor xUnit uses for
    /// <c>IClassFixture&lt;WebApiHost&lt;Program&gt;&gt;</c>, which needs a fixture to have a single public one.
    /// </summary>
    public WebApiHost() : this(new WebApiHostOptions())
    {
    }

    /// <summary>Starts a host that runs as <paramref name="environment"/>, for a derived fixture.</summary>
    /// <param name="environment">The environment; the host runs as its lowercase name.</param>
    protected WebApiHost(EnvironmentType environment) : this(new WebApiHostOptions(environment))
    {
    }

    /// <summary>Starts a host that runs as the environment named <paramref name="environmentName"/>, for a derived fixture.</summary>
    /// <param name="environmentName">The environment name, used as given (for example <c>"Testing"</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is <c>null</c>, empty or white space.</exception>
    protected WebApiHost(string environmentName) : this(new WebApiHostOptions { EnvironmentName = environmentName })
    {
    }

    /// <summary>Starts a host built as <paramref name="options"/> say, for a derived fixture.</summary>
    /// <param name="options">How to build the host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The options name no environment.</exception>
    protected WebApiHost(WebApiHostOptions options) : this(options, null)
    {
    }

    internal WebApiHost(WebApiHostOptions options, Action<IWebHostBuilder>? configureDerived)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.EnvironmentName, nameof(options));

        _options = options;
        _configureDerived = configureDerived;
        _clientOptions = options.ClientOptions;
        EnvironmentName = options.EnvironmentName;

        if (options.SetEnvironmentVariable)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, EnvironmentName);
        }

        _factory = new HostFactory(Configure, options.IsolateExecutionContext);

        try
        {
            if (options.IsolateExecutionContext)
            {
                RunIsolated(() => _ = _factory.Server);
            }
            else
            {
                _ = _factory.Server;
            }

            Gateway = new HttpGateway(CreateClient());
        }
        catch
        {
            ReleaseHost();
            throw;
        }
    }

    /// <summary>
    /// Starts a host built as <paramref name="options"/> say, for a test that starts and disposes a host of its own
    /// rather than sharing a fixture.
    /// </summary>
    /// <param name="options">How to build the host.</param>
    /// <returns>The started host; dispose it when the test is done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The options name no environment.</exception>
    public static WebApiHost<TProgram> Start(WebApiHostOptions options) => new(options, null);

    /// <summary>
    /// Starts a variant of this host: a host of its own, built as this one is (its <see cref="WebApiHostOptions"/>,
    /// then its <see cref="ConfigureWebHost"/>), then as <paramref name="configure"/> says. Use it rather than
    /// <c>Factory.WithWebHostBuilder(...)</c> for a test that needs a host configured a little differently from a
    /// shared one.
    /// </summary>
    /// <param name="configure">What the variant configures after this host's configuration.</param>
    /// <returns>The started variant. The caller owns it: dispose it when the test is done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">This host has been disposed.</exception>
    /// <remarks>
    /// The variant starts as this host does (in an execution context of its own, unless
    /// <see cref="WebApiHostOptions.IsolateExecutionContext"/> is off), and releases what it holds when it is
    /// disposed, whether this host still runs or not; disposing this host leaves it running. A variant made with
    /// <c>Factory.WithWebHostBuilder(...)</c> belongs to the factory instead: disposing it releases nothing until this
    /// host is disposed, so a long-lived shared host keeps every such variant alive.
    /// </remarks>
    public WebApiHost<TProgram> StartVariant(Action<IWebHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new WebApiHost<TProgram>(_options, builder =>
        {
            ConfigureWebHost(builder);
            _configureDerived?.Invoke(builder);
            configure(builder);
        });
    }

    /// <summary>The environment name the host runs as.</summary>
    public string EnvironmentName { get; }

    /// <summary>
    /// The factory that built the host, for what the other members do not reach (for example
    /// <c>Factory.WithWebHostBuilder(...)</c> for a variant host, disposed with this one; <see cref="StartVariant"/>
    /// starts a variant the caller owns).
    /// </summary>
    public WebApplicationFactory<TProgram> Factory => _factory;

    /// <summary>The in-memory test server the host runs on.</summary>
    public TestServer Server => _factory.Server;

    /// <summary>The host's root service provider, to resolve the API's services (create a scope for scoped ones).</summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// The gateway used to issue HTTP requests against the host. <see cref="Reset"/> replaces it, so read it in each
    /// test rather than keeping it across tests.
    /// </summary>
    public HttpGateway Gateway { get; private set; }

    /// <summary>The raw <see cref="HttpClient"/> behind <see cref="Gateway"/>, for requests the gateway does not model.</summary>
    public HttpClient Client => Gateway.Client;

    /// <summary>Creates a new client of the host with <see cref="WebApiHostOptions.ClientOptions"/>.</summary>
    /// <returns>A client with no default headers; the host disposes it with itself.</returns>
    public HttpClient CreateClient() => _factory.CreateClient(_clientOptions);

    /// <summary>Creates a new client of the host with <paramref name="options"/>.</summary>
    /// <param name="options">The client's options: base address, redirects, cookies.</param>
    /// <returns>A client with no default headers; the host disposes it with itself.</returns>
    public HttpClient CreateClient(WebApplicationFactoryClientOptions options) => _factory.CreateClient(options);

    /// <summary>Authenticates against <paramref name="authRoute"/> and returns the resulting authentication payload.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The authentication payload returned by the API.</returns>
    /// <exception cref="TestException">Thrown when authentication fails or returns no usable token.</exception>
    public async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute,
        CancellationToken cancellationToken = default)
    {
        var response = await Gateway.PostAsync<DataOutput<Authentication>>(authRoute, credentials, cancellationToken);

        var body = response.Body;

        var authError = !response.IsSuccess
                        || body is null
                        || !body.Success
                        || body.Data is null
                        || string.IsNullOrEmpty(body.Data.Token);

        return authError ? throw new TestException("Could not authenticate") : body!.Data!;
    }

    /// <summary>Sets the bearer token on the gateway's default request headers, replacing any previous one.</summary>
    /// <param name="authToken">The JWT to send as a Bearer token.</param>
    public void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

    /// <summary>Authenticates and applies the resulting token to the gateway's default headers.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="TestException">Thrown when authentication fails; no token is applied then.</exception>
    public async Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute,
        CancellationToken cancellationToken = default)
    {
        var authentication = await AuthenticateAsync(credentials, authRoute, cancellationToken);

        Authorize(authentication.Token!);
    }

    /// <summary>
    /// Replaces <see cref="Gateway"/> and <see cref="Client"/> with a fresh client, so a test starts with no default
    /// headers, no authorization and no cookies a previous test left. <see cref="SharedWebApiTest{TProgram}"/> calls
    /// it before each test. It resets nothing the API keeps (see the remarks on <see cref="WebApiHost{TProgram}"/>).
    /// </summary>
    public void Reset()
    {
        var previous = Gateway.Client;

        Gateway = new HttpGateway(CreateClient());

        previous.Dispose();
    }

    /// <summary>
    /// Configures the web host builder, after <see cref="WebApiHostOptions"/> have been applied. Override it in a
    /// derived fixture to replace services (<c>builder.ConfigureTestServices(...)</c>), add configuration and the like.
    /// </summary>
    /// <param name="builder">The host's web host builder.</param>
    /// <remarks>
    /// It runs while the base constructor starts the host, before the derived constructor's body: anything it uses
    /// must be set by a field or property initializer, or come from a primary-constructor parameter.
    /// </remarks>
    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <summary>Releases the host and its HTTP clients.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases managed resources. Override to dispose additional resources in derived fixtures.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            ReleaseHost();
        }
    }

    private void CaptureEntryPointListener()
    {
        if (EntryPointListener.Current() is { } listener)
        {
            lock (_gate)
            {
                _entryPointListeners.Add(listener);
            }
        }
    }

    private void CapturePipeline(RequestDelegate pipeline)
    {
        lock (_gate)
        {
            _pipelines.Add(pipeline);
        }
    }

    private void ReleaseHost()
    {
        _factory.Dispose();

        // A variant host built with Factory.WithWebHostBuilder(...) runs this host's configuration too, so there can
        // be several of each; the factory has disposed them all.
        lock (_gate)
        {
            _pipelines.ForEach(EndpointRateLimiters.Release);
            _pipelines.Clear();
            _entryPointListeners.ForEach(EntryPointListener.Release);
            _entryPointListeners.Clear();
        }
    }

    private void Configure(IWebHostBuilder builder)
    {
        var options = _options;

        // Service registrations run on the API's entry-point thread, where WebApplicationFactory's listener is the
        // ambient one; this method itself runs on the thread that starts the host.
        builder.ConfigureServices(_ => CaptureEntryPointListener());

        // Told to the host directly as well as through the variable, so the host's environment does not depend on
        // whether anything else in the process has since changed it.
        builder.UseEnvironment(EnvironmentName);

        if (options.Configuration.Count > 0)
        {
            var values = new Dictionary<string, string?>(options.Configuration);

            // As settings, so that what the API reads before its host is built (minimal hosting's Program.cs) sees
            // them, and as the last configuration source, so that a null value hides a key of the API's own sources.
            foreach (var (key, value) in values.Where(pair => pair.Value is not null))
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        }

        builder.ConfigureTestServices(services =>
        {
            if (options.TimeProvider is { } clock)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }

            services.AddSingleton<IStartupFilter>(
                new HostStartupFilter(options.LocalPort, CapturePipeline));

            if (options.LoggerProviders.Count > 0)
            {
                services.AddLogging(logging =>
                {
                    foreach (var provider in options.LoggerProviders)
                    {
                        logging.AddProvider(provider);
                    }

                    // A recording provider sees every entry; its own MinimumLevel decides what it keeps.
                    logging.AddFilter<RecordingLoggerProvider>(null, LogLevel.Trace);
                });
            }

            options.ConfigureServices?.Invoke(services);
        });

        options.ConfigureWebHost?.Invoke(builder);

        ConfigureWebHost(builder);

        _configureDerived?.Invoke(builder);
    }

    // Builds and starts the host on a thread of its own, started without the caller's execution context, that carries
    // only the caller's culture: nothing the host starts (timers, hosted services, the API's entry-point thread) can
    // capture the test's context and keep the test, and the host with it, alive. A thread rather than a task: a task
    // waited on before it starts can run inline on the waiting thread, in the caller's context.
    private static void RunIsolated(Action start)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        ExceptionDispatchInfo? failure = null;

        void StartInContext()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            s_isolated = true;

            try
            {
                start();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        var thread = new Thread(StartInContext) { IsBackground = true, Name = "WebApiHost start" };

        thread.UnsafeStart();
        thread.Join();

        if (failure is not null)
        {
            failure.Throw();
        }
    }

    // Whether the calling thread is one RunIsolated started, on which a host starts isolated already.
    [ThreadStatic]
    private static bool s_isolated;

    private sealed class HostFactory(Action<IWebHostBuilder> configure, bool isolate) : WebApplicationFactory<TProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => configure(builder);

        // Builds and starts this factory's host, and the host of every variant made with WithWebHostBuilder(...), which
        // calls this method of the factory it was made from. A variant starts when the test first uses it, outside the
        // constructor's RunIsolated, so it is isolated here.
        protected override IHost CreateHost(IHostBuilder builder)
        {
            if (!isolate || s_isolated)
            {
                return base.CreateHost(builder);
            }

            IHost? host = null;
            RunIsolated(() => host = base.CreateHost(builder));

            return host!;
        }
    }

    // Keeps the request pipeline, to release its rate limiters with the host, and fakes the local port when asked to.
    private sealed class HostStartupFilter(int? port, Action<RequestDelegate> capture) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(pipeline =>
            {
                capture(pipeline);

                return pipeline;
            });

            if (port is { } localPort)
            {
                app.Use((context, nextMiddleware) =>
                {
                    context.Connection.LocalPort = localPort;

                    return nextMiddleware(context);
                });
            }

            next(app);
        };
    }
}

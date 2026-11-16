using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Functional;

/// <summary>
/// How a <see cref="WebApiHost{TProgram}"/> or a <see cref="WebApiTest{T}"/> builds its in-memory host: the
/// environment it runs as, configuration and services to add or replace, a clock and loggers to plug in, and the
/// options of the client its gateway uses.
/// </summary>
/// <remarks>
/// The options are read once, when the host starts; changing them afterwards changes nothing.
/// </remarks>
public sealed class WebApiHostOptions
{
    /// <summary>Options for a host that runs as <see cref="EnvironmentType.Local"/>.</summary>
    public WebApiHostOptions()
    {
    }

    /// <summary>Options for a host that runs as <paramref name="environment"/>.</summary>
    /// <param name="environment">The environment; the host runs as its lowercase name, as <c>WebApiTest</c> always has.</param>
    public WebApiHostOptions(EnvironmentType environment) => EnvironmentName = NameOf(environment);

    /// <summary>
    /// The environment name the host runs as, used as given (for example <c>"local"</c>, or a name of the API's own
    /// such as <c>"Testing"</c>). Defaults to <c>"local"</c>.
    /// </summary>
    public string EnvironmentName { get; set; } = NameOf(EnvironmentType.Local);

    /// <summary>
    /// Whether starting the host also sets the process-wide <c>ASPNETCORE_ENVIRONMENT</c> variable to
    /// <see cref="EnvironmentName"/>, which is what the environment-aware test attributes read. Defaults to
    /// <c>true</c>, as <c>WebApiTest</c> always has. The host runs as <see cref="EnvironmentName"/> either way; turn it
    /// off for a host that must not change the variable under tests running in parallel with it.
    /// </summary>
    public bool SetEnvironmentVariable { get; set; } = true;

    /// <summary>
    /// Configuration values that override the API's own (keys use <c>:</c> for sections, as in
    /// <c>"Logging:LogLevel:Default"</c>). The API sees them everywhere, including in what <c>Program.cs</c> reads
    /// from <c>builder.Configuration</c> before the host is built. A <c>null</c> value hides the key from what reads
    /// configuration once the host is built (options, an injected <c>IConfiguration</c>).
    /// </summary>
    public IDictionary<string, string?> Configuration { get; } = new Dictionary<string, string?>();

    /// <summary>
    /// Registers or replaces services. It runs after the API's own registrations (through
    /// <c>ConfigureTestServices</c>), so a registration made here wins over the API's.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; set; }

    /// <summary>
    /// Configures the web host builder directly, for anything the other options do not cover. It runs before the
    /// host's <c>ConfigureWebHost</c> override.
    /// </summary>
    public Action<IWebHostBuilder>? ConfigureWebHost { get; set; }

    /// <summary>
    /// A clock that replaces every <see cref="System.TimeProvider"/> registration of the API, for example a
    /// <c>FakeTimeProvider</c> (<c>Microsoft.Extensions.Time.Testing</c>) the test moves by hand. <c>null</c> (the
    /// default) leaves the API's clock as it is.
    /// </summary>
    public TimeProvider? TimeProvider { get; set; }

    /// <summary>
    /// Logger providers added to the host's logging, for example a <see cref="RecordingLoggerProvider"/>. The host
    /// does not dispose them. A <see cref="RecordingLoggerProvider"/> receives every entry whatever the API's
    /// <c>Logging</c> configuration says, and keeps those at or above its own
    /// <see cref="RecordingLoggerProvider.MinimumLevel"/>; any other provider is filtered by that configuration, as
    /// the API's own are.
    /// </summary>
    public IList<ILoggerProvider> LoggerProviders { get; } = [];

    /// <summary>
    /// The local port every request appears to arrive on (<c>HttpContext.Connection.LocalPort</c>), for an API that
    /// answers differently per port, such as a metrics port. <c>null</c> (the default) leaves the test server's own.
    /// </summary>
    public int? LocalPort { get; set; }

    /// <summary>
    /// The options of the client behind the gateway and of <see cref="WebApiHost{TProgram}.CreateClient()"/>: base
    /// address, redirects, cookies. Defaults to the <see cref="WebApplicationFactoryClientOptions"/> defaults.
    /// </summary>
    public WebApplicationFactoryClientOptions ClientOptions { get; set; } = new();

    /// <summary>
    /// Whether the host is built and started on a thread of its own, in an execution context that carries only the
    /// current culture and UI culture, rather than in the caller's. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// A timer the API starts while the host starts (ASP.NET Core's rate limiter runs one it never stops) captures the
    /// execution context it was started in. Started in the test's context, it holds on to whatever that context
    /// reaches, xUnit's test context included, and through it the test and its disposed host, so no host is ever
    /// collected and the test process grows with every test. Turn it off only when the API must see, while it
    /// starts, an <see cref="AsyncLocal{T}"/> value the test set other than the culture.
    /// </remarks>
    public bool IsolateExecutionContext { get; set; } = true;

    internal static string NameOf(EnvironmentType environment) => environment.ToString().ToLowerInvariant();
}

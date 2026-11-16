using ArturRios.Configuration.Enums;
using ArturRios.Util.Http;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace ArturRios.Util.Test.Functional;

/// <summary>
/// Base class for functional web API tests. It spins up an in-memory host with
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and exposes an <see cref="HttpGateway"/> plus helpers
/// for authenticating and authorizing requests. Derive from it and pass the target environment.
/// </summary>
/// <typeparam name="T">The entry point type of the web API under test (typically its <c>Program</c> class).</typeparam>
/// <remarks>
/// <para>
/// The constructor also sets the process-wide <c>ASPNETCORE_ENVIRONMENT</c> variable, because that is what
/// the environment-aware test attributes read. Two test classes deriving from this with different
/// environments therefore cannot run in parallel; put them in one xUnit collection when that comes up.
/// </para>
/// <para>
/// xUnit creates a test class instance per test, so a class deriving from this starts a host per test. To share
/// one host between the tests of a class or collection, use a <see cref="WebApiHost{TProgram}"/> fixture and
/// <see cref="SharedWebApiTest{TProgram}"/> instead. The host is the same: this class runs on a
/// <see cref="WebApiHost{TProgram}"/>.
/// </para>
/// </remarks>
public class WebApiTest<T> : IDisposable where T : class
{
    private readonly WebApiHost<T> _host;

    /// <summary>The gateway used to issue HTTP requests against the in-memory host.</summary>
    protected readonly HttpGateway Gateway;

    /// <summary>Starts the in-memory host for the given <paramref name="environment"/> and creates the gateway.</summary>
    /// <param name="environment">The environment the host should run as; also sets <c>ASPNETCORE_ENVIRONMENT</c>.</param>
    protected WebApiTest(EnvironmentType environment) : this(new WebApiHostOptions(environment))
    {
    }

    /// <summary>Starts the in-memory host for the environment named <paramref name="environmentName"/>.</summary>
    /// <param name="environmentName">
    /// The environment name the host runs as, used as given (for example <c>"Testing"</c>); also sets
    /// <c>ASPNETCORE_ENVIRONMENT</c> to it.
    /// </param>
    protected WebApiTest(string environmentName) : this(new WebApiHostOptions { EnvironmentName = environmentName })
    {
    }

    /// <summary>Starts the in-memory host built as <paramref name="options"/> say and creates the gateway.</summary>
    /// <param name="options">How to build the host: environment, configuration, services, clock, loggers, client.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The options name no environment.</exception>
    protected WebApiTest(WebApiHostOptions options)
    {
        _host = new WebApiHost<T>(options, ConfigureWebHost);

        Gateway = _host.Gateway;
    }

    /// <summary>
    /// The factory that built the host, for what the other members do not reach (for example
    /// <c>Factory.WithWebHostBuilder(...)</c> for a variant host, disposed with this one; <see cref="StartVariant"/>
    /// starts a variant the test owns).
    /// </summary>
    protected WebApplicationFactory<T> Factory => _host.Factory;

    /// <summary>
    /// Starts a variant of the host: a host of its own, built as this one is (the options, then
    /// <see cref="ConfigureWebHost"/>), then as <paramref name="configure"/> says.
    /// </summary>
    /// <param name="configure">What the variant configures after this host's configuration.</param>
    /// <returns>The started variant. The test owns it: dispose it when the test is done.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The test has been disposed.</exception>
    /// <remarks>See <see cref="WebApiHost{TProgram}.StartVariant"/>.</remarks>
    protected WebApiHost<T> StartVariant(Action<IWebHostBuilder> configure) => _host.StartVariant(configure);

    /// <summary>The in-memory test server the host runs on.</summary>
    protected TestServer Server => _host.Server;

    /// <summary>The host's root service provider, to resolve the API's services (create a scope for scoped ones).</summary>
    protected IServiceProvider Services => _host.Services;

    /// <summary>Creates a new client of the host, with the options' <see cref="WebApiHostOptions.ClientOptions"/>.</summary>
    /// <returns>A client with no default headers, for requests <see cref="Gateway"/> does not model.</returns>
    protected HttpClient CreateClient() => _host.CreateClient();

    /// <summary>Creates a new client of the host with <paramref name="options"/>.</summary>
    /// <param name="options">The client's options: base address, redirects, cookies.</param>
    /// <returns>A client with no default headers, for requests <see cref="Gateway"/> does not model.</returns>
    protected HttpClient CreateClient(WebApplicationFactoryClientOptions options) => _host.CreateClient(options);

    /// <summary>Authenticates against <paramref name="authRoute"/> and returns the resulting authentication payload.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <returns>The authentication payload returned by the API.</returns>
    /// <exception cref="TestException">Thrown when authentication fails or returns no usable token.</exception>
    public Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute) =>
        _host.AuthenticateAsync(credentials, authRoute);

    /// <summary>Sets the bearer token on the gateway's default request headers, replacing any previous one.</summary>
    /// <param name="authToken">The JWT to send as a Bearer token.</param>
    /// <remarks>
    /// Assigning the header rather than adding it makes this idempotent: calling it twice replaces the
    /// token instead of failing on a duplicate <c>Authorization</c> header, which is what
    /// <c>BaseWebApiClientRoute.Authorize</c> already did.
    /// </remarks>
    public void Authorize(string authToken) => _host.Authorize(authToken);

    /// <summary>Authenticates and applies the resulting token to the gateway's default headers.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    public Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute) =>
        _host.AuthenticateAndAuthorizeAsync(credentials, authRoute);

    /// <summary>
    /// Configures the web host builder, after the <see cref="WebApiHostOptions"/> have been applied. Override it to
    /// replace services (<c>builder.ConfigureTestServices(...)</c>), add configuration and the like.
    /// </summary>
    /// <param name="builder">The host's web host builder.</param>
    /// <remarks>
    /// It runs while the base constructor starts the host, before the derived constructor's body: anything it uses
    /// must be set by a field or property initializer, or come from a primary-constructor parameter.
    /// </remarks>
    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <summary>Releases the in-memory host and its HTTP clients.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases managed resources. Override to dispose additional resources in derived classes.</summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _host.Dispose();
        }
    }
}

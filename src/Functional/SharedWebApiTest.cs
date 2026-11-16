using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.Test.Functional;

/// <summary>
/// Base class for functional web API tests that share one <see cref="WebApiHost{TProgram}"/> per test class or
/// collection, with the members a <see cref="WebApiTest{T}"/> gives a test class. Each test gets a fresh client:
/// the constructor calls <see cref="WebApiHost{TProgram}.Reset"/>, so no header, token or cookie carries over from
/// the previous test.
/// </summary>
/// <typeparam name="TProgram">The entry point type of the web API under test (typically its <c>Program</c> class).</typeparam>
/// <remarks>
/// The derived class declares the fixture itself, so it can be a class or a collection fixture:
/// <code>
/// public class ProductsApiTests(WebApiHost&lt;Program&gt; host)
///     : SharedWebApiTest&lt;Program&gt;(host), IClassFixture&lt;WebApiHost&lt;Program&gt;&gt;
/// </code>
/// A host is only safe to share between tests that share no state through it; see the remarks on
/// <see cref="WebApiHost{TProgram}"/>.
/// </remarks>
public abstract class SharedWebApiTest<TProgram> where TProgram : class
{
    /// <summary>Resets <paramref name="host"/>'s client for the test about to run.</summary>
    /// <param name="host">The shared host (the class or collection fixture).</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <c>null</c>.</exception>
    protected SharedWebApiTest(WebApiHost<TProgram> host)
    {
        ArgumentNullException.ThrowIfNull(host);

        Host = host;
        host.Reset();
    }

    /// <summary>The shared host.</summary>
    protected WebApiHost<TProgram> Host { get; }

    /// <summary>The gateway used to issue HTTP requests against the shared host, fresh for this test.</summary>
    protected HttpGateway Gateway => Host.Gateway;

    /// <summary>Authenticates against <paramref name="authRoute"/> and returns the resulting authentication payload.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The authentication payload returned by the API.</returns>
    /// <exception cref="Exceptions.TestException">Thrown when authentication fails or returns no usable token.</exception>
    protected Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute,
        CancellationToken cancellationToken = default) =>
        Host.AuthenticateAsync(credentials, authRoute, cancellationToken);

    /// <summary>Sets the bearer token on the gateway's default request headers, replacing any previous one.</summary>
    /// <param name="authToken">The JWT to send as a Bearer token.</param>
    protected void Authorize(string authToken) => Host.Authorize(authToken);

    /// <summary>Authenticates and applies the resulting token to the gateway's default headers.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="Exceptions.TestException">Thrown when authentication fails; no token is applied then.</exception>
    protected Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute,
        CancellationToken cancellationToken = default) =>
        Host.AuthenticateAndAuthorizeAsync(credentials, authRoute, cancellationToken);
}

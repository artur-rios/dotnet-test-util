using System.Net.Http.Headers;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArturRios.Util.Test.Functional;

/// <summary>
/// Base class for functional web API tests. It spins up an in-memory host with
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and exposes an <see cref="HttpGateway"/> plus helpers
/// for authenticating and authorizing requests. Derive from it and pass the target environment.
/// </summary>
/// <typeparam name="T">The entry point type of the web API under test (typically its <c>Program</c> class).</typeparam>
/// <remarks>
/// The constructor also sets the process-wide <c>ASPNETCORE_ENVIRONMENT</c> variable, because that is what
/// the environment-aware test attributes read. Two test classes deriving from this with different
/// environments therefore cannot run in parallel; put them in one xUnit collection when that comes up.
/// </remarks>
public class WebApiTest<T> : IDisposable where T : class
{
    private readonly WebApplicationFactory<T> _factory;

    /// <summary>The gateway used to issue HTTP requests against the in-memory host.</summary>
    protected readonly HttpGateway Gateway;

    /// <summary>Starts the in-memory host for the given <paramref name="environment"/> and creates the gateway.</summary>
    /// <param name="environment">The environment the host should run as; also sets <c>ASPNETCORE_ENVIRONMENT</c>.</param>
    protected WebApiTest(EnvironmentType environment)
    {
        var environmentName = environment.ToString().ToLowerInvariant();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environmentName);

        // Told to the host directly as well as through the variable, so the host's environment does not
        // depend on whether anything else in the process has since changed it.
        _factory = new WebApplicationFactory<T>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(environmentName));

        Gateway = new HttpGateway(_factory.CreateClient());
    }

    /// <summary>Authenticates against <paramref name="authRoute"/> and returns the resulting authentication payload.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <returns>The authentication payload returned by the API.</returns>
    /// <exception cref="TestException">Thrown when authentication fails or returns no usable token.</exception>
    public async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute)
    {
        var response = await Gateway.PostAsync<DataOutput<Authentication>>(authRoute, credentials);

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
    /// <remarks>
    /// Assigning the header rather than adding it makes this idempotent: calling it twice replaces the
    /// token instead of failing on a duplicate <c>Authorization</c> header, which is what
    /// <c>BaseWebApiClientRoute.Authorize</c> already did.
    /// </remarks>
    public void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

    /// <summary>Authenticates and applies the resulting token to the gateway's default headers.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    public async Task AuthenticateAndAuthorizeAsync(Credentials credentials, string authRoute)
    {
        var authentication = await AuthenticateAsync(credentials, authRoute);

        Authorize(authentication.Token!);
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
            _factory.Dispose();
        }
    }
}

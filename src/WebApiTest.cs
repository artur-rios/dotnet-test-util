using System.Net;
using System.Reflection;
using ArturRios.Configuration.Enums;
using ArturRios.Output;
using ArturRios.Util.Http;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ArturRios.Util.Test;

/// <summary>
/// Base class for functional web API tests. It spins up an in-memory host with
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and exposes an <see cref="HttpGateway"/> plus helpers
/// for authenticating and authorizing requests. Derive from it and pass the target environment.
/// </summary>
/// <typeparam name="T">The entry point type of the web API under test (typically its <c>Program</c> class).</typeparam>
public class WebApiTest<T> : IDisposable where T : class
{
    // HttpGateway deserializes with Newtonsoft.Json, which does not populate the protected setter of
    // DataOutput<T>.Data. The auth payload is therefore parsed here with a resolver that allows it.
    private static readonly JsonSerializerSettings AuthSerializerSettings = new()
    {
        ContractResolver = new NonPublicSetterContractResolver()
    };

    private readonly WebApplicationFactory<T> _factory = new();

    /// <summary>The gateway used to issue HTTP requests against the in-memory host.</summary>
    protected readonly HttpGateway Gateway;

    /// <summary>Starts the in-memory host for the given <paramref name="environment"/> and creates the gateway.</summary>
    /// <param name="environment">The environment the host should run as; also sets <c>ASPNETCORE_ENVIRONMENT</c>.</param>
    protected WebApiTest(EnvironmentType environment)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment.ToString().ToLower());

        Gateway = new HttpGateway(_factory.CreateClient());
    }

    /// <summary>Authenticates against <paramref name="authRoute"/> and returns the resulting authentication payload.</summary>
    /// <param name="credentials">The credentials to authenticate with.</param>
    /// <param name="authRoute">The relative authentication route.</param>
    /// <returns>The authentication payload returned by the API.</returns>
    /// <exception cref="TestException">Thrown when authentication fails or returns no usable token.</exception>
    public async Task<Authentication> AuthenticateAsync(Credentials credentials, string authRoute)
    {
        var response = await Gateway.Client.PostAsync(authRoute, credentials.ToJsonStringContent());
        var json = await response.Content.ReadAsStringAsync();

        var body = response.StatusCode == HttpStatusCode.OK && !string.IsNullOrEmpty(json)
            ? JsonConvert.DeserializeObject<DataOutput<Authentication>>(json, AuthSerializerSettings)
            : null;

        var authError = body is null
                        || !body.Success
                        || body.Data is null
                        || string.IsNullOrEmpty(body.Data.Token);

        return authError ? throw new TestException("Could not authenticate") : body!.Data!;
    }

    /// <summary>Adds a bearer token to the gateway's default request headers.</summary>
    /// <param name="authToken">The JWT to send as a Bearer token.</param>
    public void Authorize(string authToken) =>
        Gateway.Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

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

    private sealed class NonPublicSetterContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (!property.Writable && member is PropertyInfo { CanWrite: true })
            {
                property.Writable = true;
            }

            return property;
        }
    }
}

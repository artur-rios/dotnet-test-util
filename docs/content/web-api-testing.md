+++
title = "Web API Testing"
show_nav       = true
nav_back_label = "Fakes"
nav_back_url   = "/dotnet-test-util/fakes"
nav_next_label = "Home"
nav_next_url   = "/dotnet-test-util"
+++

`WebApiTest<T>` is a base class for functional web API tests. It hosts your API in memory with
`WebApplicationFactory<T>` and exposes an `HttpGateway` plus helpers for authenticating and authorizing
requests.

## Getting started

Derive from `WebApiTest<T>`, where `T` is the entry point of the API under test (typically its `Program`
class), and pass the environment to run as:

```csharp
public class ProductsApiTests : WebApiTest<Program>
{
    private static readonly Credentials Credentials = new("user@test.com", "secret123");

    public ProductsApiTests() : base(EnvironmentType.Local) { }

    [Fact]
    public async Task Rejects_anonymous_requests()
    {
        var response = await Gateway.GetAsync<DataOutput<ProductOutput>>("/products/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creates_a_product()
    {
        await AuthenticateAndAuthorizeAsync(Credentials, "/auth");

        var response = await Gateway.PostAsync<DataOutput<ProductOutput>>("/products", new { Name = "Widget" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

The constructor sets `ASPNETCORE_ENVIRONMENT` to the given environment (lower-cased), starts the in-memory
host, and creates the gateway against it.

## Members

| Member | Purpose |
|---|---|
| `Gateway` | The `HttpGateway` bound to the in-memory host; use it for all requests |
| `AuthenticateAsync(Credentials, authRoute)` | Posts credentials, returns the `Authentication` payload, throws `TestException` on failure |
| `Authorize(authToken)` | Adds a `Bearer` token to the gateway's default request headers |
| `AuthenticateAndAuthorizeAsync(Credentials, authRoute)` | Authenticates and applies the returned token in one call |

`Credentials` and `Authentication` come from `ArturRios.Util.WebApi.Security.Records`. The gateway helpers
(`GetAsync<T>`, `PostAsync<T>`, `PutAsync<T>`, `PatchAsync<T>`, `DeleteAsync<T>`) return
`HttpOutput<T>` with `StatusCode`, `Headers` and a deserialized `Body`.

## Authentication contract

`AuthenticateAsync` expects the authentication route to return a `DataOutput<Authentication>` with HTTP `200`
and:

- a successful output (no errors),
- a non-null `Data`, and
- a non-empty `Data.Token`.

When any of these is missing it throws `TestException("Could not authenticate")`. On success, the token is
returned (and applied to the gateway headers when you use `AuthenticateAndAuthorizeAsync`).

## Resource cleanup

`WebApiTest<T>` implements `IDisposable` and disposes the underlying `WebApplicationFactory<T>` (and the HTTP
clients it created) when the test class is torn down. If you override disposal in a derived class, call the
protected `Dispose(bool)` overload so the host is still released.

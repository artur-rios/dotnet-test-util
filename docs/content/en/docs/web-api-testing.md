---
title: Web API Testing
weight: 30
description: >-
  `WebApiTest<T>` is a base class for functional web API tests, and `WebApiHost<TProgram>` the same in-memory host as an xUnit fixture shared by the tests of a class or collection...
---

`WebApiTest<T>` is a base class for functional web API tests. It hosts your API in memory with
`WebApplicationFactory<T>` and exposes an `HttpGateway` plus helpers for authenticating and authorizing
requests. `WebApiHost<TProgram>` is the same host as an xUnit class or collection fixture, so the tests of a class
share one host instead of starting one each, and `SharedWebApiTest<TProgram>` gives a test class using it the members
`WebApiTest<T>` gives. `WebApiHostOptions` configures either: environment, configuration, services, a fake clock,
loggers, a faked local port and the client's options.

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
| `Authorize(authToken)` | Sets the `Bearer` token on the gateway's default request headers, replacing any previous one |
| `AuthenticateAndAuthorizeAsync(Credentials, authRoute)` | Authenticates and applies the returned token in one call |

`Credentials` and `Authentication` come from `ArturRios.Util.WebApi.Security.Records`. The gateway helpers
(`GetAsync<T>`, `PostAsync<T>`, `PutAsync<T>`, `PatchAsync<T>`, `DeleteAsync<T>`) return
`HttpOutput<T>` with `StatusCode`, `Headers` and a deserialized `Body`.

## Authentication contract

`AuthenticateAsync` expects the authentication route to return a `DataOutput<Authentication>` with a 2xx status
code and:

- a successful output (no errors),
- a non-null `Data`, and
- a non-empty `Data.Token`.

When any of these is missing it throws `TestException("Could not authenticate")`. On success, the
`Authentication` payload is returned (and its `Token` applied to the gateway headers when you use `AuthenticateAndAuthorizeAsync`).

## Choosing the environment

The environment is chosen per class, or per host:

```csharp
public ProductsApiTests() : base(EnvironmentType.Staging) { }      // runs as "staging"
public ProductsApiTests() : base("Testing") { }                    // an environment name of the API's own, as given
public ProductsApiTests() : base(new WebApiHostOptions { EnvironmentName = "Testing" }) { }
```

Starting the host sets the process-wide `ASPNETCORE_ENVIRONMENT` variable to that name, which is what the
environment-aware test attributes read, so hosts of different environments cannot start in parallel: put their test
classes in one xUnit collection. A host that must leave the variable alone sets
`WebApiHostOptions.SetEnvironmentVariable = false`; it still runs as its environment.

## Configuring the host

Pass `WebApiHostOptions` to the constructor, override `ConfigureWebHost`, or both (the options apply first):

```csharp
public class EmailApiTests : WebApiTest<Program>
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public EmailApiTests() : base(new WebApiHostOptions
    {
        Configuration = { ["Mailgun:Domain"] = "mg.example.com", ["Mailgun:ApiKey"] = null },
        ConfigureServices = services => services.AddSingleton<IEmailService>(new FakeEmailService()),
        LocalPort = 9464,
        ClientOptions = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
    })
    {
    }

    // Runs while the base constructor starts the host: use fields set by initializers, as _clock is.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(_clock));
}
```

| Option | Effect |
|---|---|
| `EnvironmentName` | The environment the host runs as, used as given (default `"local"`; the `EnvironmentType` constructor lowercases) |
| `SetEnvironmentVariable` | Whether starting the host sets `ASPNETCORE_ENVIRONMENT` (default `true`) |
| `Configuration` | Values that override the API's configuration, also what `Program.cs` reads before `Build()`; a `null` value hides a key from the built host's configuration |
| `ConfigureServices` | Registers or replaces services after the API's own registrations (through `ConfigureTestServices`) |
| `ConfigureWebHost` | Configures the `IWebHostBuilder` directly |
| `TimeProvider` | Replaces every `TimeProvider` registration, for example with a `FakeTimeProvider` the test moves |
| `LoggerProviders` | Logger providers added to the host's logging, for example a `RecordingLoggerProvider` (see [Scopes & Logging](../scopes-and-logging/)) |
| `LocalPort` | The local port every request appears to arrive on (`HttpContext.Connection.LocalPort`), for an API that answers per port |
| `ClientOptions` | The options of the gateway's client and of `CreateClient()`: base address, redirects, cookies |
| `IsolateExecutionContext` | Whether the host starts in an execution context of its own (default `true`; see [Host lifetime and memory](#host-lifetime-and-memory)) |

A derived class's `ConfigureWebHost` runs while the base constructor starts the host, before the derived
constructor's body: whatever it uses must come from a field or property initializer, or a primary-constructor
parameter.

## Reaching the host directly

For what the gateway does not model, a test reaches the host itself:

| Member | Purpose |
|---|---|
| `Factory` | The `WebApplicationFactory<T>` that built the host (for example `Factory.WithWebHostBuilder(...)` for a variant host, disposed with this one) |
| `Server` | The in-memory `TestServer` |
| `Services` | The host's root service provider (create a scope for scoped services) |
| `CreateClient()`, `CreateClient(WebApplicationFactoryClientOptions)` | A new raw `HttpClient` against the host |

They are `protected` on `WebApiTest<T>` and `public` on `WebApiHost<TProgram>`, which also has `Client`, the raw
client behind its `Gateway`.

## Sharing one host between tests

xUnit creates a test class instance per test, so a class deriving from `WebApiTest<T>` starts a whole host for every
test. To start one per class (or per collection), use `WebApiHost<TProgram>` as the fixture and derive from
`SharedWebApiTest<TProgram>`:

```csharp
public class ProductsApiTests(WebApiHost<Program> host)
    : SharedWebApiTest<Program>(host), IClassFixture<WebApiHost<Program>>
{
    [Fact]
    public async Task Creates_a_product()
    {
        await AuthenticateAndAuthorizeAsync(Credentials, "/auth");

        var response = await Gateway.PostAsync<DataOutput<ProductOutput>>("/products", new { Name = "Widget" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

`SharedWebApiTest<TProgram>` gives the test `Host`, `Gateway`, `AuthenticateAsync`, `Authorize` and
`AuthenticateAndAuthorizeAsync`, and calls `Host.Reset()` before each test: the gateway gets a fresh client, so no
header, token or cookie carries over from the previous test. To configure a shared host, derive a fixture:

```csharp
public sealed class ApiHost() : WebApiHost<Program>(new WebApiHostOptions { EnvironmentName = "Testing" })
{
    public FakeTimeProvider Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
}

public class OrdersApiTests(ApiHost host) : SharedWebApiTest<Program>(host), IClassFixture<ApiHost>;
```

The same fixture shares one host across several classes as an `ICollectionFixture<ApiHost>`.

### When a shared host is unsafe

`Reset()` resets the client, not the API. Everything the API keeps in memory lives as long as the host and is seen
by every test that shares it:

- **Rate-limiter budgets.** A limiter that allows ten anonymous authentications a minute is spent by the tenth test
  that authenticates, and every later test gets `429 Too Many Requests`.
- Caches, in-memory stores, counters and other stateful singletons.
- A fake clock one test moved, a fake service one test configured.
- The entries a `RecordingLoggerProvider` kept (call `Clear()`, or read only the entries the test caused).

A test that depends on such state, or changes it in a way another test would notice, needs a host of its own: a
`WebApiTest<T>`, or a `WebApiHost<TProgram>` it creates and disposes itself. State the API keeps outside the
process, such as a database, is shared either way.

## Host lifetime and memory

Starting a host per test is only affordable when disposed hosts are collected. Three things kept every host of an
API alive for the rest of the run, so a suite's test process grew with every test:

1. A timer the API starts and never stops captures the execution context it was started in. The host used to start
   in the test's context, and xUnit's test context reaches the test, which holds the host. The host now starts in a
   context of its own that carries only the current culture and UI culture (`IsolateExecutionContext`).
2. `WebApplicationFactory` runs a minimal-hosting API's entry point on a thread whose context holds the factory's
   host-building callbacks and the built host, so a timer started on that thread captured those too. Disposing the
   host now releases what that context holds.
3. ASP.NET Core's rate-limiting middleware never disposes the limiter it builds for endpoint policies, and that
   limiter's timer holds the middleware and the whole request pipeline. Disposing the host now disposes it.

The last two reach into internals of `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.AspNetCore.RateLimiting`; if a
future version changes them, the host is disposed as before and stays alive as it used to. Turn
`IsolateExecutionContext` off only when the API must see, while it starts, an `AsyncLocal` value the test set other
than the culture. A global limiter the API sets in `RateLimiterOptions.GlobalLimiter` is the API's own and is not
disposed: if its partitioner holds the API's services, it still keeps them alive. A variant host built with
`Factory.WithWebHostBuilder(...)` gets fixes 2 and 3 when the host is disposed, but starts when the test first uses
it, in the test's own execution context.

## Resource cleanup

`WebApiTest<T>` and `WebApiHost<TProgram>` implement `IDisposable` and dispose the underlying
`WebApplicationFactory<T>` (and the HTTP clients it created) when the test class or fixture is torn down. If you
override disposal in a derived class, call the protected `Dispose(bool)` overload so the host is still released.

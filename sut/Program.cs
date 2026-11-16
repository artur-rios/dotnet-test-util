using System.Globalization;
using System.Threading.RateLimiting;
using ArturRios.Output;
using ArturRios.Util.WebApi.Security.Records;
using Microsoft.AspNetCore.RateLimiting;

// The cultures the app starts in: a test that sets them before it starts the host should find them here.
var startupCulture = CultureInfo.CurrentCulture.Name;
var startupUiCulture = CultureInfo.CurrentUICulture.Name;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

// A configuration value read while the app is set up, before the host is built.
var startupGreeting = builder.Configuration["Sut:Greeting"] ?? string.Empty;

const string limitedPolicy = "limited";

// ASP.NET Core's rate limiter, as the APIs this library tests use it: a fixed window of one request a minute on
// GET /limited. Its budget lives as long as the host, so tests sharing a host share it.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(limitedPolicy, limiter =>
    {
        limiter.PermitLimit = 1;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

var app = builder.Build();

app.UseRateLimiter();

// Valid credentials accepted by the fake authentication route.
const string validEmail = "user@test.com";
const string validPassword = "password123";
const string issuedToken = "test-token";

// POST /auth — issues a token for valid credentials, otherwise returns a failed output.
app.MapPost("/auth", (Credentials credentials) =>
{
    if (credentials.Email == validEmail && credentials.Password == validPassword)
    {
        var authentication = new Authentication(
            issuedToken,
            true,
            DateTime.UtcNow.ToString("o"),
            DateTime.UtcNow.AddHours(1).ToString("o"));

        return Results.Ok(DataOutput<Authentication>.New.WithData(authentication));
    }

    return Results.Ok(DataOutput<Authentication>.New.WithError("Invalid credentials"));
});

// POST /auth/{variant} — answers a valid credential the ways an authentication route can fail to issue a usable
// token, so the tests can cover each case AuthenticateAsync rejects.
app.MapPost("/auth/{variant}", (string variant) =>
{
    var authentication = new Authentication(
        issuedToken,
        true,
        DateTime.UtcNow.ToString("o"),
        DateTime.UtcNow.AddHours(1).ToString("o"));

    return variant switch
    {
        // A usable payload, but an error status.
        "server-error" => Results.Json(DataOutput<Authentication>.New.WithData(authentication), statusCode: 500),
        // A success status whose body is not the expected JSON.
        "not-json" => Results.Text("not json"),
        // A successful output that carries no authentication.
        "no-data" => Results.Ok(DataOutput<Authentication>.New),
        // An authentication without a token.
        "null-token" => Results.Ok(DataOutput<Authentication>.New.WithData(authentication with { Token = null })),
        "empty-token" => Results.Ok(DataOutput<Authentication>.New.WithData(authentication with { Token = "" })),
        _ => Results.NotFound()
    };
});

// GET /environment — the environment the host runs as.
app.MapGet("/environment", (IWebHostEnvironment environment) => Results.Ok(environment.EnvironmentName));

// GET /secure — succeeds only when the issued bearer token is present.
app.MapGet("/secure", (HttpContext context) =>
{
    var header = context.Request.Headers.Authorization.ToString();

    return header == $"Bearer {issuedToken}"
        ? Results.Ok(DataOutput<string>.New.WithData("authorized"))
        : Results.Unauthorized();
});

// GET /culture — the culture and UI culture the app started in.
app.MapGet("/culture", () => Results.Ok($"{startupCulture}|{startupUiCulture}"));

// GET /configuration/{key} — a configuration value as the built host sees it, empty when there is none.
app.MapGet("/configuration/{key}", (string key, IConfiguration configuration) =>
    Results.Ok(configuration[key] ?? string.Empty));

// GET /configuration/startup — the Sut:Greeting value as the app read it before the host was built.
app.MapGet("/configuration/startup", () => Results.Ok(startupGreeting));

// GET /time — the time on the app's clock.
app.MapGet("/time", (TimeProvider clock) => Results.Ok(clock.GetUtcNow().ToString("o", CultureInfo.InvariantCulture)));

// GET /port — the local port the request arrived on.
app.MapGet("/port", (HttpContext context) => Results.Ok(context.Connection.LocalPort));

// GET /limited — answers once a minute, and 429 after that.
app.MapGet("/limited", () => Results.Ok("allowed")).RequireRateLimiting(limitedPolicy);

// POST /cookie — sets a cookie; GET /cookie — the cookie's value as the request carries it, empty when it carries none.
app.MapPost("/cookie", (HttpContext context) =>
{
    context.Response.Cookies.Append("visit", "1");

    return Results.Ok();
});
app.MapGet("/cookie", (HttpContext context) => Results.Ok(context.Request.Cookies["visit"] ?? string.Empty));

// POST /log — logs a debug entry, and a warning with an event id inside a scope.
app.MapPost("/log", (ILogger<Program> logger) =>
{
    logger.LogDebug("Debug entry");

    using (logger.BeginScope("Request {Name}", "log"))
    {
        logger.LogWarning(new EventId(7, "Seven"), "Warned {Count} times", 3);
    }

    return Results.Ok();
});

app.Run();

/// <summary>Entry point exposed so tests can host the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;

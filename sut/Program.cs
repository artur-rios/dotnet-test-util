using ArturRios.Output;
using ArturRios.Util.WebApi.Security.Records;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

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

app.Run();

/// <summary>Entry point exposed so tests can host the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;

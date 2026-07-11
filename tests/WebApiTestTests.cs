using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.Test.Tests;

/// <summary>
/// Exercises <see cref="WebApiTest{T}"/> against the in-memory SUT web app defined in the <c>sut</c> project.
/// </summary>
public class WebApiTestTests : WebApiTest<Program>
{
    private const string IssuedToken = "test-token";
    private static readonly Credentials ValidCredentials = new("user@test.com", "password123");
    private static readonly Credentials InvalidCredentials = new("user@test.com", "wrong");

    public WebApiTestTests() : base(EnvironmentType.Local)
    {
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsToken()
    {
        var authentication = await AuthenticateAsync(ValidCredentials, "/auth");

        Assert.True(authentication.Valid);
        Assert.Equal(IssuedToken, authentication.Token);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidCredentials_ThrowsTestException()
    {
        await Assert.ThrowsAsync<TestException>(() => AuthenticateAsync(InvalidCredentials, "/auth"));
    }

    [Fact]
    public async Task SecureEndpoint_WithoutAuthorization_ReturnsUnauthorized()
    {
        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_AddsBearerToken_AllowingAccessToSecureEndpoint()
    {
        Authorize(IssuedToken);

        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAndAuthorizeAsync_AllowsAccessToSecureEndpoint()
    {
        await AuthenticateAndAuthorizeAsync(ValidCredentials, "/auth");

        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

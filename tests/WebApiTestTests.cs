using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.Test.Functional;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.Test.Tests;

/// <summary>
/// Exercises <see cref="WebApiTest{T}"/> against the in-memory SUT web app defined in the <c>sut</c> project.
/// </summary>
[Trait("Category", "Functional")]
public class WebApiTestTests : WebApiTest<Program>
{
    private const string IssuedToken = "test-token";
    private static readonly Credentials ValidCredentials = new("user@test.com", "password123");
    private static readonly Credentials InvalidCredentials = new("user@test.com", "wrong");

    public WebApiTestTests() : base(EnvironmentType.Local)
    {
    }

    [Fact]
    public async Task GivenValidCredentials_WhenAuthenticating_ThenATokenComesBack()
    {
        var authentication = await AuthenticateAsync(ValidCredentials, "/auth");

        Assert.True(authentication.Valid);
        Assert.Equal(IssuedToken, authentication.Token);
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenAuthenticating_ThenATestExceptionIsThrown()
    {
        await Assert.ThrowsAsync<TestException>(() => AuthenticateAsync(InvalidCredentials, "/auth"));
    }

    [Fact]
    public async Task GivenNoAuthorization_WhenCallingASecuredEndpoint_ThenUnauthorizedComesBack()
    {
        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenABearerTokenIsApplied_WhenCallingASecuredEndpoint_ThenAccessIsAllowed()
    {
        Authorize(IssuedToken);

        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenAuthenticatingAndAuthorising_ThenAccessToASecuredEndpointIsAllowed()
    {
        await AuthenticateAndAuthorizeAsync(ValidCredentials, "/auth");

        var response = await Gateway.GetAsync<string>("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

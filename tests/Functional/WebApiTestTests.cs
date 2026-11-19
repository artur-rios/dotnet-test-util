using System.Net;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.Test.Functional;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Exercises <see cref="WebApiTest{T}"/> against the in-memory SUT web app defined in the <c>sut</c> project, as a test
/// class deriving from it does: xUnit creates one instance, and so one host, per test.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
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
    public async Task GivenValidCredentials_WhenAuthenticating_ThenNoTokenIsAppliedToTheGateway()
    {
        await AuthenticateAsync(ValidCredentials, "/auth");

        Assert.Null(Gateway.Client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenAuthenticating_ThenATestExceptionIsThrown()
    {
        var exception = await Assert.ThrowsAsync<TestException>(() => AuthenticateAsync(InvalidCredentials, "/auth"));

        Assert.Equal("Could not authenticate", exception.Message);
    }

    [Theory]
    [InlineData("server-error")] // a usable payload with an error status
    [InlineData("not-json")] // a success status with a body that is not an output
    [InlineData("no-data")] // a successful output with no authentication
    [InlineData("null-token")]
    [InlineData("empty-token")]
    [InlineData("unknown")] // a route that answers 404 with no body
    public async Task GivenAnAuthenticationRouteThatIssuesNoUsableToken_WhenAuthenticating_ThenATestExceptionIsThrown(
        string variant)
    {
        await Assert.ThrowsAsync<TestException>(() => AuthenticateAsync(ValidCredentials, $"/auth/{variant}"));
    }

    [Fact]
    public async Task GivenNoAuthorization_WhenCallingASecuredEndpoint_ThenUnauthorizedComesBack()
    {
        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenABearerTokenIsApplied_WhenCallingASecuredEndpoint_ThenAccessIsAllowed()
    {
        Authorize(IssuedToken);

        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", Gateway.Client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal(IssuedToken, Gateway.Client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async Task GivenATokenIsAlreadyApplied_WhenAuthorizingAgain_ThenTheNewTokenReplacesIt()
    {
        Authorize(IssuedToken);
        Authorize("another-token");

        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("another-token", Gateway.Client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenAuthenticatingAndAuthorising_ThenAccessToASecuredEndpointIsAllowed()
    {
        await AuthenticateAndAuthorizeAsync(ValidCredentials, "/auth");

        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenAuthenticatingAndAuthorising_ThenATestExceptionIsThrownAndNoTokenIsApplied()
    {
        await Assert.ThrowsAsync<TestException>(() => AuthenticateAndAuthorizeAsync(InvalidCredentials, "/auth"));

        Assert.Null(Gateway.Client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenTheLocalEnvironment_WhenTheHostIsAskedForItsEnvironment_ThenItRunsAsLocal()
    {
        var response = await Gateway.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal("local", response.Body);
    }
}

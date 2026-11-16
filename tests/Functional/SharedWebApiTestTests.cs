using System.Collections.Concurrent;
using System.Net;
using ArturRios.Util.Test.Exceptions;
using ArturRios.Util.Test.Functional;
using ArturRios.Util.WebApi.Security.Records;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Exercises <see cref="SharedWebApiTest{TProgram}"/> as a test class deriving from it does: xUnit creates one instance
/// per test, and gives every instance the one <see cref="WebApiHost{TProgram}"/> class fixture.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
public class SharedWebApiTestTests(WebApiHost<Program> host)
    : SharedWebApiTest<Program>(host), IClassFixture<WebApiHost<Program>>
{
    private const string IssuedToken = "test-token";
    private static readonly Credentials ValidCredentials = new("user@test.com", "password123");
    private static readonly Credentials InvalidCredentials = new("user@test.com", "wrong");
    private static readonly ConcurrentDictionary<WebApiHost<Program>, byte> HostsSeen = new();

    private sealed class FreshTest(WebApiHost<Program> host) : SharedWebApiTest<Program>(host);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GivenTheTestsOfAClass_WhenEachRuns_ThenTheyAllGetTheSameHost(int test)
    {
        HostsSeen.TryAdd(Host, 0);

        Assert.True(test > 0);
        Assert.Single(HostsSeen);
    }

    [Fact]
    public async Task GivenATokenAppliedToTheSharedHost_WhenTheNextTestStarts_ThenItsClientHasNoAuthorization()
    {
        Authorize(IssuedToken);

        _ = new FreshTest(Host);

        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);
        Assert.Null(Gateway.Client.DefaultRequestHeaders.Authorization);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GivenACookieSetOnTheSharedHost_WhenTheNextTestStarts_ThenItsClientCarriesNoCookie()
    {
        await Gateway.PostAsync<string>("/cookie", new { }, TestContext.Current.CancellationToken);
        var before = await Gateway.GetAsync<string>("/cookie", TestContext.Current.CancellationToken);

        _ = new FreshTest(Host);

        var after = await Gateway.GetAsync<string>("/cookie", TestContext.Current.CancellationToken);
        Assert.Equal("1", before.Body);
        Assert.Equal(string.Empty, after.Body);
    }

    [Fact]
    public async Task GivenAResetHost_WhenTheClientItReplacedIsUsed_ThenItIsRefused()
    {
        var previous = Host.Client;

        Host.Reset();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => previous.GetAsync("/environment", TestContext.Current.CancellationToken));
        Assert.NotSame(previous, Host.Client);
        Assert.Same(Host.Client, Gateway.Client);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenAuthenticating_ThenATokenComesBackAndIsNotApplied()
    {
        var authentication = await AuthenticateAsync(ValidCredentials, "/auth", TestContext.Current.CancellationToken);

        Assert.Equal(IssuedToken, authentication.Token);
        Assert.Null(Gateway.Client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenValidCredentials_WhenAuthenticatingAndAuthorising_ThenAccessToASecuredEndpointIsAllowed()
    {
        await AuthenticateAndAuthorizeAsync(ValidCredentials, "/auth", TestContext.Current.CancellationToken);

        var response = await Gateway.GetAsync<string>("/secure", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GivenInvalidCredentials_WhenAuthenticatingAndAuthorising_ThenATestExceptionIsThrownAndNoTokenIsApplied()
    {
        await Assert.ThrowsAsync<TestException>(
            () => AuthenticateAndAuthorizeAsync(InvalidCredentials, "/auth", TestContext.Current.CancellationToken));

        Assert.Null(Gateway.Client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenAuthenticating_ThenTheRequestIsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => AuthenticateAsync(ValidCredentials, "/auth", cancellation.Token));
    }

    [Fact]
    public void GivenNoHost_WhenATestIsCreated_ThenItIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new FreshTest(null!));
    }
}

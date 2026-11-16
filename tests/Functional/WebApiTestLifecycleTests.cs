using ArturRios.Configuration.Enums;
using ArturRios.Util.Http;
using ArturRios.Util.Test.Functional;

namespace ArturRios.Util.Test.Tests.Functional;

/// <summary>
/// Exercises how <see cref="WebApiTest{T}"/> starts and releases its host, with hosts the tests create and dispose
/// themselves rather than the one xUnit gives a derived test class.
/// </summary>
[Trait("Category", "Functional")]
[Collection(EnvironmentVariableCollection.Name)]
public sealed class WebApiTestLifecycleTests : IDisposable
{
    private const string Variable = "ASPNETCORE_ENVIRONMENT";

    private readonly string? _originalEnvironment = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _originalEnvironment);

    /// <summary>A host as a test class deriving from <see cref="WebApiTest{T}"/> sees it.</summary>
    private sealed class SutHost(EnvironmentType environment) : WebApiTest<Program>(environment)
    {
        public HttpGateway Http => Gateway;

        public List<bool> DisposeCalls { get; } = [];

        protected override void Dispose(bool disposing)
        {
            DisposeCalls.Add(disposing);

            base.Dispose(disposing);
        }
    }

    [Theory]
    [InlineData(EnvironmentType.Local, "local")]
    [InlineData(EnvironmentType.Development, "development")]
    [InlineData(EnvironmentType.Staging, "staging")]
    [InlineData(EnvironmentType.Production, "production")]
    public async Task GivenAnEnvironment_WhenTheHostStarts_ThenTheVariableAndTheHostUseItsLowercaseName(
        EnvironmentType environment, string expected)
    {
        using var host = new SutHost(environment);

        var response = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal(expected, Environment.GetEnvironmentVariable(Variable));
        Assert.Equal(expected, response.Body);
    }

    [Fact]
    public async Task GivenTheVariableChangesAfterTheHostStarted_WhenTheHostIsAsked_ThenItStillRunsAsItsOwnEnvironment()
    {
        using var host = new SutHost(EnvironmentType.Staging);

        Environment.SetEnvironmentVariable(Variable, "production");

        var response = await host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken);

        Assert.Equal("staging", response.Body);
    }

    [Fact]
    public void GivenADerivedHost_WhenDisposed_ThenItsDisposeOverrideRunsOnceWithDisposingTrue()
    {
        var host = new SutHost(EnvironmentType.Local);

        host.Dispose();

        Assert.Equal([true], host.DisposeCalls);
    }

    [Fact]
    public async Task GivenADisposedHost_WhenARequestIsSent_ThenItIsRefused()
    {
        var host = new SutHost(EnvironmentType.Local);

        host.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => host.Http.GetAsync<string>("/environment", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GivenADisposedHost_WhenDisposedAgain_ThenNothingThrows()
    {
        var host = new SutHost(EnvironmentType.Local);
        host.Dispose();

        var exception = Record.Exception(host.Dispose);

        Assert.Null(exception);
    }
}

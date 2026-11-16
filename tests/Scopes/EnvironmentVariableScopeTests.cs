using ArturRios.Util.Test.Scopes;

namespace ArturRios.Util.Test.Tests.Scopes;

// Each test uses variables of its own, named after a new Guid, so these tests can run in parallel with every other:
// none reads a variable another test sets.
[Trait("Category", "Unit")]
public sealed class EnvironmentVariableScopeTests : IDisposable
{
    private readonly string _first = $"TEST_UTIL_{Guid.NewGuid():N}";
    private readonly string _second = $"TEST_UTIL_{Guid.NewGuid():N}";

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_first, null);
        Environment.SetEnvironmentVariable(_second, null);
    }

    [Fact]
    public void GivenVariablesThatHadValues_WhenTheScopeIsDisposed_ThenTheirValuesAreBack()
    {
        Environment.SetEnvironmentVariable(_first, "before");
        Environment.SetEnvironmentVariable(_second, "kept");

        using (new EnvironmentVariableScope((_first, "during"), (_second, null)))
        {
            Assert.Equal("during", Environment.GetEnvironmentVariable(_first));
            Assert.Null(Environment.GetEnvironmentVariable(_second));
        }

        Assert.Equal("before", Environment.GetEnvironmentVariable(_first));
        Assert.Equal("kept", Environment.GetEnvironmentVariable(_second));
    }

    [Fact]
    public void GivenVariablesThatWereUnset_WhenTheScopeIsDisposed_ThenTheyAreUnsetAgain()
    {
        using (new EnvironmentVariableScope().Set(_first, "during").Set(_second, "during too"))
        {
            Assert.Equal("during", Environment.GetEnvironmentVariable(_first));
        }

        Assert.Null(Environment.GetEnvironmentVariable(_first));
        Assert.Null(Environment.GetEnvironmentVariable(_second));
    }

    [Fact]
    public void GivenVariablesToClear_WhenTheScopeIsDisposed_ThenTheirValuesAreBack()
    {
        Environment.SetEnvironmentVariable(_first, "one");
        Environment.SetEnvironmentVariable(_second, "two");

        using (new EnvironmentVariableScope().Unset(_first, _second))
        {
            Assert.Null(Environment.GetEnvironmentVariable(_first));
            Assert.Null(Environment.GetEnvironmentVariable(_second));
        }

        Assert.Equal("one", Environment.GetEnvironmentVariable(_first));
        Assert.Equal("two", Environment.GetEnvironmentVariable(_second));
    }

    [Fact]
    public void GivenAVariableChangedTwice_WhenTheScopeIsDisposed_ThenTheValueBeforeTheFirstChangeIsBack()
    {
        Environment.SetEnvironmentVariable(_first, "original");

        using (var scope = new EnvironmentVariableScope((_first, "first")))
        {
            scope.Set(_first, "second").Unset(_first);
        }

        Assert.Equal("original", Environment.GetEnvironmentVariable(_first));
    }

    [Fact]
    public void GivenATestThatThrows_WhenItsScopeIsDisposedByUsing_ThenTheVariablesAreRestored()
    {
        Environment.SetEnvironmentVariable(_first, "before");

        Assert.Throws<InvalidOperationException>(void () =>
        {
            using var scope = new EnvironmentVariableScope((_first, "during"));

            throw new InvalidOperationException("the test failed");
        });

        Assert.Equal("before", Environment.GetEnvironmentVariable(_first));
    }

    [Fact]
    public void GivenAVariableThatCannotBeSet_WhenTheScopeIsCreated_ThenTheOnesAlreadySetAreRestoredAndItThrows()
    {
        Environment.SetEnvironmentVariable(_first, "before");

        Assert.Throws<ArgumentException>(() => new EnvironmentVariableScope((_first, "during"), ("A=B", "value")));

        Assert.Equal("before", Environment.GetEnvironmentVariable(_first));
    }

    [Fact]
    public void GivenADisposedScope_WhenDisposedAgain_ThenTheVariablesAreNotTouched()
    {
        var scope = new EnvironmentVariableScope((_first, "during"));
        scope.Dispose();
        Environment.SetEnvironmentVariable(_first, "changed after the scope");

        scope.Dispose();

        Assert.Equal("changed after the scope", Environment.GetEnvironmentVariable(_first));
    }

    [Fact]
    public void GivenADisposedScope_WhenAVariableIsSet_ThenItIsRefused()
    {
        var scope = new EnvironmentVariableScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Set(_first, "value"));
        Assert.Null(Environment.GetEnvironmentVariable(_first));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GivenNoName_WhenAVariableIsSet_ThenItIsRefused(string? name)
    {
        using var scope = new EnvironmentVariableScope();

        Assert.ThrowsAny<ArgumentException>(() => scope.Set(name!, "value"));
    }

    [Fact]
    public void GivenNoVariables_WhenTheScopeIsCreatedOrClears_ThenItIsRefused()
    {
        using var scope = new EnvironmentVariableScope();

        Assert.Throws<ArgumentNullException>(() => new EnvironmentVariableScope(null!));
        Assert.Throws<ArgumentNullException>(() => scope.Unset(null!));
    }
}

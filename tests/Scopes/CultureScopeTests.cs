using System.Globalization;
using ArturRios.Util.Test.Scopes;

namespace ArturRios.Util.Test.Tests.Scopes;

// The current cultures belong to the test's execution context, so these tests run in parallel with every other.
[Trait("Category", "Unit")]
public sealed class CultureScopeTests
{
    [Fact]
    public void GivenACultureName_WhenTheScopeIsCreated_ThenItIsBothTheCultureAndTheUiCulture()
    {
        using var scope = new CultureScope("pt-BR");

        Assert.Equal("pt-BR", CultureInfo.CurrentCulture.Name);
        Assert.Equal("pt-BR", CultureInfo.CurrentUICulture.Name);
        Assert.Same(CultureInfo.CurrentCulture, scope.Culture);
        Assert.Same(CultureInfo.CurrentUICulture, scope.UICulture);
        Assert.Equal("1.234,5", 1234.5.ToString("N1"));
    }

    [Fact]
    public void GivenTwoCultureNames_WhenTheScopeIsCreated_ThenTheFirstIsTheCultureAndTheSecondTheUiCulture()
    {
        using var scope = new CultureScope("de-DE", "ja-JP");

        Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
        Assert.Equal("ja-JP", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void GivenACultureAndNoUiCulture_WhenTheScopeIsCreated_ThenTheCultureIsBoth()
    {
        using var scope = new CultureScope(CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal("fr-FR", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void GivenAnEmptyName_WhenTheScopeIsCreated_ThenTheCulturesAreInvariant()
    {
        using var scope = new CultureScope("");

        Assert.Same(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
        Assert.Same(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void GivenAScope_WhenItIsDisposed_ThenTheCulturesItFoundAreBack()
    {
        using var outer = new CultureScope("en-US", "es-ES");

        using (new CultureScope("pt-BR"))
        {
            Assert.Equal("pt-BR", CultureInfo.CurrentCulture.Name);
        }

        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
        Assert.Equal("es-ES", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void GivenATestThatThrows_WhenItsScopeIsDisposedByUsing_ThenTheCulturesAreRestored()
    {
        using var outer = new CultureScope("en-US");

        Assert.Throws<InvalidOperationException>(void () =>
        {
            using var scope = new CultureScope("pt-BR");

            throw new InvalidOperationException("the test failed");
        });

        Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public void GivenADisposedScope_WhenDisposedAgain_ThenTheCulturesAreNotTouched()
    {
        using var outer = new CultureScope("en-US");
        var scope = new CultureScope("pt-BR");
        scope.Dispose();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

        scope.Dispose();

        Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
    }

    [Fact]
    public async Task GivenAScope_WhenTheTestAwaits_ThenTheCultureFlowsWithIt()
    {
        using var scope = new CultureScope("pt-BR");

        var seen = await Task.Run(() => CultureInfo.CurrentCulture.Name, TestContext.Current.CancellationToken);

        Assert.Equal("pt-BR", seen);
    }

    [Fact]
    public void GivenAnUnknownOrMissingName_WhenTheScopeIsCreated_ThenItIsRefusedAndTheCulturesAreUnchanged()
    {
        var before = CultureInfo.CurrentCulture;

        Assert.Throws<CultureNotFoundException>(() => new CultureScope("not a culture!"));
        Assert.Throws<ArgumentNullException>(() => new CultureScope((string)null!));
        Assert.Throws<ArgumentNullException>(() => new CultureScope((CultureInfo)null!));

        Assert.Same(before, CultureInfo.CurrentCulture);
    }
}

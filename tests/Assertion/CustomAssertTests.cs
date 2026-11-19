using System.Collections;
using ArturRios.Util.Test.Assertion;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Tests.Assertion;

[Trait("Category", "Unit")]
public class CustomAssertTests
{
    // --- IEnumerable overloads ---

    [Fact]
    public void GivenANullCollection_WhenAssertingItIsNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NullOrEmpty((IEnumerable?)null);
    }

    [Fact]
    public void GivenAnEmptyCollection_WhenAssertingItIsNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NullOrEmpty(Array.Empty<int>());
    }

    [Fact]
    public void GivenANonEmptyCollection_WhenAssertingItIsNullOrEmpty_ThenTheAssertionFails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrEmpty(new[] { 1 }));
    }

    [Fact]
    public void GivenANonEmptyCollection_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NotNullOrEmpty(new[] { 1 });
    }

    [Fact]
    public void GivenANullCollection_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionFails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NotNullOrEmpty((IEnumerable?)null));
    }

    [Fact]
    public void GivenAnEmptyCollection_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionFails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NotNullOrEmpty(Array.Empty<int>()));
    }

    [Fact]
    public void GivenAnEmptyLazySequence_WhenAssertingItIsNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NullOrEmpty(Enumerable.Empty<int>().Where(_ => true));
    }

    [Fact]
    public void GivenAnEndlessLazySequence_WhenAssertingItIsNotNullOrEmpty_ThenItPassesAfterReadingOneElement()
    {
        var read = 0;

        CustomAssert.NotNullOrEmpty(Endless());

        Assert.Equal(1, read);

        IEnumerable<int> Endless()
        {
            while (true)
            {
                read++;
                yield return read;
            }
        }
    }

    [Fact]
    public void GivenANonGenericCollection_WhenAssertingIt_ThenItsCountDecides()
    {
        CustomAssert.NullOrEmpty(new ArrayList());
        CustomAssert.NotNullOrEmpty(new ArrayList { "item" });
        Assert.Throws<TrueException>(() => CustomAssert.NullOrEmpty(new Hashtable { ["key"] = "value" }));
    }

    // --- string overloads ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GivenAnEmptyString_WhenAssertingItIsNullOrEmpty_ThenTheAssertionPasses(string? value)
    {
        CustomAssert.NullOrEmpty(value);
    }

    [Fact]
    public void GivenAWhitespaceString_WhenAssertingItIsNullOrEmpty_ThenTheAssertionFails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrEmpty(" "));
    }

    [Fact]
    public void GivenANonEmptyString_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NotNullOrEmpty("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GivenAnEmptyString_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionFails(string? value)
    {
        Assert.Throws<FalseException>(() => CustomAssert.NotNullOrEmpty(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenAWhitespaceString_WhenAssertingItIsNullOrWhiteSpace_ThenTheAssertionPasses(string? value)
    {
        CustomAssert.NullOrWhiteSpace(value);
    }

    [Fact]
    public void GivenAStringWithContent_WhenAssertingItIsNullOrWhiteSpace_ThenTheAssertionFails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrWhiteSpace("x"));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("  x  ")]
    public void GivenAStringWithContent_WhenAssertingItIsNotNullOrWhiteSpace_ThenTheAssertionPasses(string value)
    {
        CustomAssert.NotNullOrWhiteSpace(value);
    }

    [Theory]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("\u00A0")] // a no-break space
    public void GivenOtherWhiteSpaceCharacters_WhenAssertingTheStringIsNullOrWhiteSpace_ThenTheAssertionPasses(string value)
    {
        CustomAssert.NullOrWhiteSpace(value);
        Assert.Throws<FalseException>(() => CustomAssert.NotNullOrWhiteSpace(value));
    }

    [Fact]
    public void GivenAWhitespaceString_WhenAssertingItIsNotNullOrEmpty_ThenTheAssertionPasses()
    {
        CustomAssert.NotNullOrEmpty(" ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenAWhitespaceString_WhenAssertingItIsNotNullOrWhiteSpace_ThenTheAssertionFails(string? value)
    {
        Assert.Throws<FalseException>(() => CustomAssert.NotNullOrWhiteSpace(value));
    }
}

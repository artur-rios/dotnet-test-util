using System.Collections;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Tests;

public class CustomAssertTests
{
    // --- IEnumerable overloads ---

    [Fact]
    public void NullOrEmpty_WithNullCollection_Passes()
    {
        CustomAssert.NullOrEmpty((IEnumerable?)null);
    }

    [Fact]
    public void NullOrEmpty_WithEmptyCollection_Passes()
    {
        CustomAssert.NullOrEmpty(Array.Empty<int>());
    }

    [Fact]
    public void NullOrEmpty_WithNonEmptyCollection_Fails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrEmpty(new[] { 1 }));
    }

    [Fact]
    public void NotNullOrEmpty_WithNonEmptyCollection_Passes()
    {
        CustomAssert.NotNullOrEmpty(new[] { 1 });
    }

    [Fact]
    public void NotNullOrEmpty_WithNullCollection_Fails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NotNullOrEmpty((IEnumerable?)null));
    }

    [Fact]
    public void NotNullOrEmpty_WithEmptyCollection_Fails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NotNullOrEmpty(Array.Empty<int>()));
    }

    // --- string overloads ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_String_Passes(string? value)
    {
        CustomAssert.NullOrEmpty(value);
    }

    [Fact]
    public void NullOrEmpty_String_WithWhitespace_Fails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrEmpty(" "));
    }

    [Fact]
    public void NotNullOrEmpty_String_Passes()
    {
        CustomAssert.NotNullOrEmpty("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotNullOrEmpty_String_Fails(string? value)
    {
        Assert.Throws<FalseException>(() => CustomAssert.NotNullOrEmpty(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhiteSpace_Passes(string? value)
    {
        CustomAssert.NullOrWhiteSpace(value);
    }

    [Fact]
    public void NullOrWhiteSpace_WithContent_Fails()
    {
        Assert.Throws<TrueException>(() => CustomAssert.NullOrWhiteSpace("x"));
    }

    [Fact]
    public void NotNullOrWhiteSpace_Passes()
    {
        CustomAssert.NotNullOrWhiteSpace("x");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_Fails(string? value)
    {
        Assert.Throws<FalseException>(() => CustomAssert.NotNullOrWhiteSpace(value));
    }
}

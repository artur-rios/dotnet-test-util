using System.Collections;
using ArturRios.Extensions;
using Xunit;

namespace ArturRios.Util.Test.Assertion;

/// <summary>
/// Extra xUnit-style assertions for the null/empty checks that come up most often in tests.
/// Each method throws a <c>Xunit.Sdk.XunitException</c> when the condition is not met, exactly like <see cref="Assert"/>.
/// </summary>
public static class CustomAssert
{
    /// <summary>Asserts that <paramref name="collection"/> is <c>null</c> or contains no elements.</summary>
    /// <param name="collection">The collection to check.</param>
    public static void NullOrEmpty(IEnumerable? collection) => Assert.True(collection is null || collection.IsEmpty());

    /// <summary>Asserts that <paramref name="collection"/> is not <c>null</c> and contains at least one element.</summary>
    /// <param name="collection">The collection to check.</param>
    public static void NotNullOrEmpty(IEnumerable? collection) => Assert.True(collection is not null && collection.IsNotEmpty());

    /// <summary>Asserts that <paramref name="string"/> is <c>null</c> or the empty string.</summary>
    /// <param name="string">The string to check.</param>
    public static void NullOrEmpty(string? @string) => Assert.True(string.IsNullOrEmpty(@string));

    /// <summary>Asserts that <paramref name="string"/> is not <c>null</c> and not the empty string.</summary>
    /// <param name="string">The string to check.</param>
    public static void NotNullOrEmpty(string? @string) => Assert.False(string.IsNullOrEmpty(@string));

    /// <summary>Asserts that <paramref name="string"/> is <c>null</c>, empty, or consists only of white-space characters.</summary>
    /// <param name="string">The string to check.</param>
    public static void NullOrWhiteSpace(string? @string) => Assert.True(string.IsNullOrWhiteSpace(@string));

    /// <summary>Asserts that <paramref name="string"/> contains at least one non-white-space character.</summary>
    /// <param name="string">The string to check.</param>
    public static void NotNullOrWhiteSpace(string? @string) => Assert.False(string.IsNullOrWhiteSpace(@string));
}

namespace ArturRios.Util.Test.Attributes;

/// <summary>
/// The kind of test a custom attribute marks. Surfaced as the <c>Category</c> trait so tests can be
/// filtered by type, for example <c>dotnet test --filter "Category=Unit"</c>.
/// </summary>
public enum TestType
{
    /// <summary>A unit test.</summary>
    Unit,

    /// <summary>A functional test.</summary>
    Functional
}

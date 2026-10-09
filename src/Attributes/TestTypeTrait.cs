namespace ArturRios.Util.Test.Attributes;

/// <summary>Builds the <c>Category</c> trait the custom test attributes stamp on a test.</summary>
internal static class TestTypeTrait
{
    public const string Name = "Category";

    public static IReadOnlyCollection<KeyValuePair<string, string>> For(TestType testType) =>
        [new KeyValuePair<string, string>(Name, testType.ToString())];
}

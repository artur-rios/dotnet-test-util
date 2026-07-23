using Xunit.Abstractions;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Attributes;

/// <summary>
/// Trait discoverer that reads the <see cref="CustomFactAttribute.TestType"/> carried by a custom test attribute
/// and exposes it as the <c>Category</c> trait, enabling filters such as <c>dotnet test --filter "Category=Unit"</c>.
/// </summary>
public class TestTypeTraitDiscoverer : ITraitDiscoverer
{
    /// <summary>The trait key under which the test type is published.</summary>
    public const string TraitName = "Category";

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var testType = traitAttribute.GetNamedArgument<TestType>(nameof(CustomFactAttribute.TestType));

        yield return new KeyValuePair<string, string>(TraitName, testType.ToString());
    }
}

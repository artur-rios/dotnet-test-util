using ArturRios.Util.Test.Attributes;
using Xunit.v3;

namespace ArturRios.Util.Test.Tests.Attributes;

[Trait("Category", "Unit")]
public class TestTypeTraitTests
{
    // Nested private class: xUnit does not discover its methods as tests, so the sample
    // attributes below exist purely to be read back through reflection. The xUnit analyzer
    // is static and cannot tell these are not real tests, so its rules are suppressed here.
#pragma warning disable xUnit1000 // Test classes must be public
#pragma warning disable xUnit1003 // Theory methods must have test data
#pragma warning disable xUnit1006 // Theory methods should have parameters
    private class Marked
    {
        [UnitFact]
        public void UnitFact() { }

        [UnitTheory]
        public void UnitTheory() { }

        [FunctionalFact]
        public void FunctionalFact() { }

        [FunctionalTheory]
        public void FunctionalTheory() { }
    }
#pragma warning restore xUnit1006
#pragma warning restore xUnit1003
#pragma warning restore xUnit1000

    [Theory]
    [InlineData(nameof(Marked.UnitFact), TestType.Unit)]
    [InlineData(nameof(Marked.UnitTheory), TestType.Unit)]
    [InlineData(nameof(Marked.FunctionalFact), TestType.Functional)]
    [InlineData(nameof(Marked.FunctionalTheory), TestType.Functional)]
    public void GivenEachCustomAttribute_WhenInspected_ThenItExposesItsTestType(string methodName, TestType expected)
    {
        var attribute = GetTestAttribute(methodName);

        var testType = attribute switch
        {
            CustomFactAttribute fact => fact.TestType,
            CustomTheoryAttribute theory => theory.TestType,
            _ => throw new InvalidOperationException($"{methodName} carries no custom test attribute.")
        };

        Assert.Equal(expected, testType);
    }

    [Theory]
    [InlineData(nameof(Marked.UnitFact), "Unit")]
    [InlineData(nameof(Marked.UnitTheory), "Unit")]
    [InlineData(nameof(Marked.FunctionalFact), "Functional")]
    [InlineData(nameof(Marked.FunctionalTheory), "Functional")]
    public void GivenACustomAttribute_WhenItsTraitsAreRead_ThenACategoryTraitIsYielded(string methodName, string expectedValue)
    {
        var attribute = GetTestAttribute(methodName);

        var trait = Assert.Single(attribute.GetTraits());

        Assert.Equal("Category", trait.Key);
        Assert.Equal(expectedValue, trait.Value);
    }

    // Reads back the custom test attribute applied to Marked.<methodName>. xUnit v3 discovers
    // traits by calling ITraitAttribute.GetTraits() on the attribute itself, so this exercises the
    // same call test discovery makes.
    private static ITraitAttribute GetTestAttribute(string methodName) =>
        typeof(Marked).GetMethod(methodName)!.GetCustomAttributes(inherit: false).OfType<ITraitAttribute>().Single();
}

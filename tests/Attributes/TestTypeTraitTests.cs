using ArturRios.Util.Test.Attributes;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Tests.Attributes;

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
    public void Attribute_ExposesExpectedTestType(string methodName, TestType expected)
    {
        var attribute = GetTestAttribute(methodName);

        var testType = attribute.GetNamedArgument<TestType>(nameof(CustomFactAttribute.TestType));

        Assert.Equal(expected, testType);
    }

    [Theory]
    [InlineData(nameof(Marked.UnitFact), "Unit")]
    [InlineData(nameof(Marked.UnitTheory), "Unit")]
    [InlineData(nameof(Marked.FunctionalFact), "Functional")]
    [InlineData(nameof(Marked.FunctionalTheory), "Functional")]
    public void Discoverer_YieldsCategoryTrait(string methodName, string expectedValue)
    {
        var attribute = GetTestAttribute(methodName);

        var trait = Assert.Single(new TestTypeTraitDiscoverer().GetTraits(attribute));

        Assert.Equal("Category", trait.Key);
        Assert.Equal(expectedValue, trait.Value);
    }

    // Wraps the custom test attribute applied to Marked.<methodName> in xUnit's own reflection
    // shim, so the discoverer is exercised exactly as it is during test discovery.
    private static IAttributeInfo GetTestAttribute(string methodName)
    {
        var method = typeof(Marked).GetMethod(methodName)!;
        var data = method.CustomAttributes.Single(a => typeof(ITraitAttribute).IsAssignableFrom(a.AttributeType));

        return new ReflectionAttributeInfo(data);
    }
}

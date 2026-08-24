using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Util.Test.Tests.Attributes;

// These tests mutate the process-wide ASPNETCORE_ENVIRONMENT variable, so they must not run
// in parallel with each other or with the web API tests (see AssemblyInfo.cs).
[Trait("Category", "Unit")]
public class CustomAttributeTests : IDisposable
{
    private readonly string? _originalEnvironment =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static void SetEnvironment(string? value) =>
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", value);

    public void Dispose() => SetEnvironment(_originalEnvironment);

    [Fact]
    public void GivenAFactWithNoRestrictions_WhenInspected_ThenItIsNotSkipped()
    {
        var attribute = new UnitFactAttribute();

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenAFactWithASkipConditionThatMatches_WhenInspected_ThenItIsSkipped()
    {
        var attribute = new UnitFactAttribute(skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
    }

    [Fact]
    public void GivenAFactWithASkipConditionAndNoEnvironments_WhenInspected_ThenItIsStillSkipped()
    {
        // Regression: skipCondition used to be ignored when no environments were supplied.
        var attribute = new FunctionalFactAttribute(environments: null, skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
    }

    [Fact]
    public void GivenAFactBlockedInTheCurrentEnvironment_WhenInspected_ThenItIsSkipped()
    {
        SetEnvironment("Production");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.Equal("Test can't run on Production", attribute.Skip);
    }

    [Fact]
    public void GivenAFactNotBlockedInTheCurrentEnvironment_WhenInspected_ThenItIsNotSkipped()
    {
        SetEnvironment("Local");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenTheEnvironmentNameInADifferentCase_WhenMatchingABlockedFact_ThenItStillMatches()
    {
        SetEnvironment("production");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.NotNull(attribute.Skip);
    }

    [Fact]
    public void GivenAFactWithAnEmptyEnvironmentList_WhenInspected_ThenItIsNotSkipped()
    {
        SetEnvironment("Production");

        var attribute = new UnitFactAttribute([]);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenATheoryBlockedInTheCurrentEnvironment_WhenInspected_ThenItIsSkipped()
    {
        SetEnvironment("Staging");

        var attribute = new FunctionalTheoryAttribute([EnvironmentType.Staging]);

        Assert.Equal("Test can't run on Staging", attribute.Skip);
    }

    [Fact]
    public void GivenATheoryWithNoRestrictions_WhenInspected_ThenItIsNotSkipped()
    {
        var attribute = new UnitTheoryAttribute();

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenAnAttributeScopedToEnvironments_WhenInspected_ThenThoseEnvironmentsAreExposed()
    {
        var environments = new[] { EnvironmentType.Local, EnvironmentType.Development };

        var attribute = new UnitFactAttribute(environments);

        Assert.Equal(environments, attribute.Environments);
    }
}

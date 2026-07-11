using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Util.Test.Tests.Attributes;

// These tests mutate the process-wide ASPNETCORE_ENVIRONMENT variable, so they must not run
// in parallel with each other or with the web API tests (see AssemblyInfo.cs).
public class CustomAttributeTests : IDisposable
{
    private readonly string? _originalEnvironment =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static void SetEnvironment(string? value) =>
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", value);

    public void Dispose() => SetEnvironment(_originalEnvironment);

    [Fact]
    public void Fact_WithNoRestrictions_IsNotSkipped()
    {
        var attribute = new UnitFactAttribute();

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Fact_WithSkipConditionTrue_IsSkipped()
    {
        var attribute = new UnitFactAttribute(skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
    }

    [Fact]
    public void Fact_WithSkipConditionTrueAndNoEnvironments_IsStillSkipped()
    {
        // Regression: skipCondition used to be ignored when no environments were supplied.
        var attribute = new FunctionalFactAttribute(environments: null, skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
    }

    [Fact]
    public void Fact_WhenCurrentEnvironmentIsBlocked_IsSkipped()
    {
        SetEnvironment("Production");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.Equal("Test can't run on Production", attribute.Skip);
    }

    [Fact]
    public void Fact_WhenCurrentEnvironmentIsNotBlocked_IsNotSkipped()
    {
        SetEnvironment("Local");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Fact_BlockedMatchIsCaseInsensitive()
    {
        SetEnvironment("production");

        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        Assert.NotNull(attribute.Skip);
    }

    [Fact]
    public void Fact_WithEmptyEnvironments_IsNotSkipped()
    {
        SetEnvironment("Production");

        var attribute = new UnitFactAttribute([]);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Theory_WhenCurrentEnvironmentIsBlocked_IsSkipped()
    {
        SetEnvironment("Staging");

        var attribute = new FunctionalTheoryAttribute([EnvironmentType.Staging]);

        Assert.Equal("Test can't run on Staging", attribute.Skip);
    }

    [Fact]
    public void Theory_WithNoRestrictions_IsNotSkipped()
    {
        var attribute = new UnitTheoryAttribute();

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void Environments_PropertyIsExposed()
    {
        var environments = new[] { EnvironmentType.Local, EnvironmentType.Development };

        var attribute = new UnitFactAttribute(environments);

        Assert.Equal(environments, attribute.Environments);
    }
}

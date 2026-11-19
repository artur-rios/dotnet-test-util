using System.Runtime.CompilerServices;
using ArturRios.Configuration.Enums;
using ArturRios.Util.Test.Attributes;

namespace ArturRios.Util.Test.Tests.Attributes;

// These tests mutate the process-wide ASPNETCORE_ENVIRONMENT variable, so they share a collection with the web API
// tests, which set it too (see EnvironmentVariableCollection).
[Trait("Category", "Unit")]
[Collection(EnvironmentVariableCollection.Name)]
public class CustomAttributeTests : IDisposable
{
    private readonly string? _originalEnvironment =
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    private static void SetEnvironment(string? value) =>
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", value);

    public void Dispose() => SetEnvironment(_originalEnvironment);



    [Fact]
    public void GivenAFactWithASkipConditionAndNoEnvironments_WhenInspected_ThenItIsStillSkipped()
    {
        // Regression: skipCondition used to be ignored when no environments were supplied.
        var attribute = new FunctionalFactAttribute(environments: null, skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
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
    public void GivenAnAttributeScopedToEnvironments_WhenInspected_ThenThoseEnvironmentsAreExposed()
    {
        var environments = new[] { EnvironmentType.Local, EnvironmentType.Development };

        var attribute = new UnitFactAttribute(environments);

        Assert.Equal(environments, attribute.Environments);
    }

    /// <summary>Each of the four public attributes, by name, built with the given arguments.</summary>
    public static TheoryData<string> AttributeKinds => ["UnitFact", "UnitTheory", "FunctionalFact", "FunctionalTheory"];

    private static (string? Skip, EnvironmentType[]? Environments) Build(string kind, EnvironmentType[]? environments = null,
        bool skipCondition = false) => kind switch
        {
            "UnitFact" => Read(new UnitFactAttribute(environments, skipCondition)),
            "UnitTheory" => Read(new UnitTheoryAttribute(environments, skipCondition)),
            "FunctionalFact" => Read(new FunctionalFactAttribute(environments, skipCondition)),
            "FunctionalTheory" => Read(new FunctionalTheoryAttribute(environments, skipCondition)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static (string? Skip, EnvironmentType[]? Environments) Read(CustomFactAttribute attribute) =>
        (attribute.Skip, attribute.Environments);

    private static (string? Skip, EnvironmentType[]? Environments) Read(CustomTheoryAttribute attribute) =>
        (attribute.Skip, attribute.Environments);

    private static int CurrentLine([CallerLineNumber] int line = 0) => line;

    [Theory]
    [MemberData(nameof(AttributeKinds))]
    public void GivenEachAttributeWithASkipCondition_WhenInspected_ThenItIsSkipped(string kind)
    {
        var (skip, _) = Build(kind, skipCondition: true);

        Assert.Equal("Condition to skip matched", skip);
    }

    [Theory]
    [MemberData(nameof(AttributeKinds))]
    public void GivenEachAttributeBlockedInTheCurrentEnvironment_WhenInspected_ThenItIsSkipped(string kind)
    {
        SetEnvironment("Development");

        var (skip, _) = Build(kind, [EnvironmentType.Development]);

        Assert.Equal("Test can't run on Development", skip);
    }

    [Theory]
    [MemberData(nameof(AttributeKinds))]
    public void GivenEachAttributeWithNoArguments_WhenInspected_ThenItRunsAndHasNoEnvironments(string kind)
    {
        SetEnvironment("Production");

        var (skip, environments) = Build(kind);

        Assert.Null(skip);
        Assert.Null(environments);
    }

    [Fact]
    public void GivenASkipConditionAndABlockedEnvironment_WhenInspected_ThenTheConditionIsTheReason()
    {
        SetEnvironment("Production");

        var attribute = new UnitFactAttribute([EnvironmentType.Production], skipCondition: true);

        Assert.Equal("Condition to skip matched", attribute.Skip);
    }

    [Fact]
    public void GivenSeveralBlockedEnvironments_WhenTheCurrentOneIsAmongThem_ThenTheReasonNamesItAsTheVariableSpellsIt()
    {
        SetEnvironment("sTaGiNg");

        var attribute = new FunctionalFactAttribute([EnvironmentType.Local, EnvironmentType.Staging]);

        Assert.Equal("Test can't run on sTaGiNg", attribute.Skip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("QA")] // a name outside EnvironmentType
    [InlineData("Production ")] // not trimmed
    public void GivenAnEnvironmentVariableThatNamesNoBlockedEnvironment_WhenInspected_ThenItIsNotSkipped(string? value)
    {
        SetEnvironment(value);

        var attribute = new UnitFactAttribute(
            [EnvironmentType.Local, EnvironmentType.Development, EnvironmentType.Staging, EnvironmentType.Production]);

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenAnEnvironmentThatIsBlockedLater_WhenTheAttributeWasBuiltBefore_ThenItKeepsTheReasonItWasBuiltWith()
    {
        SetEnvironment("Local");
        var attribute = new UnitFactAttribute([EnvironmentType.Production]);

        SetEnvironment("Production");

        Assert.Null(attribute.Skip);
    }

    [Fact]
    public void GivenEachAttribute_WhenBuilt_ThenItReportsTheCallersFileAndLineAsTheTestsSourceLocation()
    {
        var (unitFact, unitFactLine) = (new UnitFactAttribute(), CurrentLine());
        var (unitTheory, unitTheoryLine) = (new UnitTheoryAttribute(), CurrentLine());
        var (functionalFact, functionalFactLine) = (new FunctionalFactAttribute(), CurrentLine());
        var (functionalTheory, functionalTheoryLine) = (new FunctionalTheoryAttribute(), CurrentLine());

        Assert.Equal(unitFactLine, unitFact.SourceLineNumber);
        Assert.Equal(unitTheoryLine, unitTheory.SourceLineNumber);
        Assert.Equal(functionalFactLine, functionalFact.SourceLineNumber);
        Assert.Equal(functionalTheoryLine, functionalTheory.SourceLineNumber);
        Assert.All([unitFact.SourceFilePath, unitTheory.SourceFilePath, functionalFact.SourceFilePath,
            functionalTheory.SourceFilePath], path => Assert.Equal("CustomAttributeTests.cs", Path.GetFileName(path)));
    }
}

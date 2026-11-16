using ArturRios.Util.Test.Attributes;

namespace ArturRios.Util.Test.Tests.Attributes;

/// <summary>
/// Real tests marked with the package's own attributes, and no class-level trait: each reads back, from xUnit's
/// running test, the <c>Category</c> trait its attribute stamped. <see cref="TestTypeTraitTests"/> reads the traits
/// through reflection; these show that test discovery gets them too, so <c>--filter "Category=Unit"</c> and the
/// <c>test</c> check's category gate see them.
/// </summary>
public class CategoryTraitDiscoveryTests
{
    private static IReadOnlyCollection<string> Categories() => TestContext.Current.Test!.Traits["Category"];

    [UnitFact]
    public void GivenAUnitFact_WhenXunitRunsIt_ThenTheTestCarriesTheUnitCategoryOnly() =>
        Assert.Equal(["Unit"], Categories());

    [UnitTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void GivenAUnitTheory_WhenXunitRunsEachRow_ThenEachCarriesTheUnitCategoryOnly(int row)
    {
        Assert.InRange(row, 1, 2);
        Assert.Equal(["Unit"], Categories());
    }

    [FunctionalFact]
    public void GivenAFunctionalFact_WhenXunitRunsIt_ThenTheTestCarriesTheFunctionalCategoryOnly() =>
        Assert.Equal(["Functional"], Categories());

    [FunctionalTheory]
    [InlineData("row")]
    public void GivenAFunctionalTheory_WhenXunitRunsARow_ThenItCarriesTheFunctionalCategoryOnly(string row)
    {
        Assert.Equal("row", row);
        Assert.Equal(["Functional"], Categories());
    }
}

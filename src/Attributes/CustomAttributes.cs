using ArturRios.Configuration.Enums;

namespace ArturRios.Util.Test.Attributes;

/// <summary>Marks a unit test fact that can be scoped to specific environments. Stamps the <c>Category=Unit</c> trait.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class UnitFactAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomFactAttribute(TestType.Unit, environments, skipCondition);

/// <summary>Marks a unit test theory that can be scoped to specific environments. Stamps the <c>Category=Unit</c> trait.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class UnitTheoryAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomTheoryAttribute(TestType.Unit, environments, skipCondition);

/// <summary>Marks a functional test fact that can be scoped to specific environments. Stamps the <c>Category=Functional</c> trait.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class FunctionalFactAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomFactAttribute(TestType.Functional, environments, skipCondition);

/// <summary>Marks a functional test theory that can be scoped to specific environments. Stamps the <c>Category=Functional</c> trait.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class FunctionalTheoryAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomTheoryAttribute(TestType.Functional, environments, skipCondition);

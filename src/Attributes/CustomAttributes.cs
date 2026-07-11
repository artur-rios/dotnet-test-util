using ArturRios.Configuration.Enums;

namespace ArturRios.Util.Test.Attributes;

/// <summary>Marks a unit test fact that can be scoped to specific environments.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class UnitFactAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomFactAttribute(environments, skipCondition);

/// <summary>Marks a unit test theory that can be scoped to specific environments.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class UnitTheoryAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomTheoryAttribute(environments, skipCondition);

/// <summary>Marks a functional test fact that can be scoped to specific environments.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class FunctionalFactAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomFactAttribute(environments, skipCondition);

/// <summary>Marks a functional test theory that can be scoped to specific environments.</summary>
/// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
/// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
public class FunctionalTheoryAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    : CustomTheoryAttribute(environments, skipCondition);

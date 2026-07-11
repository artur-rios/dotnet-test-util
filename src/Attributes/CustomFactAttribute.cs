using ArturRios.Configuration.Enums;
using Xunit;

namespace ArturRios.Util.Test.Attributes;

/// <summary>
/// Base <see cref="FactAttribute"/> that can skip a test based on the active <c>ASPNETCORE_ENVIRONMENT</c>
/// or an explicit condition. Derive from it to create environment-aware fact attributes.
/// </summary>
public class CustomFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute and applies the skip reason, if any.</summary>
    /// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
    /// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
    protected CustomFactAttribute(EnvironmentType[]? environments = null, bool skipCondition = false)
    {
        Environments = environments;

        var reason = EnvironmentSkip.GetReason(environments, skipCondition);

        if (reason is not null)
        {
            Skip = reason;
        }
    }

    /// <summary>The environments in which the test must not run, or <c>null</c> when unrestricted.</summary>
    public EnvironmentType[]? Environments { get; }
}

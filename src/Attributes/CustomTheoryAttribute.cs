using System.Runtime.CompilerServices;
using ArturRios.Configuration.Enums;
using Xunit;
using Xunit.v3;

namespace ArturRios.Util.Test.Attributes;

/// <summary>
/// Base <see cref="TheoryAttribute"/> that can skip a test based on the active <c>ASPNETCORE_ENVIRONMENT</c>
/// or an explicit condition, and stamps its <see cref="TestType"/> as the <c>Category</c> trait.
/// Derive from it to create environment-aware theory attributes filterable by test type.
/// </summary>
public class CustomTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    /// <summary>Creates the attribute and applies the skip reason, if any.</summary>
    /// <param name="testType">The kind of test this attribute marks, published as the <c>Category</c> trait.</param>
    /// <param name="environments">Environments in which the test must not run. <c>null</c> imposes no restriction.</param>
    /// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
    /// <param name="sourceFilePath">Supplied by the compiler; xUnit reports it as the test's source location.</param>
    /// <param name="sourceLineNumber">Supplied by the compiler; xUnit reports it as the test's source location.</param>
    protected CustomTheoryAttribute(TestType testType, EnvironmentType[]? environments = null, bool skipCondition = false,
        [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        TestType = testType;
        Environments = environments;

        var reason = EnvironmentSkip.GetReason(environments, skipCondition);

        if (reason is not null)
        {
            Skip = reason;
        }
    }

    /// <summary>The kind of test this attribute marks, published as the <c>Category</c> trait.</summary>
    public TestType TestType { get; }

    /// <summary>The environments in which the test must not run, or <c>null</c> when unrestricted.</summary>
    public EnvironmentType[]? Environments { get; }

    /// <summary>Publishes <see cref="TestType"/> as the <c>Category</c> trait.</summary>
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => TestTypeTrait.For(TestType);
}

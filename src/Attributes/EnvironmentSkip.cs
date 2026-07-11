using ArturRios.Configuration.Enums;

namespace ArturRios.Util.Test.Attributes;

/// <summary>
/// Shared logic that decides whether a custom test attribute should skip the current test,
/// based on the active <c>ASPNETCORE_ENVIRONMENT</c> and an optional manual condition.
/// </summary>
internal static class EnvironmentSkip
{
    /// <summary>
    /// Computes the skip reason for a test.
    /// </summary>
    /// <param name="environments">
    /// Environments in which the test must <b>not</b> run. When the current environment is one of these,
    /// the test is skipped. <c>null</c> means the test is not restricted by environment.
    /// </param>
    /// <param name="skipCondition">When <c>true</c>, the test is skipped regardless of the environment.</param>
    /// <returns>The skip reason, or <c>null</c> when the test should run.</returns>
    public static string? GetReason(EnvironmentType[]? environments, bool skipCondition)
    {
        if (skipCondition)
        {
            return "Condition to skip matched";
        }

        if (environments is null || environments.Length == 0)
        {
            return null;
        }

        var currentEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        var blocked = environments.Any(x => x.ToString().Equals(currentEnvironment, StringComparison.OrdinalIgnoreCase));

        return blocked ? $"Test can't run on {currentEnvironment}" : null;
    }
}

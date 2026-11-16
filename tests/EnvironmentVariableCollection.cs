namespace ArturRios.Util.Test.Tests;

/// <summary>
/// The test classes that set the process-wide <c>ASPNETCORE_ENVIRONMENT</c> variable: the custom attribute tests read
/// it, and every <see cref="Util.Test.Functional.WebApiTest{T}"/> or <see cref="Util.Test.Functional.WebApiHost{TProgram}"/>
/// host sets it. Sharing one collection runs them one at
/// a time, so they cannot race each other over the variable, while the rest of the suite, which never reads it, runs
/// in parallel with them.
/// </summary>
[CollectionDefinition(Name)]
public sealed class EnvironmentVariableCollection
{
    /// <summary>The collection's name, for <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "ASPNETCORE_ENVIRONMENT";
}

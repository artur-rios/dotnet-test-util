namespace ArturRios.Util.Test.Functional;

/// <summary>
/// Sets <c>ASPNETCORE_ENVIRONMENT</c> for as long as at least one host that set it is alive, and puts back the value
/// it had before the first of them once the last is released. Counted, not restored per host: two hosts of one
/// environment may run at the same time, and the first released must not take the variable from the other.
/// </summary>
internal static class EnvironmentVariableLease
{
    internal const string Name = "ASPNETCORE_ENVIRONMENT";

    private static readonly Lock Gate = new();
    private static int _holders;
    private static string? _before;

    /// <summary>Sets the variable to <paramref name="environmentName"/> and takes a lease on it.</summary>
    public static void Acquire(string environmentName)
    {
        lock (Gate)
        {
            if (_holders++ == 0)
            {
                _before = Environment.GetEnvironmentVariable(Name);
            }

            Environment.SetEnvironmentVariable(Name, environmentName);
        }
    }

    /// <summary>Gives a lease back; the last one puts back the value the variable had before the first.</summary>
    public static void Release()
    {
        lock (Gate)
        {
            if (_holders == 0 || --_holders > 0)
            {
                return;
            }

            Environment.SetEnvironmentVariable(Name, _before);
            _before = null;
        }
    }
}

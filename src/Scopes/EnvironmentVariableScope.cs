namespace ArturRios.Util.Test.Scopes;

/// <summary>
/// Sets and clears process environment variables for as long as it lives, and on <see cref="Dispose"/> puts back
/// what each one held before the scope first touched it: its value, or no value at all. Use it with <c>using</c>, so
/// the variables are restored when the test ends, whether it passes, fails or throws.
/// </summary>
/// <remarks>
/// <para>
/// Environment variables belong to the process, not to the test: every test running at the same time sees the
/// change. A test using a scope must not run in parallel with a test that reads or sets the same variables, so put
/// every such test class in one xUnit collection
/// (<c>[CollectionDefinition("Environment")]</c> on a class, <c>[Collection("Environment")]</c> on each test class),
/// whose tests run one at a time.
/// </para>
/// <para>
/// <code>
/// using var environment = new EnvironmentVariableScope(("MAILGUN_API_KEY", "key"), ("MAILGUN_DOMAIN", null));
/// environment.Set("MAILGUN_FROM", "noreply@example.com");
/// </code>
/// </para>
/// </remarks>
public sealed class EnvironmentVariableScope : IDisposable
{
    // Each variable's value before this scope first changed it, in the order the scope first changed them.
    private readonly List<KeyValuePair<string, string?>> _previous = [];
    private readonly HashSet<string> _touched = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <summary>A scope that has changed nothing yet; change variables with <see cref="Set"/> and <see cref="Unset"/>.</summary>
    public EnvironmentVariableScope()
    {
    }

    /// <summary>A scope that sets each of <paramref name="variables"/>; a <c>null</c> value clears the variable.</summary>
    /// <param name="variables">The variables' names and the values to give them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variables"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A name is empty, or not one a variable can have.</exception>
    /// <remarks>When one of the variables cannot be set, those already set are restored before the exception is thrown.</remarks>
    public EnvironmentVariableScope(params (string Name, string? Value)[] variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        try
        {
            foreach (var (name, value) in variables)
            {
                Set(name, value);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/> until the scope is disposed.</summary>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">The value to give it; <c>null</c> or an empty string clears it.</param>
    /// <returns>This scope, to chain calls.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <c>null</c>, empty, or not one a variable can have.</exception>
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    public EnvironmentVariableScope Set(string name, string? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var previous = Environment.GetEnvironmentVariable(name);

        Environment.SetEnvironmentVariable(name, value);

        if (_touched.Add(name))
        {
            _previous.Add(new KeyValuePair<string, string?>(name, previous));
        }

        return this;
    }

    /// <summary>Clears each of <paramref name="names"/> until the scope is disposed.</summary>
    /// <param name="names">The variables to clear.</param>
    /// <returns>This scope, to chain calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A name is <c>null</c>, empty, or not one a variable can have.</exception>
    /// <exception cref="ObjectDisposedException">The scope has been disposed.</exception>
    public EnvironmentVariableScope Unset(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        foreach (var name in names)
        {
            Set(name, null);
        }

        return this;
    }

    /// <summary>
    /// Puts back what each variable the scope changed held before: its previous value, or no value when it had none.
    /// Disposing again does nothing.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        for (var index = _previous.Count - 1; index >= 0; index--)
        {
            Environment.SetEnvironmentVariable(_previous[index].Key, _previous[index].Value);
        }
    }
}

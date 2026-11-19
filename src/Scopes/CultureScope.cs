using System.Globalization;

namespace ArturRios.Util.Test.Scopes;

/// <summary>
/// Sets <see cref="CultureInfo.CurrentCulture"/> and <see cref="CultureInfo.CurrentUICulture"/> for as long as it
/// lives, and puts the previous ones back on <see cref="Dispose"/>, so a culture-sensitive assertion does not depend
/// on the machine it runs on. Use it with <c>using</c>, so the cultures are restored also when the test throws.
/// </summary>
/// <remarks>
/// <para>
/// The current cultures flow with the execution context, not the process: a scope changes them for the test that
/// created it and for the work that test starts (awaits, tasks, a <see cref="Functional.WebApiHost{TProgram}"/> it
/// starts), while tests running in parallel keep theirs. Create it in the test itself, not in an async helper
/// method: a culture set inside an async method is undone when that method returns.
/// </para>
/// <para>
/// Cultures are looked up with <see cref="CultureInfo.GetCultureInfo(string)"/>, which ignores the machine user's
/// regional overrides, so <c>"pt-BR"</c> formats the same on every machine. An empty name is the invariant culture.
/// </para>
/// </remarks>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private bool _disposed;

    /// <summary>Sets both the current culture and the current UI culture to the culture named <paramref name="name"/>.</summary>
    /// <param name="name">The culture's name, such as <c>"pt-BR"</c>; an empty name is the invariant culture.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
    /// <exception cref="CultureNotFoundException">No culture has that name.</exception>
    public CultureScope(string name) : this(name, name)
    {
    }

    /// <summary>Sets the current culture to <paramref name="culture"/> and the current UI culture to <paramref name="uiCulture"/>.</summary>
    /// <param name="culture">The name of the culture for formatting and parsing.</param>
    /// <param name="uiCulture">The name of the culture for resources (translated messages).</param>
    /// <exception cref="ArgumentNullException">A name is <c>null</c>.</exception>
    /// <exception cref="CultureNotFoundException">No culture has one of the names.</exception>
    public CultureScope(string culture, string uiCulture)
        : this(CultureInfo.GetCultureInfo(culture), CultureInfo.GetCultureInfo(uiCulture))
    {
    }

    /// <summary>Sets the current culture to <paramref name="culture"/> and the current UI culture to <paramref name="uiCulture"/>.</summary>
    /// <param name="culture">The culture for formatting and parsing.</param>
    /// <param name="uiCulture">The culture for resources; <c>null</c> uses <paramref name="culture"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <c>null</c>.</exception>
    public CultureScope(CultureInfo culture, CultureInfo? uiCulture = null)
    {
        ArgumentNullException.ThrowIfNull(culture);

        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;

        Culture = culture;
        UICulture = uiCulture ?? culture;

        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = UICulture;
    }

    /// <summary>The current culture the scope set.</summary>
    public CultureInfo Culture { get; }

    /// <summary>The current UI culture the scope set.</summary>
    public CultureInfo UICulture { get; }

    /// <summary>Puts back the current culture and UI culture the scope found. Disposing again does nothing.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}

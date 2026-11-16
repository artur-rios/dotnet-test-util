using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> whose loggers keep every entry written to them, of every category, in one list.
/// Add it to a host to see what the API logs: through <see cref="Functional.WebApiHostOptions.LoggerProviders"/>,
/// or <c>builder.ConfigureLogging(logging =&gt; logging.AddProvider(provider))</c>. Assert on <see cref="Entries"/>
/// or <see cref="EntriesOf{T}"/>, for example with <see cref="LogAssert"/>.
/// </summary>
/// <remarks>
/// It honours the logging scopes of the factory it is added to (<see cref="ISupportExternalScope"/>), so entries
/// carry the scopes the API opened. Disposing it keeps its entries, so a host that disposes its providers leaves them
/// readable. It is safe to log to from several threads.
/// </remarks>
public sealed class RecordingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly LogSink _sink = new();

    /// <summary>
    /// The lowest level the provider's loggers keep; entries below it are not enabled and not kept. Defaults to
    /// <see cref="LogLevel.Trace"/>, everything.
    /// </summary>
    public LogLevel MinimumLevel
    {
        get => _sink.MinimumLevel;
        set => _sink.MinimumLevel = value;
    }

    /// <summary>Every entry the provider's loggers kept, of every category, oldest first: a snapshot.</summary>
    public IReadOnlyList<LogEntry> Entries => _sink.Snapshot(null);

    /// <summary>The entries kept under <paramref name="categoryName"/>, oldest first: a snapshot.</summary>
    /// <param name="categoryName">The category, such as <c>"Microsoft.AspNetCore.Hosting.Diagnostics"</c>.</param>
    /// <returns>The category's entries.</returns>
    public IReadOnlyList<LogEntry> EntriesOf(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        return _sink.Snapshot(categoryName);
    }

    /// <summary>The entries kept under the category of an <c>ILogger&lt;T&gt;</c>, oldest first: a snapshot.</summary>
    /// <typeparam name="T">The type whose logger's entries to return.</typeparam>
    /// <returns>The category's entries.</returns>
    public IReadOnlyList<LogEntry> EntriesOf<T>() => _sink.Snapshot(LogSink.CategoryOf<T>());

    /// <summary>Forgets every entry kept, of every category.</summary>
    public void Clear() => _sink.Clear(null);

    /// <summary>Creates a logger of <paramref name="categoryName"/> that keeps its entries in this provider.</summary>
    /// <param name="categoryName">The logger's category.</param>
    /// <returns>A <see cref="RecordingLogger"/>.</returns>
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _sink);

    /// <summary>Creates an <see cref="ILogger{TCategoryName}"/> that keeps its entries in this provider.</summary>
    /// <typeparam name="T">The type whose name is the logger's category.</typeparam>
    /// <returns>A <see cref="RecordingLogger{T}"/>.</returns>
    public RecordingLogger<T> CreateLogger<T>() => new(_sink);

    /// <summary>Uses the scopes of the logger factory the provider was added to.</summary>
    /// <param name="scopeProvider">The factory's scope provider.</param>
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);

        _sink.ScopeProvider = scopeProvider;
    }

    /// <summary>Does nothing: the entries stay readable after the host that used the provider is gone.</summary>
    public void Dispose()
    {
    }
}

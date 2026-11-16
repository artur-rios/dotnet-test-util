using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Logging;

/// <summary>
/// An <see cref="ILogger"/> that keeps every entry written to it: level, event id, rendered message, exception,
/// state and the scopes open at the time. Pass it where the code under test takes a logger, then assert on
/// <see cref="Entries"/>, for example with <see cref="LogAssert"/>. It is safe to log to from several threads.
/// </summary>
/// <remarks>
/// A logger made with <c>new</c> keeps its own entries. One created by a <see cref="RecordingLoggerProvider"/>
/// shares the provider's: its <see cref="Entries"/> are those of its category, and its <see cref="MinimumLevel"/>
/// is the provider's.
/// </remarks>
public class RecordingLogger : ILogger
{
    private readonly LogSink _sink;

    /// <summary>A logger of category <paramref name="categoryName"/> that keeps its own entries.</summary>
    /// <param name="categoryName">The category to record its entries under; empty by default.</param>
    public RecordingLogger(string categoryName = "") : this(categoryName, new LogSink())
    {
    }

    internal RecordingLogger(string categoryName, LogSink sink)
    {
        ArgumentNullException.ThrowIfNull(categoryName);

        CategoryName = categoryName;
        _sink = sink;
    }

    /// <summary>The category the logger records its entries under.</summary>
    public string CategoryName { get; }

    /// <summary>
    /// The lowest level the logger keeps; entries below it are not enabled and not kept. Defaults to
    /// <see cref="LogLevel.Trace"/>, everything.
    /// </summary>
    public LogLevel MinimumLevel
    {
        get => _sink.MinimumLevel;
        set => _sink.MinimumLevel = value;
    }

    /// <summary>The entries the logger kept, oldest first: a snapshot that later entries do not change.</summary>
    public IReadOnlyList<LogEntry> Entries => _sink.Snapshot(CategoryName);

    /// <summary>Forgets the entries the logger kept.</summary>
    public void Clear() => _sink.Clear(CategoryName);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _sink.ScopeProvider.Push(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _sink.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!IsEnabled(logLevel))
        {
            return;
        }

        _sink.Add(CategoryName, logLevel, eventId, formatter(state, exception), exception, state);
    }
}

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps every entry written to it, under the category an
/// <see cref="ILoggerFactory"/> gives <typeparamref name="T"/>. See <see cref="RecordingLogger"/>.
/// </summary>
/// <typeparam name="T">The type whose name is the logger's category.</typeparam>
public class RecordingLogger<T> : RecordingLogger, ILogger<T>
{
    /// <summary>A logger of <typeparamref name="T"/>'s category that keeps its own entries.</summary>
    public RecordingLogger() : base(LogSink.CategoryOf<T>())
    {
    }

    internal RecordingLogger(LogSink sink) : base(LogSink.CategoryOf<T>(), sink)
    {
    }
}

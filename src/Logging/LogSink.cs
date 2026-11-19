using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Logging;

// Where recording loggers keep their entries: one per standalone logger, one per provider for all its loggers.
internal sealed class LogSink
{
    private readonly List<LogEntry> _entries = [];
    private readonly Lock _gate = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

    public IExternalScopeProvider ScopeProvider { get; set; } = new LoggerExternalScopeProvider();

    public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= MinimumLevel;

    public void Add(string category, LogLevel level, EventId eventId, string message, Exception? exception,
        object? state)
    {
        var scopes = new List<object?>();
        ScopeProvider.ForEachScope((scope, list) => list.Add(scope), scopes);

        var entry = new LogEntry(category, level, eventId, message, exception, state, scopes);

        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<LogEntry> Snapshot(string? category)
    {
        lock (_gate)
        {
            return category is null ? [.. _entries] : [.. _entries.Where(entry => entry.Category == category)];
        }
    }

    public void Clear(string? category)
    {
        lock (_gate)
        {
            _entries.RemoveAll(entry => category is null || entry.Category == category);
        }
    }

    // The category name an ILoggerFactory gives ILogger<T>: asked of a factory, so it is exactly the same.
    public static string CategoryOf<T>() => CategoryCache<T>.Name;

    private static class CategoryCache<T>
    {
        public static readonly string Name = Resolve();

        private static string Resolve()
        {
            var capture = new CategoryCapture();

            // Added rather than passed to the constructor, so the factory disposes it.
            using (var factory = new LoggerFactory())
            {
                factory.AddProvider(capture);
                factory.CreateLogger<T>();
            }

            return capture.Name!;
        }
    }

    private sealed class CategoryCapture : ILoggerProvider
    {
        public string? Name { get; private set; }

        public ILogger CreateLogger(string categoryName)
        {
            Name = categoryName;

            return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        public void Dispose()
        {
        }
    }
}

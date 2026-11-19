using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Logging;

/// <summary>One entry a <see cref="RecordingLogger"/> or a <see cref="RecordingLoggerProvider"/> kept.</summary>
/// <param name="category">The category of the logger it was written to.</param>
/// <param name="level">Its level.</param>
/// <param name="eventId">Its event id.</param>
/// <param name="message">Its message, rendered by the formatter the caller passed.</param>
/// <param name="exception">The exception logged with it, if any.</param>
/// <param name="state">The state the caller logged (for the <c>LogInformation</c>-style extensions and
/// <c>[LoggerMessage]</c> methods, the structured values with the message template).</param>
/// <param name="scopes">The scopes open when it was written, outermost first.</param>
public sealed class LogEntry(
    string category,
    LogLevel level,
    EventId eventId,
    string message,
    Exception? exception,
    object? state,
    IReadOnlyList<object?> scopes)
{
    private const string OriginalFormat = "{OriginalFormat}";

    /// <summary>The category of the logger the entry was written to (for an <c>ILogger&lt;T&gt;</c>, <c>T</c>'s full name).</summary>
    public string Category { get; } = category;

    /// <summary>The entry's level.</summary>
    public LogLevel Level { get; } = level;

    /// <summary>The entry's event id.</summary>
    public EventId EventId { get; } = eventId;

    /// <summary>The entry's message, rendered by the formatter the caller passed.</summary>
    public string Message { get; } = message;

    /// <summary>The exception logged with the entry, if any.</summary>
    public Exception? Exception { get; } = exception;

    /// <summary>The state the caller logged.</summary>
    public object? State { get; } = state;

    /// <summary>The scopes open when the entry was written, outermost first; empty when none was.</summary>
    public IReadOnlyList<object?> Scopes { get; } = scopes;

    /// <summary>
    /// The structured values of <see cref="State"/>, as the <c>LogInformation</c>-style extensions and
    /// <c>[LoggerMessage]</c> methods log them (the message template included, under <c>{OriginalFormat}</c>); empty
    /// when the state is not a list of name-value pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object?>> Properties => State switch
    {
        IReadOnlyList<KeyValuePair<string, object?>> list => list,
        IEnumerable<KeyValuePair<string, object?>> pairs => [.. pairs],
        _ => []
    };

    /// <summary>The message template the entry was written with (<c>"Sent {Count} emails"</c>), if its state has one.</summary>
    public string? MessageTemplate => GetProperty(OriginalFormat) as string;

    /// <summary>The structured value named <paramref name="name"/> in <see cref="Properties"/>, or <c>null</c> when there is none.</summary>
    /// <param name="name">The value's name, as in the message template (<c>"Count"</c> for <c>{Count}</c>).</param>
    /// <returns>The value, or <c>null</c>.</returns>
    public object? GetProperty(string name)
    {
        foreach (var (key, value) in Properties)
        {
            if (key == name)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>The entry as one line: level, category, message and exception type, for assertion messages.</summary>
    /// <returns>The entry's description.</returns>
    public override string ToString() =>
        Exception is null
            ? $"[{Level}] {Category}: {Message}"
            : $"[{Level}] {Category}: {Message} ({Exception.GetType().Name}: {Exception.Message})";
}

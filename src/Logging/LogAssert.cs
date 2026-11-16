using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Logging;

/// <summary>
/// Assertions over the entries a <see cref="RecordingLogger"/> or a <see cref="RecordingLoggerProvider"/> kept.
/// A failure lists every entry, so the message shows what was logged instead.
/// </summary>
public static class LogAssert
{
    /// <summary>Asserts that exactly one entry has <paramref name="level"/>, and returns it.</summary>
    /// <param name="entries">The entries, such as <see cref="RecordingLogger.Entries"/>.</param>
    /// <param name="level">The level.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="FailException">No entry, or more than one, has the level.</exception>
    public static LogEntry Single(IEnumerable<LogEntry> entries, LogLevel level) =>
        Single(entries, entry => entry.Level == level, $"exactly one {level} entry");

    /// <summary>
    /// Asserts that exactly one entry has <paramref name="level"/> and a message containing
    /// <paramref name="messageContains"/> (ordinal), and returns it.
    /// </summary>
    /// <param name="entries">The entries, such as <see cref="RecordingLogger.Entries"/>.</param>
    /// <param name="level">The level.</param>
    /// <param name="messageContains">Text the message contains.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="FailException">No such entry, or more than one, was kept.</exception>
    public static LogEntry Single(IEnumerable<LogEntry> entries, LogLevel level, string messageContains)
    {
        ArgumentNullException.ThrowIfNull(messageContains);

        return Single(entries, entry => Matches(entry, level, messageContains),
            $"exactly one {level} entry containing \"{messageContains}\"");
    }

    /// <summary>
    /// Asserts that at least one entry has <paramref name="level"/> and a message containing
    /// <paramref name="messageContains"/> (ordinal), and returns the first.
    /// </summary>
    /// <param name="entries">The entries, such as <see cref="RecordingLogger.Entries"/>.</param>
    /// <param name="level">The level.</param>
    /// <param name="messageContains">Text the message contains.</param>
    /// <returns>The first such entry.</returns>
    /// <exception cref="FailException">No such entry was kept.</exception>
    public static LogEntry Contains(IEnumerable<LogEntry> entries, LogLevel level, string messageContains)
    {
        ArgumentNullException.ThrowIfNull(messageContains);

        var all = Materialize(entries);

        return all.FirstOrDefault(entry => Matches(entry, level, messageContains))
               ?? Fail<LogEntry>($"a {level} entry containing \"{messageContains}\"", all);
    }

    /// <summary>Asserts that no entry has <paramref name="level"/> or a higher one (for example no error or critical entry).</summary>
    /// <param name="entries">The entries, such as <see cref="RecordingLogger.Entries"/>.</param>
    /// <param name="level">The lowest level no entry may have.</param>
    /// <exception cref="FailException">An entry has the level or a higher one.</exception>
    public static void NoneAtOrAbove(IEnumerable<LogEntry> entries, LogLevel level)
    {
        var all = Materialize(entries);

        if (all.Any(entry => entry.Level >= level))
        {
            Fail<LogEntry>($"no entry at {level} or above", all);
        }
    }

    /// <summary>
    /// Asserts that no entry's message, nor the message of an exception logged with it, contains
    /// <paramref name="text"/> (ordinal): for example that an address or a secret was not logged.
    /// </summary>
    /// <param name="entries">The entries, such as <see cref="RecordingLogger.Entries"/>.</param>
    /// <param name="text">The text no entry may contain.</param>
    /// <exception cref="FailException">An entry contains the text.</exception>
    public static void DoesNotContain(IEnumerable<LogEntry> entries, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        var all = Materialize(entries);

        if (all.Any(entry => entry.Message.Contains(text, StringComparison.Ordinal)
                             || (entry.Exception?.ToString().Contains(text, StringComparison.Ordinal) ?? false)))
        {
            Fail<LogEntry>($"no entry containing \"{text}\"", all);
        }
    }

    private static LogEntry Single(IEnumerable<LogEntry> entries, Func<LogEntry, bool> predicate, string expected)
    {
        var all = Materialize(entries);
        var matches = all.Where(predicate).ToList();

        return matches.Count == 1 ? matches[0] : Fail<LogEntry>($"{expected}, found {matches.Count}", all);
    }

    private static bool Matches(LogEntry entry, LogLevel level, string messageContains) =>
        entry.Level == level && entry.Message.Contains(messageContains, StringComparison.Ordinal);

    private static List<LogEntry> Materialize(IEnumerable<LogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return [.. entries];
    }

    private static T Fail<T>(string expected, IReadOnlyCollection<LogEntry> entries)
    {
        var logged = entries.Count == 0
            ? "nothing was logged"
            : $"logged:{Environment.NewLine}" + string.Join(Environment.NewLine, entries.Select(entry => $"  {entry}"));

        throw FailException.ForFailure($"Expected {expected}; {logged}");
    }
}

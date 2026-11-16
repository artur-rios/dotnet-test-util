using ArturRios.Util.Test.Logging;
using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace ArturRios.Util.Test.Tests.Logging;

[Trait("Category", "Unit")]
public sealed class LogAssertTests
{
    private static IReadOnlyList<LogEntry> Logged(Action<ILogger> log)
    {
        var logger = new RecordingLogger("Orders");

        log(logger);

        return logger.Entries;
    }

    [Fact]
    public void GivenOneEntryAtALevel_WhenAssertingASingleOne_ThenItIsReturned()
    {
        var entries = Logged(logger =>
        {
            logger.LogInformation("started");
            logger.LogWarning("Order 7 is late");
        });

        var entry = LogAssert.Single(entries, LogLevel.Warning);

        Assert.Equal("Order 7 is late", entry.Message);
        Assert.Same(entry, LogAssert.Single(entries, LogLevel.Warning, "is late"));
    }

    [Fact]
    public void GivenTwoEntriesAtALevel_WhenAssertingASingleOne_ThenTheFailureListsWhatWasLogged()
    {
        var entries = Logged(logger =>
        {
            logger.LogWarning("first");
            logger.LogWarning("second");
        });

        var failure = Assert.Throws<FailException>(() => LogAssert.Single(entries, LogLevel.Warning));

        Assert.Contains("exactly one Warning entry, found 2", failure.Message);
        Assert.Contains("[Warning] Orders: second", failure.Message);
    }

    [Fact]
    public void GivenNothingLogged_WhenAssertingASingleEntry_ThenTheFailureSaysSo()
    {
        var failure = Assert.Throws<FailException>(() => LogAssert.Single([], LogLevel.Error, "boom"));

        Assert.Contains("exactly one Error entry containing \"boom\", found 0", failure.Message);
        Assert.Contains("nothing was logged", failure.Message);
    }

    [Fact]
    public void GivenSeveralMatchingEntries_WhenAssertingThatOneIsThere_ThenTheFirstIsReturned()
    {
        var entries = Logged(logger =>
        {
            logger.LogInformation("Sent 1 email");
            logger.LogInformation("Sent 2 emails");
        });

        Assert.Equal("Sent 1 email", LogAssert.Contains(entries, LogLevel.Information, "Sent").Message);
    }

    [Fact]
    public void GivenNoMatchingEntry_WhenAssertingThatOneIsThere_ThenItFails()
    {
        var entries = Logged(logger => logger.LogInformation("Sent 1 email"));

        var failure = Assert.Throws<FailException>(() => LogAssert.Contains(entries, LogLevel.Warning, "Sent"));

        Assert.Contains("a Warning entry containing \"Sent\"", failure.Message);
    }

    [Fact]
    public void GivenEntriesBelowALevel_WhenAssertingNoneAtOrAboveIt_ThenItPasses()
    {
        var entries = Logged(logger => logger.LogWarning("late"));

        LogAssert.NoneAtOrAbove(entries, LogLevel.Error);
    }

    [Fact]
    public void GivenAnEntryAtALevel_WhenAssertingNoneAtOrAboveIt_ThenItFails()
    {
        var entries = Logged(logger => logger.LogCritical("down"));

        var failure = Assert.Throws<FailException>(() => LogAssert.NoneAtOrAbove(entries, LogLevel.Error));

        Assert.Contains("no entry at Error or above", failure.Message);
    }

    [Fact]
    public void GivenEntriesWithoutAText_WhenAssertingItWasNotLogged_ThenItPasses()
    {
        var entries = Logged(logger => logger.LogError(new InvalidOperationException("timeout"), "Request failed"));

        LogAssert.DoesNotContain(entries, "203.0.113.7");
    }

    [Theory]
    [InlineData("Request from 203.0.113.7 failed", "timeout")]
    [InlineData("Request failed", "timeout calling 203.0.113.7")]
    public void GivenATextInAMessageOrAnException_WhenAssertingItWasNotLogged_ThenItFails(string message,
        string exceptionMessage)
    {
        var entries = Logged(logger => logger.LogError(new InvalidOperationException(exceptionMessage), message));

        var failure = Assert.Throws<FailException>(() => LogAssert.DoesNotContain(entries, "203.0.113.7"));

        Assert.Contains("no entry containing \"203.0.113.7\"", failure.Message);
    }

    [Fact]
    public void GivenMissingArguments_WhenAsserting_ThenTheyAreRefused()
    {
        Assert.Throws<ArgumentNullException>(() => LogAssert.Single(null!, LogLevel.Error));
        Assert.Throws<ArgumentNullException>(() => LogAssert.Single([], LogLevel.Error, null!));
        Assert.Throws<ArgumentNullException>(() => LogAssert.Contains([], LogLevel.Error, null!));
        Assert.ThrowsAny<ArgumentException>(() => LogAssert.DoesNotContain([], ""));
    }
}

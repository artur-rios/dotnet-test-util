using ArturRios.Util.Test.Logging;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Tests.Logging;

[Trait("Category", "Unit")]
public sealed class RecordingLoggerTests
{
    private sealed class Outer
    {
        public sealed class Nested;
    }

    private sealed class Generic<T>;

    // A state that lists name-value pairs without being a list.
    private sealed class PairState : IEnumerable<KeyValuePair<string, object?>>
    {
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("Name", "value");
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void GivenAMessageWithAnEventAndAnException_WhenLogged_ThenTheEntryKeepsThemAll()
    {
        var logger = new RecordingLogger("Orders");
        var failure = new InvalidOperationException("boom");

        logger.LogError(new EventId(42, "Failed"), failure, "Order {OrderId} failed", 17);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Orders", entry.Category);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(new EventId(42, "Failed"), entry.EventId);
        Assert.Equal("Order 17 failed", entry.Message);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("Order {OrderId} failed", entry.MessageTemplate);
        Assert.Equal(17, entry.GetProperty("OrderId"));
        Assert.Null(entry.GetProperty("Missing"));
        Assert.Contains(entry.Properties, pair => pair.Key == "OrderId");
        Assert.Empty(entry.Scopes);
    }

    [Fact]
    public void GivenStatesOfEveryShape_WhenLogged_ThenTheirPropertiesAreReadWhereThereAreAny()
    {
        var logger = new RecordingLogger();

        logger.Log(LogLevel.Information, default, new PairState(), null, (_, _) => "pairs");
        logger.Log(LogLevel.Information, default, "plain", null, (state, _) => state);

        Assert.Equal("value", logger.Entries[0].GetProperty("Name"));
        Assert.Null(logger.Entries[0].MessageTemplate);
        Assert.Empty(logger.Entries[1].Properties);
        Assert.Equal("plain", logger.Entries[1].State);
        Assert.Equal(string.Empty, logger.CategoryName);
    }

    [Fact]
    public void GivenOpenScopes_WhenAMessageIsLogged_ThenTheEntryKeepsThemOutermostFirstUntilTheyAreClosed()
    {
        var logger = new RecordingLogger();

        using (logger.BeginScope("outer"))
        using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = "r1" }))
        {
            logger.LogInformation("inside");
        }

        logger.LogInformation("outside");

        Assert.Equal("outer", logger.Entries[0].Scopes[0]);
        Assert.IsType<Dictionary<string, object>>(logger.Entries[0].Scopes[1]);
        Assert.Empty(logger.Entries[1].Scopes);
    }

    [Fact]
    public void GivenAMinimumLevel_WhenMessagesAreLogged_ThenOnlyThoseAtOrAboveItAreEnabledAndKept()
    {
        var logger = new RecordingLogger { MinimumLevel = LogLevel.Warning };

        logger.LogInformation("dropped");
        logger.LogWarning("kept");

        Assert.Equal(LogLevel.Warning, logger.MinimumLevel);
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.None));
        Assert.Equal("kept", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public void GivenKeptEntries_WhenTheLoggerIsCleared_ThenItKeepsNothingAndEarlierSnapshotsStayAsTheyWere()
    {
        var logger = new RecordingLogger();
        logger.LogInformation("first");
        var snapshot = logger.Entries;

        logger.Clear();
        logger.LogInformation("second");

        Assert.Equal("first", Assert.Single(snapshot).Message);
        Assert.Equal("second", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public void GivenATypedLogger_WhenCreated_ThenItsCategoryIsTheOneALoggerFactoryGivesTheType()
    {
        var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));

        factory.CreateLogger<Outer.Nested>().LogInformation("nested");
        factory.CreateLogger<Generic<int>>().LogInformation("generic");

        Assert.Equal(provider.Entries[0].Category, new RecordingLogger<Outer.Nested>().CategoryName);
        Assert.Equal(provider.Entries[1].Category, new RecordingLogger<Generic<int>>().CategoryName);
    }

    [Fact]
    public void GivenATypedLogger_WhenPassedWhereAnILoggerOfTIsExpected_ThenItRecordsWhatIsLogged()
    {
        ILogger<RecordingLoggerTests> logger = new RecordingLogger<RecordingLoggerTests>();

        logger.LogInformation("typed");

        Assert.Equal("typed", Assert.Single(((RecordingLogger)logger).Entries).Message);
    }

    [Fact]
    public void GivenAnEntry_WhenDescribed_ThenItShowsLevelCategoryMessageAndException()
    {
        var logger = new RecordingLogger("Orders");
        logger.LogWarning("plain");
        logger.LogError(new InvalidOperationException("boom"), "failed");

        Assert.Equal("[Warning] Orders: plain", logger.Entries[0].ToString());
        Assert.Equal("[Error] Orders: failed (InvalidOperationException: boom)", logger.Entries[1].ToString());
    }

    [Fact]
    public void GivenNoFormatterOrCategory_WhenUsed_ThenItIsRefused()
    {
        var logger = new RecordingLogger();

        Assert.Throws<ArgumentNullException>(() => logger.Log(LogLevel.Information, default, "state", null, null!));
        Assert.Throws<ArgumentNullException>(() => new RecordingLogger(null!));
    }
}

using ArturRios.Util.Test.Logging;
using Microsoft.Extensions.Logging;

namespace ArturRios.Util.Test.Tests.Logging;

[Trait("Category", "Unit")]
public sealed class RecordingLoggerProviderTests
{
    private sealed class Orders;

    private sealed class Payments;

    [Fact]
    public void GivenLoggersOfSeveralCategories_WhenTheyLog_ThenTheProviderKeepsEveryEntryAndEachLoggerItsOwn()
    {
        var provider = new RecordingLoggerProvider();
        var orders = provider.CreateLogger<Orders>();
        var payments = provider.CreateLogger("Payments");

        orders.LogInformation("order");
        payments.LogInformation("payment");

        Assert.Equal(["order", "payment"], provider.Entries.Select(entry => entry.Message));
        Assert.Equal("order", Assert.Single(orders.Entries).Message);
        Assert.Equal("order", Assert.Single(provider.EntriesOf<Orders>()).Message);
        Assert.Equal("payment", Assert.Single(provider.EntriesOf("Payments")).Message);
        Assert.Empty(provider.EntriesOf<Payments>());
    }

    [Fact]
    public void GivenLoggersOfSeveralCategories_WhenOneIsCleared_ThenOnlyItsEntriesAreForgotten()
    {
        var provider = new RecordingLoggerProvider();
        var orders = provider.CreateLogger<Orders>();
        provider.CreateLogger("Payments").LogInformation("payment");
        orders.LogInformation("order");

        orders.Clear();

        Assert.Equal("payment", Assert.Single(provider.Entries).Message);
    }

    [Fact]
    public void GivenKeptEntries_WhenTheProviderIsCleared_ThenEveryCategoryIsForgotten()
    {
        var provider = new RecordingLoggerProvider();
        provider.CreateLogger("Orders").LogInformation("order");
        provider.CreateLogger("Payments").LogInformation("payment");

        provider.Clear();

        Assert.Empty(provider.Entries);
    }

    [Fact]
    public void GivenAMinimumLevel_WhenItsLoggersLog_ThenTheySkipWhatIsBelowIt()
    {
        var provider = new RecordingLoggerProvider { MinimumLevel = LogLevel.Error };
        var logger = provider.CreateLogger("Orders");

        logger.LogWarning("dropped");
        logger.LogError("kept");

        Assert.Equal(LogLevel.Error, provider.MinimumLevel);
        Assert.Equal("kept", Assert.Single(provider.Entries).Message);
    }

    [Fact]
    public void GivenAProviderAddedToALoggerFactory_WhenAScopeIsOpenedThroughTheFactory_ThenTheEntryKeepsIt()
    {
        var provider = new RecordingLoggerProvider();
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = factory.CreateLogger<Orders>();

        using (logger.BeginScope("checkout"))
        {
            logger.LogInformation("inside");
        }

        Assert.Equal("checkout", Assert.Single(Assert.Single(provider.EntriesOf<Orders>()).Scopes));
    }

    [Fact]
    public void GivenADisposedProvider_WhenItsEntriesAreRead_ThenTheyAreStillThere()
    {
        var provider = new RecordingLoggerProvider();
        provider.CreateLogger("Orders").LogInformation("order");

        provider.Dispose();

        Assert.Single(provider.Entries);
    }

    [Fact]
    public void GivenMissingArguments_WhenUsed_ThenTheyAreRefused()
    {
        var provider = new RecordingLoggerProvider();

        Assert.Throws<ArgumentNullException>(() => provider.EntriesOf(null!));
        Assert.Throws<ArgumentNullException>(() => provider.SetScopeProvider(null!));
    }
}

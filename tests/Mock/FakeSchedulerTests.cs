using System.Diagnostics;
using ArturRios.Mediator;
using ArturRios.Mediator.Command.Interfaces;
using ArturRios.Mediator.Query.Interfaces;
using ArturRios.Util.Test.Mock;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.Test.Tests.Mock;

[Trait("Category", "Unit")]
public class FakeSchedulerTests
{
    private static CommandQueryMediator BuildMediator(out RecordingCommandHandler commandHandler,
        out RecordingQueryHandler queryHandler)
    {
        commandHandler = new RecordingCommandHandler();
        queryHandler = new RecordingQueryHandler();

        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandlerAsync<PingCommand, PingCommandOutput>>(commandHandler);
        services.AddSingleton<IQueryHandlerAsync<PingQuery, PingQueryOutput>>(queryHandler);

        var provider = services.BuildServiceProvider();

        return new CommandQueryMediator(provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task GivenAScheduledCommand_WhenTheScheduleRuns_ThenTheCommandIsDispatchedThroughTheMediator()
    {
        var mediator = BuildMediator(out var commandHandler, out _);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        var command = new PingCommand { Value = "ping" };

        await scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(command);

        Assert.True(commandHandler.WasCalled);
        Assert.Same(command, commandHandler.Received);
    }

    [Fact]
    public async Task GivenAScheduledQuery_WhenTheScheduleRuns_ThenTheQueryIsDispatchedThroughTheMediator()
    {
        var mediator = BuildMediator(out _, out var queryHandler);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        var query = new PingQuery { Value = "ask" };

        await scheduler.CreateQuerySchedule<PingQuery, PingQueryOutput>(query);

        Assert.True(queryHandler.WasCalled);
        Assert.Same(query, queryHandler.Received);
    }

    [Fact]
    public async Task GivenAConfiguredDelay_WhenTheScheduleStarts_ThenNothingIsDispatchedUntilTheDelayHasElapsed()
    {
        // One second is the smallest delay the scheduler takes: it has no clock to fake.
        var mediator = BuildMediator(out var commandHandler, out _);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 1);
        var stopwatch = Stopwatch.StartNew();

        var schedule = scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand { Value = "x" });

        Assert.False(schedule.IsCompleted);
        Assert.False(commandHandler.WasCalled);

        await schedule;

        Assert.True(commandHandler.WasCalled);
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(900), $"Elapsed was {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task GivenANegativeDelay_WhenACommandScheduleRuns_ThenItThrowsAndDispatchesNothing()
    {
        var mediator = BuildMediator(out var commandHandler, out _);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: -1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand()));

        Assert.False(commandHandler.WasCalled);
    }

    [Fact]
    public async Task GivenANegativeDelay_WhenAQueryScheduleRuns_ThenItThrowsAndDispatchesNothing()
    {
        var mediator = BuildMediator(out _, out var queryHandler);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: -1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => scheduler.CreateQuerySchedule<PingQuery, PingQueryOutput>(new PingQuery()));

        Assert.False(queryHandler.WasCalled);
    }

    [Fact]
    public async Task GivenNoHandlerForTheCommand_WhenTheScheduleRuns_ThenTheMediatorsErrorReachesTheCaller()
    {
        var mediator = new CommandQueryMediator(new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>());
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand()));
    }

    [Fact]
    public async Task GivenNoHandlerForTheQuery_WhenTheScheduleRuns_ThenTheMediatorsErrorReachesTheCaller()
    {
        var mediator = new CommandQueryMediator(new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>());
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scheduler.CreateQuerySchedule<PingQuery, PingQueryOutput>(new PingQuery()));
    }
}

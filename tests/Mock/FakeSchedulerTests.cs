using System.Diagnostics;
using ArturRios.Mediator;
using ArturRios.Mediator.Command.Interfaces;
using ArturRios.Mediator.Query.Interfaces;
using ArturRios.Util.Test.Mock;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

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
        // The system clock, as the constructor without a TimeProvider uses: one second is the smallest delay it takes.
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

    [Fact]
    public async Task GivenAFakeClock_WhenTheDefaultDelayHasNotElapsed_ThenNothingIsDispatchedUntilItHas()
    {
        var mediator = BuildMediator(out var commandHandler, out _);
        var clock = new FakeTimeProvider();
        var scheduler = new FakeScheduler(mediator, clock);

        var schedule = scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand { Value = "x" });
        clock.Advance(TimeSpan.FromSeconds(59));

        Assert.False(schedule.IsCompleted);
        Assert.False(commandHandler.WasCalled);

        clock.Advance(TimeSpan.FromSeconds(1));
        await schedule;

        Assert.True(commandHandler.WasCalled);
    }

    [Fact]
    public async Task GivenAFakeClockAndADelay_WhenTheClockPassesIt_ThenTheQueryIsDispatched()
    {
        var mediator = BuildMediator(out _, out var queryHandler);
        var clock = new FakeTimeProvider();
        var scheduler = new FakeScheduler(mediator, clock, waitTimeInSeconds: 300);
        var query = new PingQuery { Value = "ask" };

        var schedule = scheduler.CreateQuerySchedule<PingQuery, PingQueryOutput>(query);
        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.False(queryHandler.WasCalled);

        clock.Advance(TimeSpan.FromMinutes(1));
        await schedule;

        Assert.Same(query, queryHandler.Received);
    }

    [Fact]
    public void GivenNoClock_WhenTheSchedulerIsCreated_ThenItIsRefused()
    {
        var mediator = BuildMediator(out _, out _);

        Assert.Throws<ArgumentNullException>(() => new FakeScheduler(mediator, (TimeProvider)null!));
    }
}

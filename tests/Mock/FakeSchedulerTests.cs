using ArturRios.Mediator;
using ArturRios.Mediator.Command.Interfaces;
using ArturRios.Mediator.Query.Interfaces;
using ArturRios.Util.Test.Mock;
using Microsoft.Extensions.DependencyInjection;

namespace ArturRios.Util.Test.Tests.Mock;

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
    public async Task CreateCommandSchedule_DispatchesCommandThroughMediator()
    {
        var mediator = BuildMediator(out var commandHandler, out _);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        var command = new PingCommand { Value = "ping" };

        await scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(command);

        Assert.True(commandHandler.WasCalled);
        Assert.Same(command, commandHandler.Received);
    }

    [Fact]
    public async Task CreateQuerySchedule_DispatchesQueryThroughMediator()
    {
        var mediator = BuildMediator(out _, out var queryHandler);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

        var query = new PingQuery { Value = "ask" };

        await scheduler.CreateQuerySchedule<PingQuery, PingQueryOutput>(query);

        Assert.True(queryHandler.WasCalled);
        Assert.Same(query, queryHandler.Received);
    }

    [Fact]
    public async Task CreateCommandSchedule_WaitsForConfiguredDelay()
    {
        var mediator = BuildMediator(out _, out _);
        var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 1);

        var start = DateTime.UtcNow;
        await scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand { Value = "x" });
        var elapsed = DateTime.UtcNow - start;

        Assert.True(elapsed >= TimeSpan.FromMilliseconds(900), $"Elapsed was {elapsed}");
    }
}

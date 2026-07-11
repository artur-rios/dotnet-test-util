using ArturRios.Mediator;
using ArturRios.Mediator.Command;
using ArturRios.Mediator.Query;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// Test helper that simulates a delayed (scheduled) dispatch of a command or query through a
/// <see cref="CommandQueryMediator"/>. Use a small <paramref name="waitTimeInSeconds"/> to keep tests fast.
/// </summary>
/// <param name="mediator">The mediator used to dispatch the command or query.</param>
/// <param name="waitTimeInSeconds">The delay, in seconds, applied before dispatching. Defaults to 60.</param>
public class FakeScheduler(CommandQueryMediator mediator, int waitTimeInSeconds = 60)
{
    /// <summary>Waits for the configured delay and then dispatches <paramref name="command"/>.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TCommandOutput">The command output type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    public async Task CreateCommandSchedule<TCommand, TCommandOutput>(TCommand command)
        where TCommand : BaseCommand
        where TCommandOutput : CommandOutput
    {
        await Task.Delay(TimeSpan.FromSeconds(waitTimeInSeconds));

        await mediator.ExecuteCommandAsync<TCommand, TCommandOutput>(command);
    }

    /// <summary>Waits for the configured delay and then dispatches <paramref name="query"/>.</summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TQueryOutput">The query output type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    public async Task CreateQuerySchedule<TQuery, TQueryOutput>(TQuery query)
        where TQuery : BaseQuery
        where TQueryOutput : QueryOutput
    {
        await Task.Delay(TimeSpan.FromSeconds(waitTimeInSeconds));

        await mediator.ExecuteQueryAsync<TQuery, TQueryOutput>(query);
    }
}

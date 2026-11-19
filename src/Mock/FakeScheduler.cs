using ArturRios.Mediator;
using ArturRios.Mediator.Command;
using ArturRios.Mediator.Query;

namespace ArturRios.Util.Test.Mock;

/// <summary>
/// Test helper that simulates a delayed (scheduled) dispatch of a command or query through a
/// <see cref="CommandQueryMediator"/>. The delay runs on a <see cref="TimeProvider"/>: pass a
/// <c>FakeTimeProvider</c> (<c>Microsoft.Extensions.Time.Testing</c>) and advance it to make the delay elapse
/// without waiting, or use a small delay with the system clock.
/// </summary>
public class FakeScheduler
{
    private readonly CommandQueryMediator _mediator;
    private readonly TimeProvider _timeProvider;
    private readonly int _waitTimeInSeconds;

    /// <summary>Creates a scheduler whose delay runs on the system clock (<see cref="TimeProvider.System"/>).</summary>
    /// <param name="mediator">The mediator used to dispatch the command or query.</param>
    /// <param name="waitTimeInSeconds">The delay, in seconds, applied before dispatching. Defaults to 60.</param>
    public FakeScheduler(CommandQueryMediator mediator, int waitTimeInSeconds = 60)
        : this(mediator, TimeProvider.System, waitTimeInSeconds)
    {
    }

    /// <summary>Creates a scheduler whose delay runs on <paramref name="timeProvider"/>.</summary>
    /// <param name="mediator">The mediator used to dispatch the command or query.</param>
    /// <param name="timeProvider">The clock the delay runs on, for example a <c>FakeTimeProvider</c>.</param>
    /// <param name="waitTimeInSeconds">The delay, in seconds, applied before dispatching. Defaults to 60.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <c>null</c>.</exception>
    public FakeScheduler(CommandQueryMediator mediator, TimeProvider timeProvider, int waitTimeInSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _mediator = mediator;
        _timeProvider = timeProvider;
        _waitTimeInSeconds = waitTimeInSeconds;
    }

    /// <summary>Waits for the configured delay and then dispatches <paramref name="command"/>.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TCommandOutput">The command output type.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    public async Task CreateCommandSchedule<TCommand, TCommandOutput>(TCommand command)
        where TCommand : BaseCommand
        where TCommandOutput : CommandOutput
    {
        await Task.Delay(TimeSpan.FromSeconds(_waitTimeInSeconds), _timeProvider);

        await _mediator.ExecuteCommandAsync<TCommand, TCommandOutput>(command);
    }

    /// <summary>Waits for the configured delay and then dispatches <paramref name="query"/>.</summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TQueryOutput">The query output type.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    public async Task CreateQuerySchedule<TQuery, TQueryOutput>(TQuery query)
        where TQuery : BaseQuery
        where TQueryOutput : QueryOutput
    {
        await Task.Delay(TimeSpan.FromSeconds(_waitTimeInSeconds), _timeProvider);

        await _mediator.ExecuteQueryAsync<TQuery, TQueryOutput>(query);
    }
}

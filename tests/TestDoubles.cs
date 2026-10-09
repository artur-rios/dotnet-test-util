using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Mediator.Command;
using ArturRios.Mediator.Command.Interfaces;
using ArturRios.Mediator.Query;
using ArturRios.Mediator.Query.Interfaces;
using ArturRios.Output;

namespace ArturRios.Util.Test.Tests;

/// <summary>Simple <see cref="long"/>-keyed entity used to exercise <c>FakeRepository</c>.</summary>
public class Person : Entity<long>
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

/// <summary><see cref="int"/>-keyed entity used to exercise the fakes' sequential id generation.</summary>
public class Counter : Entity<int>
{
    public string Label { get; set; } = string.Empty;
}

/// <summary><see cref="Guid"/>-keyed entity used to exercise the fakes' Guid id generation.</summary>
public class Device : Entity<Guid>
{
    public string Model { get; set; } = string.Empty;
}

/// <summary><see cref="string"/>-keyed entity used to exercise caller-assigned ids.</summary>
public class Country : Entity<string>
{
    public string Name { get; set; } = string.Empty;
}

/// <summary><see cref="long"/>-keyed entity with an optimistic-concurrency stamp, used to exercise the fakes' stamp checks.</summary>
public class Account : VersionedEntity<long>
{
    public string Owner { get; set; } = string.Empty;
}

/// <summary>Command dispatched by the scheduler tests.</summary>
public class PingCommand : BaseCommand
{
    public string Value { get; init; } = string.Empty;
}

/// <summary>Output of <see cref="PingCommand"/>.</summary>
public class PingCommandOutput : CommandOutput
{
    public string Value { get; init; } = string.Empty;
}

/// <summary>Async command handler that records whether it ran and what it received.</summary>
public class RecordingCommandHandler : ICommandHandlerAsync<PingCommand, PingCommandOutput>
{
    public bool WasCalled { get; private set; }
    public PingCommand? Received { get; private set; }

    public async Task<DataOutput<PingCommandOutput?>> HandleAsync(
        PingCommand command,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        WasCalled = true;
        Received = command;

        return DataOutput<PingCommandOutput?>.New.WithData(new PingCommandOutput { Value = command.Value });
    }
}

/// <summary>Query dispatched by the scheduler tests.</summary>
public class PingQuery : BaseQuery
{
    public string Value { get; init; } = string.Empty;
}

/// <summary>Output of <see cref="PingQuery"/>.</summary>
public class PingQueryOutput : QueryOutput
{
    public string Value { get; init; } = string.Empty;
}

/// <summary>Async query handler that records whether it ran and what it received.</summary>
public class RecordingQueryHandler : IQueryHandlerAsync<PingQuery, PingQueryOutput>
{
    public bool WasCalled { get; private set; }
    public PingQuery? Received { get; private set; }

    public async Task<DataOutput<PingQueryOutput?>> HandleAsync(
        PingQuery query,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        WasCalled = true;
        Received = query;

        return DataOutput<PingQueryOutput?>.New.WithData(new PingQueryOutput { Value = query.Value });
    }
}

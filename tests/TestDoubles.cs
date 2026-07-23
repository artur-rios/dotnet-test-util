using ArturRios.Data.Relational.Core.Entities;
using ArturRios.Mediator.Command;
using ArturRios.Mediator.Command.Interfaces;
using ArturRios.Mediator.Query;
using ArturRios.Mediator.Query.Interfaces;
using ArturRios.Output;

namespace ArturRios.Util.Test.Tests;

/// <summary>Simple entity used to exercise <c>FakeRepository</c>.</summary>
public class Person : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
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

    public async Task<DataOutput<PingCommandOutput?>> HandleAsync(PingCommand command)
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

    public async Task<DataOutput<PingQueryOutput?>> HandleAsync(PingQuery query)
    {
        await Task.Yield();

        WasCalled = true;
        Received = query;

        return DataOutput<PingQueryOutput?>.New.WithData(new PingQueryOutput { Value = query.Value });
    }
}

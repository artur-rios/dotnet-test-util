---
title: Fakes
weight: 20
description: >-
  This page covers the in-memory test doubles: `FakeRepository<T>`, `AsyncFakeRepository<T>` and `FakeScheduler`.
---

This page covers the in-memory test doubles: `FakeRepository<T>`, `AsyncFakeRepository<T>` and `FakeScheduler`.

## FakeRepository&lt;T&gt;

`FakeRepository<T>` is an in-memory implementation of `IRepository<T>` (from `ArturRios.Data.Relational.Core`).
It stores entities in a backing list so you can exercise services that depend on a repository without touching a
database. `T` must derive from `ArturRios.Data.Relational.Core.Entities.Entity`. Every method returns a
`DataOutput<...>`; lookups that find no matching entity return a failed output carrying an error rather than
throwing.

| Method | Behavior |
|---|---|
| `Create(T)` | Assigns a fresh identifier (starting at `1`), stores the entity, returns the id |
| `GetById(long)` | Returns a successful output with the matching entity, or a failed output when the id is unknown |
| `GetAll()` | Returns every stored entity |
| `Query()` | Exposes the stored entities as `IQueryable<T>` |
| `Update(T)` | Copies writable properties (except `Id`) onto the stored entity; returns a failed output when the id is unknown |
| `Delete(T)` | Removes the entity with the matching id; returns a failed output when unknown |
| `CreateRange(IEnumerable<T>)` | Stores every entity, assigning each a fresh id; returns the assigned ids |
| `UpdateRange(IEnumerable<T>)` | Updates every entity that exists, silently skipping unknown ids; returns the updated entities |
| `DeleteRange(IEnumerable<long>)` | Removes every entity whose id is listed; returns the ids that were actually removed |

```csharp
public class Person : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var repository = new FakeRepository<Person>();

var annId = repository.Create(new Person { Name = "Ann", Age = 30 }).Data;   // annId == 1
var bobId = repository.Create(new Person { Name = "Bob", Age = 25 }).Data;   // bobId == 2

repository.Update(new Person { Id = annId, Name = "Ann Smith", Age = 31 });

var ann = repository.GetById(annId).Data;      // Name == "Ann Smith"
var all = repository.GetAll().Data!.ToList();  // two people

repository.DeleteRange([annId, bobId]);
```

`Update` uses reflection to copy every writable property from the incoming entity onto the stored one, so it
mirrors the behavior of a real ORM update where the identifier is preserved.

## AsyncFakeRepository&lt;T&gt;

`AsyncFakeRepository<T>` is the asynchronous counterpart of `FakeRepository<T>`. It implements
`IAsyncRepository<T>` (from `ArturRios.Data.Relational.Core`) with the same in-memory storage and semantics, so
you can exercise services that depend on an async repository without touching a database. `T` must derive from
`ArturRios.Data.Relational.Core.Entities.Entity`.

Every method returns a `Task<DataOutput<...>>` that completes synchronously, and each accepts an optional
`CancellationToken` that is observed before the operation runs (a cancelled token throws
`OperationCanceledException`). `Query()` keeps the synchronous signature the interface declares, but the
`IQueryable<T>` it returns is backed by an async query provider, so EF Core's async operators
(`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, …) can be composed on top of it.

| Method | Behavior |
|---|---|
| `CreateAsync(T, CancellationToken)` | Assigns a fresh identifier (starting at `1`), stores the entity, returns the id |
| `GetByIdAsync(long, CancellationToken)` | Returns a successful output with the matching entity, or a failed output when the id is unknown |
| `GetAllAsync(CancellationToken)` | Returns every stored entity |
| `Query()` | Exposes the stored entities as an async-capable `IQueryable<T>` (supports `ToListAsync`, `FirstOrDefaultAsync`, …) |
| `UpdateAsync(T, CancellationToken)` | Copies writable properties (except `Id`) onto the stored entity; returns a failed output when the id is unknown |
| `DeleteAsync(T, CancellationToken)` | Removes the entity with the matching id; returns a failed output when unknown |
| `CreateRangeAsync(IEnumerable<T>, CancellationToken)` | Stores every entity, assigning each a fresh id; returns the assigned ids |
| `UpdateRangeAsync(IEnumerable<T>, CancellationToken)` | Updates every entity that exists, silently skipping unknown ids; returns the updated entities |
| `DeleteRangeAsync(IEnumerable<long>, CancellationToken)` | Removes every entity whose id is listed; returns the ids that were actually removed |

```csharp
var repository = new AsyncFakeRepository<Person>();

var annId = (await repository.CreateAsync(new Person { Name = "Ann", Age = 30 })).Data;   // annId == 1
var bobId = (await repository.CreateAsync(new Person { Name = "Bob", Age = 25 })).Data;   // bobId == 2

await repository.UpdateAsync(new Person { Id = annId, Name = "Ann Smith", Age = 31 });

var ann = (await repository.GetByIdAsync(annId)).Data;      // Name == "Ann Smith"
var all = (await repository.GetAllAsync()).Data!.ToList();  // two people

// Query() composes with EF Core's async operators:
var adults = await repository.Query().Where(p => p.Age >= 18).ToListAsync();

await repository.DeleteRangeAsync([annId, bobId]);
```

## FakeScheduler

`FakeScheduler` simulates a scheduled (delayed) dispatch of a command or query through an
`ArturRios.Mediator.CommandQueryMediator`. It waits for a configurable delay and then dispatches, which is
useful for testing code paths that would otherwise run on a timer or background schedule.

```csharp
FakeScheduler(CommandQueryMediator mediator, int waitTimeInSeconds = 60)
```

| Method | Behavior |
|---|---|
| `CreateCommandSchedule<TCommand, TCommandOutput>(TCommand)` | Waits, then dispatches the command |
| `CreateQuerySchedule<TQuery, TQueryOutput>(TQuery)` | Waits, then dispatches the query |

Pass a small `waitTimeInSeconds` (for example `0`) to keep tests fast:

```csharp
var services = new ServiceCollection();
services.AddSingleton<ICommandHandlerAsync<PingCommand, PingCommandOutput>>(handler);
var provider = services.BuildServiceProvider();

var mediator = new CommandQueryMediator(provider.GetRequiredService<IServiceScopeFactory>());
var scheduler = new FakeScheduler(mediator, waitTimeInSeconds: 0);

await scheduler.CreateCommandSchedule<PingCommand, PingCommandOutput>(new PingCommand { Value = "ping" });
// the handler has now been invoked through the mediator
```

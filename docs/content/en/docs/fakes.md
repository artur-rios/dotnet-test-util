---
title: Fakes
weight: 20
description: >-
  This page covers the in-memory test doubles: `FakeRepository<T, TKey>`, `AsyncFakeRepository<T, TKey>` and `FakeScheduler`.
---

This page covers the in-memory test doubles: `FakeRepository<T, TKey>`, `AsyncFakeRepository<T, TKey>` and `FakeScheduler`.

## FakeRepository&lt;T, TKey&gt;

`FakeRepository<T, TKey>` is an in-memory implementation of `IRepository<T, TKey>` (from
`ArturRios.Data.Relational.Core`). It stores entities in a backing list so you can exercise services that depend
on a repository without touching a database. `T` must derive from
`ArturRios.Data.Relational.Core.Entities.Entity<TKey>`, and `TKey` is the entity's key type (`long`, `int`,
`Guid`, `string`, …). Every method except `Query()` returns a `DataOutput<...>`, with the outcomes
`EfRepository<T, TKey>` produces, so a test that passes against the fake passes for the reason it would in
production: a lookup that finds nothing succeeds with no data, and an update or delete that would affect no row
fails with `RelationalErrors.ConcurrencyMessage` and changes nothing.

| Method | Behavior |
|---|---|
| `Create(T)` | Assigns the entity's id (see [How ids are assigned](#how-ids-are-assigned)), stores it, returns the id |
| `GetById(TKey)` | Returns a successful output with the matching entity, or a successful output with `null` data when the id is unknown |
| `GetAll()` | Returns a snapshot of every stored entity (later writes don't change it) |
| `Query()` | Exposes the stored entities as `IQueryable<T>` |
| `Update(T)` | Copies writable properties (except `Id`) onto the stored entity; fails with `RelationalErrors.ConcurrencyMessage` when the id is unknown or the concurrency stamp is stale (see [Versioned entities](#versioned-entities)) |
| `Delete(T)` | Removes the entity with the matching id; fails with `RelationalErrors.ConcurrencyMessage` when the id is unknown or the concurrency stamp is stale |
| `CreateRange(IEnumerable<T>)` | Stores every entity, assigning each its id; returns the ids. If any id is missing or duplicated, stores nothing and returns a failed output |
| `UpdateRange(IEnumerable<T>)` | Updates every entity, all or none: if any id is unknown or any stamp stale, updates nothing and fails with `RelationalErrors.ConcurrencyMessage`; returns the updated entities |
| `DeleteRange(IEnumerable<TKey>)` | Removes every entity whose id is listed; returns the ids that were actually removed |

```csharp
public class Person : Entity<long>
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var repository = new FakeRepository<Person, long>();

var annId = repository.Create(new Person { Name = "Ann", Age = 30 }).Data;   // annId == 1
var bobId = repository.Create(new Person { Name = "Bob", Age = 25 }).Data;   // bobId == 2

repository.Update(new Person { Id = annId, Name = "Ann Smith", Age = 31 });

var ann = repository.GetById(annId).Data;      // Name == "Ann Smith"
var all = repository.GetAll().Data!.ToList();  // two people

repository.DeleteRange([annId, bobId]);
```

`Update` uses reflection to copy every writable property from the incoming entity onto the stored one, so it
mirrors the behavior of a real ORM update where the identifier is preserved.

### Versioned entities

For an entity implementing `IVersionedEntity` (such as one deriving from `VersionedEntity<TKey>`), `Update`,
`UpdateRange` and `Delete` check the incoming `ConcurrencyStamp` against the stored one, as the real repository's
`UPDATE`/`DELETE` does. A stale stamp — including the fresh one a newly constructed entity gets — fails with
`RelationalErrors.ConcurrencyMessage` and changes nothing. A successful update issues a new stamp, on the stored
entity and on the one passed in, as `BaseDbContext` does on save. So an entity rebuilt from a request must carry
the stamp it was read with, in tests as in production:

```csharp
var accounts = new FakeRepository<Account, long>();   // Account : VersionedEntity<long>
var id = accounts.Create(new Account { Owner = "Ann" }).Data;
var stamp = accounts.GetById(id).Data!.ConcurrencyStamp;

accounts.Update(new Account { Id = id, Owner = "Bob" });                           // fails: stale stamp
accounts.Update(new Account { Id = id, Owner = "Bob", ConcurrencyStamp = stamp }); // succeeds, new stamp
```

### How ids are assigned

`Create` and `CreateRange` (and their async counterparts) pick the id by key type, the way a database would for
the usual key columns:

| Key type | Id assigned on create |
|---|---|
| `long`, `int` | Sequentially, starting at `1` per repository instance; ids are never reused after a delete |
| `Guid` | `Guid.NewGuid()` |
| Anything else (e.g. `string`) | None: the caller sets `Id` before creating. A missing (`null`/default) id or one that is already stored returns a failed output |

When the fake generates the id, it overwrites any `Id` the caller set, so `long`-keyed tests behave exactly as
they did in 3.x. To choose the ids yourself — for any key type — pass a generator to the constructor; it is called
once per created entity:

```csharp
public class Country : Entity<string>
{
    public string Name { get; set; } = string.Empty;
}

// Caller-assigned string keys:
var countries = new FakeRepository<Country, string>();
countries.Create(new Country { Id = "BR", Name = "Brazil" });   // Data == "BR"
countries.Create(new Country { Name = "Nowhere" });            // failed output: no id

// Generated string keys:
var next = 0;
var generated = new FakeRepository<Country, string>(() => $"C{++next}");
generated.Create(new Country { Name = "Brazil" });             // Data == "C1"
```

A generator that returns an id already in the store makes the create fail with
`RelationalErrors.UniqueViolationMessage`, as a unique-key violation would.

## AsyncFakeRepository&lt;T, TKey&gt;

`AsyncFakeRepository<T, TKey>` is the asynchronous counterpart of `FakeRepository<T, TKey>`. It implements
`IAsyncRepository<T, TKey>` (from `ArturRios.Data.Relational.Core`) with the same in-memory storage, the same
[id assignment](#how-ids-are-assigned) and the same semantics, so you can exercise services that depend on an
async repository without touching a database. `T` must derive from
`ArturRios.Data.Relational.Core.Entities.Entity<TKey>`.

Every method returns a `Task<DataOutput<...>>` that completes synchronously, and each accepts an optional
`CancellationToken` that is observed before the operation runs (a cancelled token throws
`OperationCanceledException`). `Query()` keeps the synchronous signature the interface declares, but the
`IQueryable<T>` it returns is backed by an async query provider, so EF Core's async operators
(`ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, …) can be composed on top of it.

| Method | Behavior |
|---|---|
| `CreateAsync(T, CancellationToken)` | Assigns the entity's id (see [How ids are assigned](#how-ids-are-assigned)), stores it, returns the id |
| `GetByIdAsync(TKey, CancellationToken)` | Returns a successful output with the matching entity, or a successful output with `null` data when the id is unknown |
| `GetAllAsync(CancellationToken)` | Returns a snapshot of every stored entity (later writes don't change it) |
| `Query()` | Exposes the stored entities as an async-capable `IQueryable<T>` (supports `ToListAsync`, `FirstOrDefaultAsync`, …) |
| `UpdateAsync(T, CancellationToken)` | Copies writable properties (except `Id`) onto the stored entity; fails with `RelationalErrors.ConcurrencyMessage` when the id is unknown or the concurrency stamp is stale |
| `DeleteAsync(T, CancellationToken)` | Removes the entity with the matching id; fails with `RelationalErrors.ConcurrencyMessage` when the id is unknown or the concurrency stamp is stale |
| `CreateRangeAsync(IEnumerable<T>, CancellationToken)` | Stores every entity, assigning each its id; returns the ids. If any id is missing or duplicated, stores nothing and returns a failed output |
| `UpdateRangeAsync(IEnumerable<T>, CancellationToken)` | Updates every entity, all or none: if any id is unknown or any stamp stale, updates nothing and fails with `RelationalErrors.ConcurrencyMessage`; returns the updated entities |
| `DeleteRangeAsync(IEnumerable<TKey>, CancellationToken)` | Removes every entity whose id is listed; returns the ids that were actually removed |

```csharp
var repository = new AsyncFakeRepository<Person, long>();

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

## Upgrading to 4.0

The changes the 4.0 fakes need in your tests are listed in
[Upgrading from 3.x to 4.0](../changelog/#upgrading-from-3x-to-40) in the changelog.

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
`Guid`, `string`, …). Every method returns a `DataOutput<...>`; lookups that find no matching entity return a
failed output carrying an error rather than throwing.

| Method | Behavior |
|---|---|
| `Create(T)` | Assigns the entity's id (see [How ids are assigned](#how-ids-are-assigned)), stores it, returns the id |
| `GetById(TKey)` | Returns a successful output with the matching entity, or a failed output when the id is unknown |
| `GetAll()` | Returns every stored entity |
| `Query()` | Exposes the stored entities as `IQueryable<T>` |
| `Update(T)` | Copies writable properties (except `Id`) onto the stored entity; returns a failed output when the id is unknown |
| `Delete(T)` | Removes the entity with the matching id; returns a failed output when unknown |
| `CreateRange(IEnumerable<T>)` | Stores every entity, assigning each its id; returns the ids. If any id is missing or duplicated, stores nothing and returns a failed output |
| `UpdateRange(IEnumerable<T>)` | Updates every entity that exists, silently skipping unknown ids; returns the updated entities |
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

A generator that returns an id already in the store makes the create fail, as a unique-key violation would.

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
| `GetByIdAsync(TKey, CancellationToken)` | Returns a successful output with the matching entity, or a failed output when the id is unknown |
| `GetAllAsync(CancellationToken)` | Returns every stored entity |
| `Query()` | Exposes the stored entities as an async-capable `IQueryable<T>` (supports `ToListAsync`, `FirstOrDefaultAsync`, …) |
| `UpdateAsync(T, CancellationToken)` | Copies writable properties (except `Id`) onto the stored entity; returns a failed output when the id is unknown |
| `DeleteAsync(T, CancellationToken)` | Removes the entity with the matching id; returns a failed output when unknown |
| `CreateRangeAsync(IEnumerable<T>, CancellationToken)` | Stores every entity, assigning each its id; returns the ids. If any id is missing or duplicated, stores nothing and returns a failed output |
| `UpdateRangeAsync(IEnumerable<T>, CancellationToken)` | Updates every entity that exists, silently skipping unknown ids; returns the updated entities |
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

4.0 moves to `ArturRios.Data.Relational.Core` 5.0, which replaced the `long`-keyed `Entity` with `Entity<TKey>`
and gave every repository contract a key type argument. The fakes now implement `IRepository<T, TKey>` and
`IAsyncRepository<T, TKey>`; `FakeRepository<T>` and `AsyncFakeRepository<T>` are removed. To keep the previous
`long` keys, add `long` everywhere the old types appear:

| 3.x | 4.x |
|---|---|
| `class Person : Entity` | `class Person : Entity<long>` |
| `class Person : VersionedEntity` | `class Person : VersionedEntity<long>` |
| `FakeRepository<Person>` | `FakeRepository<Person, long>` |
| `AsyncFakeRepository<Person>` | `AsyncFakeRepository<Person, long>` |
| `static AsyncFakeRepository<T> Seed<T>(...) where T : Entity` | `static AsyncFakeRepository<T, long> Seed<T>(...) where T : Entity<long>` |

Nothing else changes for `long` keys: ids still start at `1`, still overwrite any id set before `Create`, and
lookups, updates and deletes behave as before. A fake now passes wherever the code under test takes
`IRepository<T, TKey>` or `IAsyncRepository<T, TKey>` (or their read-only bases), exactly as
`EfRepository<T, TKey>` does.

+++
title = "Fakes"
show_nav       = true
nav_back_label = "Assertions & Attributes"
nav_back_url   = "/dotnet-test-util/assertions-and-attributes"
nav_next_label = "Web API Testing"
nav_next_url   = "/dotnet-test-util/web-api-testing"
+++

This page covers the in-memory test doubles: `FakeRepository<T>` and `FakeScheduler`.

## FakeRepository&lt;T&gt;

`FakeRepository<T>` is an in-memory implementation of `ICrudRepository<T>` and `IRangeRepository<T>` (from
`ArturRios.Data`). It stores entities in a backing list so you can exercise services that depend on a
repository without touching a database. `T` must derive from `ArturRios.Data.Entity`.

| Method | Behavior |
|---|---|
| `Create(T)` | Assigns a fresh identifier (starting at `1`), stores the entity, returns the id |
| `GetById(int)` | Returns the matching entity, or `null` |
| `GetAll()` | Returns every stored entity as `IQueryable<T>` |
| `Update(T)` | Copies writable properties (except `Id`) onto the stored entity; throws `KeyNotFoundException` when the id is unknown |
| `Delete(T)` | Removes the entity with the matching id; throws `KeyNotFoundException` when unknown |
| `UpdateRange(List<T>)` | Updates every entity that exists, silently skipping unknown ids; returns the updated entities |
| `DeleteRange(List<int>)` | Removes every entity whose id is listed; returns the ids that were actually removed |

```csharp
public class Person : Entity
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

var repository = new FakeRepository<Person>();

var annId = repository.Create(new Person { Name = "Ann", Age = 30 });   // annId == 1
var bobId = repository.Create(new Person { Name = "Bob", Age = 25 });   // bobId == 2

repository.Update(new Person { Id = annId, Name = "Ann Smith", Age = 31 });

var ann = repository.GetById(annId);   // Name == "Ann Smith"
var all = repository.GetAll().ToList(); // two people

repository.DeleteRange([annId, bobId]);
```

`Update` uses reflection to copy every writable property from the incoming entity onto the stored one, so it
mirrors the behavior of a real ORM update where the identifier is preserved.

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

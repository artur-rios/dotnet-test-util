# Changelog

All notable changes to `ArturRios.Util.Test` are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- `ArturRios.Configuration` updated from 2.0.1 to 2.0.2, `ArturRios.Data.Relational.Core` from 5.0.2 to 5.0.3,
  `ArturRios.Mediator` from 2.0.2 to 2.0.3, `ArturRios.Util` from 2.2.1 to 2.3.0 and `ArturRios.Util.WebApi` from
  6.0.0 to 6.0.1.

## [6.1.0] - 2026-11-19

### Added

- `WebApiHostOptions` configures the in-memory host of `WebApiTest<T>` (new constructor `WebApiTest(WebApiHostOptions)`)
  and of `WebApiHost<TProgram>`: the environment name, whether to set `ASPNETCORE_ENVIRONMENT`, configuration values
  (seen also by what `Program.cs` reads before `Build()`), services to add or replace, a `TimeProvider`, logger
  providers, a faked local port, and the client's options.
- `WebApiTest<T>` takes an environment name of the API's own (`WebApiTest(string)`), has a virtual
  `ConfigureWebHost(IWebHostBuilder)` to configure the host, and reaches it directly through `Factory`, `Server`,
  `Services` and `CreateClient()`, for requests the gateway does not model.
- `WebApiHost<TProgram>`, the same in-memory host as an xUnit class or collection fixture, so the tests of a class
  share one host instead of starting one each, and `SharedWebApiTest<TProgram>`, a base class that gives a test using
  it `Gateway`, `AuthenticateAsync`, `Authorize` and `AuthenticateAndAuthorizeAsync`, with a fresh client (no
  headers, token or cookies) for every test. `WebApiHost<TProgram>.Start(options)` starts a host a test owns. The
  documentation says when a shared host is unsafe: whatever the API keeps in memory, rate-limiter budgets included,
  is shared by its tests.
- `FakeScheduler(CommandQueryMediator, TimeProvider, int)`: the scheduler's delay, the 60-second default included,
  runs on the given clock, so a test can make it elapse with a `FakeTimeProvider`. The package now references
  `Microsoft.Extensions.TimeProvider.Testing`, which provides `FakeTimeProvider`.
- `EnvironmentVariableScope`, which sets and clears environment variables and restores what each held, value or none,
  when disposed, and `CultureScope`, which sets the current culture and UI culture and restores them
  (`ArturRios.Util.Test.Scopes`).
- `RecordingLogger`, `RecordingLogger<T>` and `RecordingLoggerProvider`, which keep the level, event id, message,
  exception, state and scopes of every entry logged to them, and `LogAssert`, with assertions over those entries
  (`ArturRios.Util.Test.Logging`). A `RecordingLoggerProvider` plugs into a host through
  `WebApiHostOptions.LoggerProviders`.
- `WebApiHost<TProgram>.StartVariant(Action<IWebHostBuilder>)` (and the protected `WebApiTest<T>.StartVariant`)
  starts a variant of a host: a host of its own, built as the host is and then as the action says, which the test
  owns and disposes, and which is released when disposed even while the host it came from runs on.
- `WebApiHostOptions.DisposeGlobalRateLimiter`: disposing the host also disposes the API's global rate limiter
  (`RateLimiterOptions.GlobalLimiter`), for an API that builds one for each host. Off by default, since an API may
  share one limiter between hosts.

### Changed

- **Dependency:** `ArturRios.Util.WebApi` updated from 5.2.0 to 6.0.0, which brings `Swashbuckle.AspNetCore` 11 and
  `Microsoft.OpenApi` 3 with it. A test project gets them too, so the API it tests should be on
  `ArturRios.Util.WebApi` 6 first; an API still on 5.x, built against Swashbuckle 10, can fail at run time in the
  in-memory host. Nothing in this package's own API changes.
- `ArturRios.Configuration` updated from 2.0.0 to 2.0.1, `ArturRios.Data.Relational.Core` from 5.0.1 to 5.0.2,
  `ArturRios.Mediator` from 2.0.1 to 2.0.2 and `ArturRios.Util` from 2.2.0 to 2.2.1.
- The package's tags are `utilities`, `test`, `testing`, `xunit` and `.net`. They were comma-separated, which NuGet
  does not split on, so they were listed as `utilities,`, `test,` and `.net`.

### Fixed

- A disposed `WebApiTest<T>` (or `WebApiHost<TProgram>`) host can be garbage-collected. Every host stayed alive
  until the test process exited, so a suite's memory grew with every test. Three things held it:
  - a timer the API never stops captured the test's execution context, which reaches the test and its host. The
    host now starts in an execution context of its own, carrying only the current cultures
    (`WebApiHostOptions.IsolateExecutionContext`, on by default);
  - such a timer, started on the thread `WebApplicationFactory` runs the API's entry point on, also captured that
    thread's context, which holds the factory and the host. Disposing the host now releases what it holds;
  - ASP.NET Core's rate-limiting middleware never disposes the limiter it builds for endpoint policies, whose timer
    holds the whole application. Disposing the host now disposes that limiter.
- A variant host made with `Factory.WithWebHostBuilder(...)` starts in an execution context of its own, as its host
  does. It started in the test's, so a timer the API started captured the test's context and kept the variant, and
  the host it came from, alive after both were disposed.
- A host whose API's global rate limiter holds its services can be garbage-collected, with
  `WebApiHostOptions.DisposeGlobalRateLimiter` on. The limiter's own timer kept it, its partitioner and what that
  holds alive after the host was disposed; the host leaves it alone by default, since the API may share it between
  hosts, and the documentation says what an API can do instead.
- `AsyncFakeRepository.Query()` hands back the exception a query operator throws, as EF Core does. `SingleAsync` or
  `FirstAsync` with no match threw a `TargetInvocationException` wrapping the `InvalidOperationException`, so a
  handler that catches `InvalidOperationException` behaved differently against the fake and in production.
- The async query operators over `AsyncFakeRepository.Query()` (`ToListAsync`, `CountAsync`, `FirstOrDefaultAsync`,
  `await foreach`, ...) observe their cancellation token, as EF Core's do. They ran to completion with a cancelled one.
- `AsyncFakeRepository`'s async methods return a cancelled task for a cancelled token, as `EfRepository`'s do,
  instead of throwing `OperationCanceledException` before returning one. Code that starts the call and awaits it later
  now sees the cancellation where it would in production.
- Disposing a `WebApiTest<T>` or `WebApiHost<TProgram>` puts back the `ASPNETCORE_ENVIRONMENT` value the process had
  before the host set it, once the last host that set it is disposed. The variable stayed set, so a later test in
  another class that read it, or built a host of its own with `WebApplication.CreateBuilder()`, ran as the earlier
  host's environment, depending on the order the tests ran in. The docs no longer give the environment-aware test
  attributes as the reason the variable is set: they read it when the tests are discovered, before any host starts.

## [6.0.0] - 2026-11-14

### Changed

- **Behavior change:** `ArturRios.Configuration` updated from 1.2.0 to 2.0.0. The API host `WebApiTest<T>` starts
  resolves it too, and its `ConfigurationLoader.LoadEnvironment()` no longer lets a `.env` file override environment
  variables the process already has: real environment variables now win over the file.
- `ArturRios.Data.Relational.Core` updated from 5.0.0 to 5.0.1, `ArturRios.Mediator` from 2.0.0 to 2.0.1,
  `ArturRios.Util` from 2.1.0 to 2.2.0, `ArturRios.Util.WebApi` from 5.1.0 to 5.2.0,
  `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore` from 10.0.12 to 10.0.15, and
  `xunit.v3.assert` and `xunit.v3.extensibility.core` from 4.0.2 to 4.0.3.

## [5.0.1] - 2026-10-29

### Changed

- The package's project URL is the documentation site's new address, `https://docs.artur-rios.tech/dotnet-test-util`, since the
  site moved from GitHub Pages.

## [5.0.0] - 2026-10-09

### Changed

- **Breaking:** `FakeRepository<T, TKey>` and `AsyncFakeRepository<T, TKey>` now produce the outcomes
  `EfRepository<T, TKey>` (`ArturRios.Data.Relational.Core`) produces, so a test that passes against a fake no longer
  passes where the deployed repository would fail. See [Upgrading from 4.x to 5.0](#upgrading-from-4x-to-50).
  - `GetById`/`GetByIdAsync` with an unknown id return a **successful** output with `null` data, as the repository
    contract specifies, instead of a failed one. Check `Data`, not `Success`, for "not found".
  - `Update`, `Delete` and their async forms fail with `RelationalErrors.ConcurrencyMessage` (instead of
    `"Entity with Id … not found"`) when the id is unknown.
  - `UpdateRange`/`UpdateRangeAsync` are all or none: a range containing an unknown id fails with
    `RelationalErrors.ConcurrencyMessage` and updates nothing, instead of silently skipping the unknown entities.
  - For `IVersionedEntity` entities, `Update`, `UpdateRange` and `Delete` (and async forms) fail with
    `RelationalErrors.ConcurrencyMessage` when the incoming `ConcurrencyStamp` differs from the stored one, and a
    successful update issues a new stamp, as `BaseDbContext` does.
  - Creating an entity whose id is already stored fails with `RelationalErrors.UniqueViolationMessage` instead of
    `"Entity with Id … already exists"`.
- **Breaking:** the package is built on xUnit v3 (4.0.2) instead of xUnit v2 (2.9.3). It references
  `xunit.v3.extensibility.core` and `xunit.v3.assert` in place of `xunit`, so a test project using it moves to xUnit v3
  too. `UnitFact`, `UnitTheory`, `FunctionalFact` and `FunctionalTheory` keep their parameters and their `Category`
  trait, which they now publish through xUnit v3's `ITraitAttribute.GetTraits()`. They also take xUnit v3's
  compiler-supplied `sourceFilePath` and `sourceLineNumber`, so test results point at the test's own declaration.

### Removed

- `TestTypeTraitDiscoverer`. xUnit v2 needed it to read the `Category` trait; xUnit v3 reads it from the attributes.

### Fixed

- `GetAll`/`GetAllAsync` return a snapshot. They returned the fakes' backing list itself, so a later write changed a
  result the caller already held, and deleting entities while enumerating it threw `InvalidOperationException`.

### Upgrading from 4.x to 5.0

Move the test project to xUnit v3:

- Replace the `xunit` package with `xunit.v3`, or with `xunit.v3.mtp-off` to keep running through VSTest so that
  `dotnet test`, its `--filter` and `.runsettings` loggers work as before. Use `xunit.runner.visualstudio` 4.x.
- Apply the [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration) to the project's own code,
  for example `IAsyncLifetime` members returning `ValueTask`, and `[assembly: Parallelization(...)]` in place of
  `CollectionBehavior(DisableTestParallelization = ...)`.
- Tests marked with the custom attributes need no change, and `--filter "Category=Unit"` selects the same tests.

Then check tests that assert on the fakes' failures:

| 4.x | 5.0 |
|---|---|
| `GetById` with an unknown id fails | It succeeds with `null` data: check `Data`, not `Success` |
| Unknown id in `Update`/`Delete`: `"Entity with Id … not found"` | `RelationalErrors.ConcurrencyMessage` |
| `UpdateRange` skips unknown ids | It fails with `RelationalErrors.ConcurrencyMessage` and updates nothing |
| Duplicate id on create: `"Entity with Id … already exists"` | `RelationalErrors.UniqueViolationMessage` |
| A stale `ConcurrencyStamp` is accepted | It fails with `RelationalErrors.ConcurrencyMessage` |

## [4.0.0] - 2026-10-07

### Added

- `FakeRepository<T, TKey>` and `AsyncFakeRepository<T, TKey>`, implementing `IRepository<T, TKey>` and
  `IAsyncRepository<T, TKey>` for any entity key type. `long` and `int` ids are assigned sequentially from 1, `Guid`
  ids with `Guid.NewGuid()`, and any other key type keeps the caller-assigned id; a `Func<TKey>` passed to the
  constructor replaces the default. Creating an entity with a missing or already-stored id returns a failed output,
  and `CreateRange` stores nothing when any id is rejected.

### Changed

- **Breaking:** moved to `ArturRios.Data.Relational.Core` 5.0.0, where entities derive from `Entity<TKey>`. See
  [Upgrading from 3.x to 4.0](#upgrading-from-3x-to-40).
- `ArturRios.Util.WebApi` updated to 5.1.0 and the Microsoft packages to 10.0.12.

### Removed

- **Breaking:** `FakeRepository<T>` and `AsyncFakeRepository<T>`. Use `FakeRepository<T, long>` /
  `AsyncFakeRepository<T, long>` to keep the previous behavior.

### Upgrading from 3.x to 4.0

4.0 moves to `ArturRios.Data.Relational.Core` 5.0, where entities declare their key type (`Entity<TKey>`) and
the repository contracts take it as a second argument. The fakes follow: they implement
`IRepository<T, TKey>` / `IAsyncRepository<T, TKey>`, and the single-argument `FakeRepository<T>` and
`AsyncFakeRepository<T>` are removed. To keep the previous `long` keys, add `long` everywhere the old types appear:

| 3.x | 4.x |
|---|---|
| `class Person : Entity` | `class Person : Entity<long>` |
| `class Person : VersionedEntity` | `class Person : VersionedEntity<long>` |
| `new FakeRepository<Person>()` | `new FakeRepository<Person, long>()` |
| `new AsyncFakeRepository<Person>()` | `new AsyncFakeRepository<Person, long>()` |
| `where T : Entity` (generic helpers over the fakes) | `where T : Entity<long>` |

Behavior for `long` keys is unchanged: ids are assigned sequentially from `1`, overwriting any id the caller set.
`int` keys behave the same way, `Guid` keys get `Guid.NewGuid()`, and any other key type (such as `string`) keeps the
caller-assigned id. Pass a `Func<TKey>` to the constructor to generate ids yourself — see
[Fakes](https://artur-rios.github.io/dotnet-test-util/docs/fakes/#how-ids-are-assigned).

A fake now passes wherever the code under test takes `IRepository<T, TKey>` or `IAsyncRepository<T, TKey>` (or their
read-only bases), exactly as `EfRepository<T, TKey>` does.

4.0 also moves to `ArturRios.Util.WebApi` 5.1; `WebApiTest<T>` needed no change.

## [3.0.0] - 2026-08-24

### Changed

- **Breaking:** `WebApiTest<T>.Authorize` assigns the `Authorization` header rather than adding it, so authorizing
  twice within one test class replaces the header instead of sending two.
- The host's environment is passed to the host builder as well as set on the process.
- Dependencies updated: `ArturRios.Configuration` 1.2.0, `ArturRios.Data.Relational.Core` 4.0.1,
  `ArturRios.Mediator` 2.0.0, `ArturRios.Util` 2.1.0 and `ArturRios.Util.WebApi` 4.0.0.

### Removed

- **Breaking:** the `Newtonsoft.Json` dependency. A consumer that relied on getting it transitively must reference it
  directly.

## [2.3.0] - 2026-08-19

### Changed

- Dependencies updated: `ArturRios.Configuration` 1.1.0, `ArturRios.Data.Relational.Core` 4.0.0, `ArturRios.Util`
  2.0.0, `ArturRios.Util.WebApi` 3.3.0, and `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore`
  10.0.11. The public API is unchanged.

## [2.2.0] - 2026-07-28

### Added

- `AsyncFakeRepository.Query()` supports EF Core's async operators (`ToListAsync`, `FirstOrDefaultAsync`,
  `CountAsync`, …) on top of the in-memory fake.

## [2.1.0] - 2026-07-28

### Added

- `AsyncFakeRepository<T>`, an in-memory `IAsyncRepository<T>` with cancellation support.

### Changed

- `ArturRios.*` dependencies updated.

## [2.0.0] - 2026-07-23

### Added

- `UnitFact`/`UnitTheory` and `FunctionalFact`/`FunctionalTheory` stamp a `Category` trait (`Unit` or `Functional`),
  so tests can be filtered with `dotnet test --filter "Category=Unit"`.

### Changed

- **Breaking:** `FakeRepository<T>` implements `ArturRios.Data.Relational.Core`'s `IRepository<T>` and returns
  `DataOutput` wrappers; not-found lookups return a failed output instead of throwing. The `ArturRios.Data`
  dependency is replaced by `ArturRios.Data.Relational.Core` 3.0.0.
- **Breaking:** `CustomAssert`, `TestException` and `WebApiTest<T>` moved to the `ArturRios.Util.Test.Assertion`,
  `ArturRios.Util.Test.Exceptions` and `ArturRios.Util.Test.Functional` namespaces.

## [1.0.0] - 2026-07-11

### Added

- `CustomAssert` with null/empty assertions for collections and strings.
- Environment-aware `UnitFact`, `UnitTheory`, `FunctionalFact` and `FunctionalTheory` attributes.
- In-memory `FakeRepository<T>` and `FakeScheduler`.
- `WebApiTest<T>`, a base class for functional web API tests on an in-memory host, and `TestException`.

[Unreleased]: https://github.com/artur-rios/dotnet-test-util/compare/6.1.0...HEAD
[6.1.0]: https://github.com/artur-rios/dotnet-test-util/compare/6.0.0...6.1.0
[6.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/5.0.1...6.0.0
[5.0.1]: https://github.com/artur-rios/dotnet-test-util/compare/5.0.0...5.0.1
[5.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/4.0.0...5.0.0
[4.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/3.0.0...4.0.0
[3.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.3.0...3.0.0
[2.3.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/artur-rios/dotnet-test-util/releases/tag/v1.0.0

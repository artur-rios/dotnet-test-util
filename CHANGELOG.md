# Changelog

All notable changes to `ArturRios.Util.Test` are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Breaking:** `FakeRepository<T, TKey>` and `AsyncFakeRepository<T, TKey>` now produce the outcomes
  `EfRepository<T, TKey>` (`ArturRios.Data.Relational.Core`) produces, so a test that passes against a fake no longer
  passes where the deployed repository would fail. Releasing this needs a major version bump.
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

### Fixed

- `GetAll`/`GetAllAsync` return a snapshot. They returned the fakes' backing list itself, so a later write changed a
  result the caller already held, and deleting entities while enumerating it threw `InvalidOperationException`.

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

[Unreleased]: https://github.com/artur-rios/dotnet-test-util/compare/4.0.0...HEAD
[4.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/3.0.0...4.0.0
[3.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.3.0...3.0.0
[2.3.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/artur-rios/dotnet-test-util/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/artur-rios/dotnet-test-util/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/artur-rios/dotnet-test-util/releases/tag/v1.0.0

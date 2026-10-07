# Test Util

[![Docs](https://img.shields.io/badge/docs-website-blue)](https://artur-rios.github.io/dotnet-test-util)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![NuGet](https://img.shields.io/nuget/v/ArturRios.Util.Test.svg)](https://www.nuget.org/packages/ArturRios.Util.Test)

`ArturRios.Util.Test` is a small .NET library of test-support utilities for xUnit projects. It bundles the
helpers that come up again and again when testing services and web APIs: extra assertions, environment-aware
test attributes, in-memory fakes for repositories and schedulers, and a base class for functional web API tests.

## Installation

```bash
dotnet add package ArturRios.Util.Test
```

The package targets **net10.0** and builds on other `ArturRios.*` packages
([`ArturRios.Util`](https://www.nuget.org/packages/ArturRios.Util),
[`ArturRios.Data.Relational.Core`](https://www.nuget.org/packages/ArturRios.Data.Relational.Core),
[`ArturRios.Mediator`](https://www.nuget.org/packages/ArturRios.Mediator),
[`ArturRios.Configuration`](https://www.nuget.org/packages/ArturRios.Configuration) and
[`ArturRios.Util.WebApi`](https://www.nuget.org/packages/ArturRios.Util.WebApi)) plus `xunit` and
`Microsoft.AspNetCore.Mvc.Testing`.

## What's inside

| Component | Role |
|---|---|
| `CustomAssert` | Extra xUnit assertions for null/empty checks on collections and strings |
| `UnitFactAttribute`, `UnitTheoryAttribute`, `FunctionalFactAttribute`, `FunctionalTheoryAttribute` | Test attributes that can skip tests per environment or on a condition |
| `FakeRepository<T, TKey>` | In-memory `IRepository<T, TKey>` implementation, for any entity key type |
| `AsyncFakeRepository<T, TKey>` | In-memory `IAsyncRepository<T, TKey>` implementation with cancellation support and async-capable `Query()` |
| `FakeScheduler` | Simulates a delayed command/query dispatch through a `CommandQueryMediator` |
| `WebApiTest<T>` | Base class for functional web API tests using an in-memory host |
| `TestException` | Exception raised by the utilities when a test-support operation fails |

## Quick Start

### Custom assertions

```csharp
CustomAssert.NullOrEmpty(collection);      // passes for null or empty
CustomAssert.NotNullOrEmpty(collection);   // passes only for a non-empty collection
CustomAssert.NullOrWhiteSpace(text);
CustomAssert.NotNullOrWhiteSpace(text);
```

### Environment-aware attributes

```csharp
// Runs everywhere except Production.
[UnitFact([EnvironmentType.Production])]
public void Calculates_totals() { /* ... */ }

// Skipped whenever the condition is true, on any environment.
[FunctionalFact(skipCondition: FeatureFlags.PaymentsDisabled)]
public void Charges_card() { /* ... */ }
```

### In-memory repository

```csharp
var repository = new FakeRepository<Person, long>();   // Person : Entity<long>

var id = repository.Create(new Person { Name = "Ann" }).Data;   // long and int ids start at 1
var person = repository.GetById(id).Data;
repository.Update(new Person { Id = id, Name = "Ann Smith" });
repository.Delete(new Person { Id = id });
```

### Functional web API test

```csharp
public class ProductsApiTests : WebApiTest<Program>
{
    public ProductsApiTests() : base(EnvironmentType.Local) { }

    [Fact]
    public async Task Creates_a_product()
    {
        await AuthenticateAndAuthorizeAsync(new Credentials("user@test.com", "secret123"), "/auth");

        var response = await Gateway.PostAsync<DataOutput<ProductOutput>>("/products", new { Name = "Widget" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

## Upgrading to 4.0

4.0 moves to `ArturRios.Data.Relational.Core` 5.0, where entities declare their key type (`Entity<TKey>`) and
the repository contracts take it as a second argument. The fakes follow: they implement
`IRepository<T, TKey>` / `IAsyncRepository<T, TKey>`, and the single-argument `FakeRepository<T>` and
`AsyncFakeRepository<T>` are removed. To keep the previous `long` keys, add `long` everywhere the old types appear:

| 3.x | 4.x |
|---|---|
| `class Person : Entity` | `class Person : Entity<long>` |
| `new FakeRepository<Person>()` | `new FakeRepository<Person, long>()` |
| `new AsyncFakeRepository<Person>()` | `new AsyncFakeRepository<Person, long>()` |
| `where T : Entity` (generic helpers over the fakes) | `where T : Entity<long>` |

Behavior for `long` keys is unchanged: ids are assigned sequentially from `1`, overwriting any id the caller set.
`int` keys behave the same way, `Guid` keys get `Guid.NewGuid()`, and any other key type (such as `string`) keeps the
caller-assigned id. Pass a `Func<TKey>` to the constructor to generate ids yourself — see
[Fakes](https://artur-rios.github.io/dotnet-test-util/docs/fakes/#how-ids-are-assigned).

4.0 also moves to `ArturRios.Util.WebApi` 5.1; `WebApiTest<T>` needed no change.

## Documentation

Full documentation, with per-component guides, lives at
[artur-rios.github.io/dotnet-test-util](https://artur-rios.github.io/dotnet-test-util/):

- [Assertions & Attributes](https://artur-rios.github.io/dotnet-test-util/docs/assertions-and-attributes/)
- [Fakes](https://artur-rios.github.io/dotnet-test-util/docs/fakes/)
- [Web API Testing](https://artur-rios.github.io/dotnet-test-util/docs/web-api-testing/)

## Testing

The test suite is xUnit, and every test is named with the Given / When / Then pattern. Every test class
carries a `Category` trait, so the two kinds can be run — and reported — separately:

```bash
dotnet test src/ArturRios.Util.Test.sln --filter "Category=Unit"
dotnet test src/ArturRios.Util.Test.sln --filter "Category=Functional"
```

Unit tests exercise the assertions, attributes and fakes in isolation.
Functional tests host the `sut/` web app in memory through `WebApiTest<Program>` and drive real HTTP
requests against it.
CI runs the two as separate jobs, and both must pass before a pull request can be merged.

`Category` is the same trait the `UnitFact`, `UnitTheory`, `FunctionalFact` and `FunctionalTheory`
attributes in this package stamp on your own tests — see
[Assertions and attributes](https://artur-rios.github.io/dotnet-test-util/docs/assertions-and-attributes/).
Use those in a project that references this package; a project that does not can put
`[Trait("Category", "Unit")]` on the class, as this repository's own tests do, and both filter the same way.

## Branching and releases

`develop` is the integration branch and the base for all new work; `main` only holds released code.

1. Branch off `develop` — `feature/<name>` for features, `fix/<name>` for fixes (`chore/`, `refactor/`, `docs/`,
   `ci/`, `test/`, `perf/` and `build/` are accepted too) — and open a pull request back into `develop`.
2. To release, cut `release/<version>` from `develop`, set `<Version>` in `src/ArturRios.Util.Test.csproj` to that version
   and open a pull request into `main`. Only `release/*` branches can be merged into `main`.
3. Once it is merged, tag the merge commit on `main` with the version. Pushing the tag publishes the package to
   nuget.org and GitHub Packages:

   ```bash
   git switch main && git pull
   git tag <version> && git push origin <version>
   ```

4. Open a pull request from `main` into `develop` to bring the release back into the integration branch.

Pull requests into `develop` and `main` must pass the tests and the branch policy check. Only the repository owner can
push version tags, and the publish workflow rejects tags that do not point at a commit on `main`.

## Versioning

Semantic Versioning (SemVer). Breaking changes result in a new major version. New methods or non-breaking behavior
changes increment the minor version; fixes or tweaks increment the patch.

## Build, test and publish

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project and Git for source control.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Legal Details

This project is licensed under the [MIT License](https://en.wikipedia.org/wiki/MIT_License). A copy of the license is available at [LICENSE](https://github.com/artur-rios/dotnet-test-util/blob/main/LICENSE) in the repository.

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
| `FakeRepository<T>` | In-memory `IRepository<T>` implementation |
| `AsyncFakeRepository<T>` | In-memory `IAsyncRepository<T>` implementation with cancellation support |
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
var repository = new FakeRepository<Person>();

var id = repository.Create(new Person { Name = "Ann" }).Data;   // ids start at 1
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

## Documentation

Full documentation, with per-component guides, lives at
[artur-rios.github.io/dotnet-test-util](https://artur-rios.github.io/dotnet-test-util/):

- [Assertions & Attributes](https://artur-rios.github.io/dotnet-test-util/assertions-and-attributes/)
- [Fakes](https://artur-rios.github.io/dotnet-test-util/fakes/)
- [Web API Testing](https://artur-rios.github.io/dotnet-test-util/web-api-testing/)

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

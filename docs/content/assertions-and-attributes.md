+++
title = "Assertions & Attributes"
show_nav       = true
nav_back_label = "Home"
nav_back_url   = "/dotnet-test-util"
nav_next_label = "Fakes"
nav_next_url   = "/dotnet-test-util/fakes"
+++

This page covers `CustomAssert` and the environment-aware test attributes.

## CustomAssert

`CustomAssert` is a static class with the null/empty assertions that come up most often, so you don't have to
spell out `Assert.True(collection is null || !collection.Any())` every time. Each method throws the same
exception type as a failing `Assert`, so failures read exactly like normal xUnit failures.

| Method | Passes when |
|---|---|
| `NullOrEmpty(IEnumerable?)` | The collection is `null` or has no elements |
| `NotNullOrEmpty(IEnumerable?)` | The collection is not `null` and has at least one element |
| `NullOrEmpty(string?)` | The string is `null` or empty |
| `NotNullOrEmpty(string?)` | The string is not `null` and not empty |
| `NullOrWhiteSpace(string?)` | The string is `null`, empty, or only white space |
| `NotNullOrWhiteSpace(string?)` | The string has at least one non-white-space character |

```csharp
CustomAssert.NullOrEmpty(Array.Empty<int>());     // passes
CustomAssert.NullOrEmpty((IEnumerable?)null);      // passes
CustomAssert.NotNullOrEmpty(new[] { 1, 2, 3 });    // passes

CustomAssert.NullOrWhiteSpace("   ");              // passes
CustomAssert.NotNullOrWhiteSpace("value");         // passes
```

The collection overloads treat `null` and empty the same way, so a `null` reference never trips up a
`NullOrEmpty` check.

## Environment-aware attributes

Four attributes wrap xUnit's `[Fact]` and `[Theory]` so a test can be skipped based on the active
environment or an explicit condition. Each also stamps a `Category` trait (`Unit` or `Functional`), so the
tests can be filtered by type, for example `dotnet test --filter "Category=Unit"`:

| Attribute | Wraps | Intended use | `Category` trait |
|---|---|---|---|
| `UnitFactAttribute` | `[Fact]` | Unit test | `Unit` |
| `UnitTheoryAttribute` | `[Theory]` | Data-driven unit test | `Unit` |
| `FunctionalFactAttribute` | `[Fact]` | Functional / integration test | `Functional` |
| `FunctionalTheoryAttribute` | `[Theory]` | Data-driven functional test | `Functional` |

All four share the same two optional parameters:

```csharp
UnitFact(EnvironmentType[]? environments = null, bool skipCondition = false)
```

- **`environments`** — the environments in which the test **must not** run. The current environment is read
  from the `ASPNETCORE_ENVIRONMENT` variable and compared case-insensitively. When it matches one of the
  listed environments, the test is skipped with the reason `Test can't run on {environment}`. `null` (the
  default) imposes no environment restriction.
- **`skipCondition`** — when `true`, the test is skipped regardless of the environment, with the reason
  `Condition to skip matched`.

```csharp
// Runs everywhere except Production.
[UnitFact([EnvironmentType.Production])]
public void Calculates_totals() { /* ... */ }

// Never runs while the feature flag is off, on any environment.
[FunctionalFact(skipCondition: FeatureFlags.PaymentsDisabled)]
public void Charges_card() { /* ... */ }

// Data-driven test blocked on Staging and Production.
[UnitTheory([EnvironmentType.Staging, EnvironmentType.Production])]
[InlineData(1)]
[InlineData(2)]
public void Validates_input(int value) { /* ... */ }
```

`EnvironmentType` comes from `ArturRios.Configuration.Enums` and defines `Local`, `Development`, `Staging`
and `Production`.

### How the skip decision is made

1. If `skipCondition` is `true`, the test is skipped.
2. Otherwise, if `environments` is `null` or empty, the test runs.
3. Otherwise, the test is skipped when the current `ASPNETCORE_ENVIRONMENT` matches one of the listed
   environments.

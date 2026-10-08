# Contributing

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Git

Use the official [.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/) to build, test and publish the project.
If you want, optional helper toolsets I built to facilitate these tasks are available:

- [Dotnet Tools](https://github.com/artur-rios/dotnet-tools)
- [Python Dotnet Tools](https://github.com/artur-rios/python-dotnet-tools)

## Build

```bash
dotnet build src/ArturRios.Util.Test.sln
```

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

The unit job also fails when a test has no `Category` trait, since no job would run it, and when
`dotnet format --verify-no-changes` finds a file to reformat; run `dotnet format src/ArturRios.Util.Test.sln` before pushing.

`Category` is the same trait the `UnitFact`, `UnitTheory`, `FunctionalFact` and `FunctionalTheory`
attributes in this package stamp on your own tests — see
[Assertions and attributes](https://artur-rios.github.io/dotnet-test-util/docs/assertions-and-attributes/).
Use those in a project that references this package; a project that does not can put
`[Trait("Category", "Unit")]` on the class, as this repository's own tests do, and both filter the same way.

## Branching and pull requests

`develop` is the integration branch and the base for all new work; `main` only holds released code.

Branch off `develop` — `feature/<name>` for features, `fix/<name>` for fixes (`feat/`, `bugfix/`, `chore/`, `refactor/`, `docs/`, `ci/`,
`test/`, `perf/` and `build/` are accepted too) — and open a pull request back into `develop`.

Dependabot's `dependabot/*` dependency-update branches are accepted into `develop` too.

Pull requests into `develop` and `main` must pass the tests and the branch policy check.

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) with a lowercase subject, e.g.
`feat: key the fake repositories by entity id type` or `fix: refresh the WebApi dependency and the auth helpers`.

Record every change a package consumer would notice under `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md), in the
same pull request that makes it.

## Versioning

`ArturRios.Util.Test` follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html). For a library the
version describes its public API and documented behaviour — what code referencing the package compiles against and
relies on at run time:

- **Major** — a breaking change: a public type or member removed or renamed, a signature changed, documented behaviour
  of an assertion, attribute, fake or `WebApiTest<T>` changed in a way existing tests can observe, a dependency update
  that changes the types the public API exposes (for example the entity and repository types of
  `ArturRios.Data.Relational.Core` that the fakes implement), or a higher target framework.
- **Minor** — a backwards-compatible addition: new types, members or overloads, or new behaviour that existing callers
  only get by opting in.
- **Patch** — a backwards-compatible fix that brings the behaviour in line with the documentation, or a dependency
  update that changes nothing for callers.

The version lives in one place, `<Version>` in `src/ArturRios.Util.Test.csproj`, which `dotnet pack` uses as the package
version. It is set by hand on the release branch (see [Releasing](#releasing)), and CI holds it to the release: the
branch policy check requires it to match the `release/<version>` branch name, and the publish workflow requires it to
match the tag.

## Releasing

1. Cut `release/<version>` from `develop`, set `<Version>` in `src/ArturRios.Util.Test.csproj` to that version,
   rename `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md) to `## [<version>] - <yyyy-mm-dd>` above a fresh, empty
   `## [Unreleased]`, update the compare links at the bottom, and open a pull request into `main`. Only `release/*`
   branches can be merged into `main`.
2. Once it is merged, tag the merge commit on `main` with the version. Pushing the tag publishes the package to
   nuget.org and GitHub Packages:

   ```bash
   git switch main && git pull
   git tag <version> && git push origin <version>
   ```

   Tag with the bare version, e.g. `1.3.0`. Some older releases were tagged with a `v` prefix
   (`v1.1.0`); those tags stay as they are, but new tags drop the prefix.

3. Open a pull request from `main` into `develop` to bring the release back into the integration branch.

Only the repository owner can push version tags, and the publish workflow rejects tags that do not point at a commit on
`main` or whose version differs from the one in the csproj.

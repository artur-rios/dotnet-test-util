# Contributing

This repository lives at <https://git.techcraftsmen.net/artur-rios/dotnet-test-util>; the GitHub repository is a read-only mirror of it, so
clone from there and open pull requests there:

```bash
git clone https://git.techcraftsmen.net/artur-rios/dotnet-test-util.git
```

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
The `test` check runs the two one after the other, and both must pass before a pull request can be merged. It is `ci/test.sh`, which you can run on your machine
as well.

The check also fails when a test has no `Category` trait, since no run would include it, and when
`dotnet format --verify-no-changes` finds a file to reformat; run `dotnet format src/ArturRios.Util.Test.sln` before pushing.

The `coverage` check (`ci/coverage.sh`) runs the tests with coverage collected and makes the HTML report that the docs site
serves at `/coverage-report/`.

`Category` is the same trait the `UnitFact`, `UnitTheory`, `FunctionalFact` and `FunctionalTheory`
attributes in this package stamp on your own tests — see
[Assertions and attributes](https://docs.artur-rios.tech/dotnet-test-util/docs/assertions-and-attributes/).
Use those in a project that references this package; a project that does not can put
`[Trait("Category", "Unit")]` on the class, as this repository's own tests do, and both filter the same way.

## Branching and pull requests

`develop` is the integration branch and the base for all new work; `main` only holds released code.

Branch off `develop` — `feature/<name>` for features, `fix/<name>` for fixes (lowercase letters, digits, `.`, `_` and `-` in the
name) — and open a pull request back into `develop`. Renovate's `renovate/*` dependency-update branches are accepted into
`develop` too, and `main` only receives `release/<version>` pull requests.

Pull requests into `develop` and `main` must pass the `test` and `coverage` checks and the branch policy check, which Jenkins runs
(`yggdrasil.yml` and `ci/` describe them).

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
version. It is set by hand on `develop` just before a release (see [Releasing](#releasing)), and CI holds it to the release: the branch
policy check (`ci/branch-policy.sh`) requires it to match the `release/<version>` branch name, and packing (`ci/pack.sh`)
requires it to match the tag.

## Releasing

1. A release branch is a snapshot of `develop` and carries no commits of its own, so prepare the release on `develop` first, in a
   `feature/` or `fix/` branch merged like any other: set `<Version>` in `src/ArturRios.Util.Test.csproj` to the new version, rename
   `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md) to `## [<version>] - <yyyy-mm-dd>` above a fresh, empty
   `## [Unreleased]`, and update the compare links at the bottom.
2. Cut `release/<version>` from the updated `develop`, push it and open a pull request into `main`. Only `release/*`
   branches can be merged into `main`, and the branch policy check rejects a branch that carries a commit `develop` does not have or whose `<Version>` in the csproj is
   not the branch's version.
3. Once its checks have passed, the owner merges it with a merge commit. Then tag the merge commit on `main` with the version,
   annotated. Pushing the tag publishes the package to nuget.org:

   ```bash
   git switch main && git pull
   git tag -a <version> -m <version> && git push origin <version>
   ```

   Tag with the bare version, e.g. `1.3.0`. Some older releases were tagged with a `v` prefix
   (`v1.1.0`); those tags stay as they are, but new tags drop the prefix.

Only the repository owner can push version tags. Jenkins builds the tag, which must point at a commit on `main`, packs the library
with `ci/pack.sh` (it fails when the tag's version differs from `<Version>` in the csproj) and pushes the package to nuget.org.

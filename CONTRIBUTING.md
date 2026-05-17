# Contributing to Markus

Thanks for considering a contribution. This project aims for very high code quality, so a few conventions are enforced automatically.

## Prerequisites

- .NET SDK 10.0.101 or newer (pinned via `global.json`)
- Git 2.40+ (for modern hook support)

## Bootstrap

```bash
git clone git@github.com:cihanyakar/Markus.git
cd Markus
dotnet tool restore     # installs CSharpier and Husky locally
dotnet restore          # also triggers Husky to install git hooks
```

After this, `git commit` and `git push` will run quality gates locally.

## Daily workflow

```bash
dotnet csharpier format .   # format the whole repo
dotnet build                # treat warnings as errors
dotnet test                 # run unit tests
dotnet run --project src/Markus
```

## Quality gates

- **Formatting**: CSharpier (`dotnet csharpier check .`)
- **Analyzers**: StyleCop, Meziantou, SonarAnalyzer, Roslynator, NetAnalyzers
- **Build**: `TreatWarningsAsErrors=true` everywhere
- **Tests**: xUnit v3 (`tests/Markus.Tests/`), Shouldly assertions, NSubstitute mocks

Pre-commit hook runs CSharpier check on staged `.cs` files.
Pre-push hook runs a full Release build plus a repo-wide format check.
CI additionally runs the full test suite on Linux, macOS, and Windows.

## Commit messages: Conventional Commits

Format:

```
<type>(<optional scope>)!?: <subject>
```

Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.

Examples:

```
feat: add markdown file picker
fix(viewer): handle empty document
chore!: drop .NET 9 support
docs(readme): add screenshot
```

The `commit-msg` hook enforces this.

## Pull requests

- Keep PRs focused. One feature or fix per PR.
- All CI checks must pass on Linux, macOS, and Windows.
- Reference any related issue in the PR description.

## Branching and releases

### Branches

- **`main`**: active development. Always green. New work lands here via PR.
- **`release/X.Y`**: long-lived per-minor branch (e.g. `release/1.0`, `release/1.1`).
  Created when a minor line ships. Hotfixes target the right `release/X.Y`,
  then are cherry-picked into `main` if they still apply.

### Versioning

[SemVer](https://semver.org). Versions are computed from git tags by
[MinVer](https://github.com/adamralph/minver). No manual `<Version>` edits.

- Tag form: `vMAJOR.MINOR.PATCH` (e.g. `v0.1.0`, `v1.2.3`).
- Between tags, builds get an automatic prerelease (e.g. `1.2.4-alpha.0.5`).
- While pre-1.0, breaking changes bump **minor**, not major
  (configured in `release-please-config.json`).

### Release flow

1. Land Conventional Commits on `main` (or a `release/X.Y` branch for hotfixes).
2. [release-please](https://github.com/googleapis/release-please) opens a
   "release PR" titled `chore: release X.Y.Z` with the proposed version bump
   and an updated `CHANGELOG.md`.
3. Review and merge the release PR.
4. release-please creates a git tag (`vX.Y.Z`) and a GitHub Release.
5. The `Release binaries` workflow publishes self-contained executables for
   `win-x64`, `osx-x64`, `osx-arm64`, `linux-x64` and attaches them
   (with `.sha256` checksums) to the GitHub Release.

### Hotfix recipe

```bash
git switch -c fix/short-description release/X.Y
# ... fix + commit (conventional commits)
git push -u origin fix/short-description
# open PR targeting release/X.Y
# release-please then opens a patch release PR on release/X.Y
# after merge, cherry-pick to main if still relevant
git switch main && git pull
git cherry-pick <commit-sha>
```

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

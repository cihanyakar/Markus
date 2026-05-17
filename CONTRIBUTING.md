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

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

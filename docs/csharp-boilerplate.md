# C# / .NET 10 / Avalonia Boilerplate

A reusable starter kit for new open-source desktop applications. This is the exact stack the Markus repository was bootstrapped with. Copy the files, change the names, and you have a production-grade project skeleton in minutes.

## Stack at a glance

| Layer | Choice | Version (May 2026) |
|---|---|---|
| SDK | .NET | 10.0.101 |
| Language | C# | latest (C# 14) |
| UI | Avalonia | 12.0.3 |
| MVVM | CommunityToolkit.Mvvm | 8.4.1 |
| Annotations | JetBrains.Annotations | 2025.2.4 |
| Test framework | xUnit v3 (Microsoft Testing Platform) | 3.2.2 |
| Assertions | Shouldly | 4.3.0 |
| Mocks | NSubstitute | 5.3.0 |
| Formatter | CSharpier | 1.2.6 |
| Hooks | Husky.Net | 0.9.1 |
| Analyzers | StyleCop / Meziantou / Sonar / Roslynator / VSTHRD / NetAnalyzers | latest stable |
| Solution format | `.slnx` (XML, .NET 10 default) | — |
| Package management | Central Package Management (CPM) | — |
| License | MIT | — |

## Directory layout

```
RepoRoot/
├── .config/
│   └── dotnet-tools.json          # CSharpier, Husky pinned locally
├── .editorconfig                  # Strict, modern, Python-tight indentation
├── .gitattributes                 # LF normalization (critical for hooks)
├── .github/
│   ├── dependabot.yml             # Weekly NuGet + Actions updates, grouped
│   └── workflows/
│       └── ci.yml                 # 3-OS matrix CI
├── .gitignore                     # `dotnet new gitignore`
├── .husky/
│   ├── _/                         # Husky internals
│   ├── commit-msg                 # Conventional Commits enforcement
│   ├── pre-commit                 # CSharpier check (staged .cs only)
│   ├── pre-push                   # Build + Test + repo-wide format check
│   └── task-runner.json
├── Directory.Build.props          # Language, quality, analyzers, packaging
├── Directory.Packages.props       # CPM versions
├── LICENSE                        # MIT
├── README.md                      # Project entry, CI badge
├── CONTRIBUTING.md                # Bootstrap, conventions
├── <Solution>.slnx                # XML solution
├── stylecop.json                  # StyleCop behavior
├── docs/                          # Documentation (this file lives here)
├── src/
│   └── <App>/                     # Main project
└── tests/
    └── <App>.Tests/               # xUnit v3 tests
```

## Bootstrap recipe

```bash
# 1. SDK
dotnet --version          # confirm 10.x

# 2. Avalonia templates (one-time)
dotnet new install Avalonia.Templates

# 3. Create solution + project
dotnet new sln -n <App>
dotnet new avalonia.mvvm -n <App> -o src/<App>
dotnet sln <App>.slnx add src/<App>/<App>.csproj

# 4. Standard files
dotnet new gitignore
dotnet new editorconfig          # then replace with the strict version below

# 5. Local tools
dotnet new tool-manifest         # creates .config/dotnet-tools.json
dotnet tool install CSharpier --version 1.2.6
dotnet tool install Husky --version 0.9.1

# 6. Git
git init -b main
git config user.email "<you>@example.com"
git config user.name "Your Name"

# 7. Husky hooks
dotnet husky install             # also wires git core.hooksPath

# 8. Drop the config files below into place, then:
dotnet build
dotnet test
dotnet csharpier check .
git add . && git commit -m "feat: bootstrap project"
gh repo create <user>/<App> --public --source=. --remote=origin --push
```

## Configuration files (copy verbatim)

Each block below is the exact content from the Markus repo. Replace `<App>` with your project name where appropriate.

### `global.json`

```json
{
  "sdk": {
    "version": "10.0.101",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

### `Directory.Build.props`

```xml
<Project>

  <PropertyGroup Label="Language">
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup Label="Quality">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <PropertyGroup Label="Build">
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <PropertyGroup Label="Packaging">
    <Authors>Your Name</Authors>
    <Product><App></Product>
    <Copyright>Copyright (c) $([System.DateTime]::UtcNow.Year) Your Name</Copyright>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup Label="Analyzers" Condition="'$(IsAnalyzerProject)' != 'true'">
    <PackageReference Include="StyleCop.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Meziantou.Analyzer">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="SonarAnalyzer.CSharp">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Roslynator.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Roslynator.Formatting.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.VisualStudio.Threading.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup Label="StyleCopConfig">
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)stylecop.json" Link="stylecop.json" Visible="false" />
  </ItemGroup>

</Project>
```

### `Directory.Packages.props`

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup Label="Runtime">
    <PackageVersion Include="Avalonia" Version="12.0.3" />
    <PackageVersion Include="Avalonia.Desktop" Version="12.0.3" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="12.0.3" />
    <PackageVersion Include="Avalonia.Fonts.Inter" Version="12.0.3" />
    <PackageVersion Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.1" />
  </ItemGroup>

  <ItemGroup Label="Annotations">
    <PackageVersion Include="JetBrains.Annotations" Version="2025.2.4" />
  </ItemGroup>

  <ItemGroup Label="Analyzers">
    <PackageVersion Include="StyleCop.Analyzers" Version="1.1.118" />
    <PackageVersion Include="Meziantou.Analyzer" Version="3.0.85" />
    <PackageVersion Include="SonarAnalyzer.CSharp" Version="10.25.0.139117" />
    <PackageVersion Include="Roslynator.Analyzers" Version="4.15.0" />
    <PackageVersion Include="Roslynator.Formatting.Analyzers" Version="4.15.0" />
    <PackageVersion Include="Microsoft.VisualStudio.Threading.Analyzers" Version="17.14.15" />
  </ItemGroup>

  <ItemGroup Label="Testing">
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>

</Project>
```

### Main app csproj (`src/<App>/<App>.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <Folder Include="Models\" />
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" />
    <PackageReference Include="Avalonia.Desktop" />
    <PackageReference Include="Avalonia.Themes.Fluent" />
    <PackageReference Include="Avalonia.Fonts.Inter" />
    <PackageReference Include="AvaloniaUI.DiagnosticsSupport">
      <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
      <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
    </PackageReference>
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="JetBrains.Annotations">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup Label="FriendAssemblies">
    <InternalsVisibleTo Include="<App>.Tests" />
    <InternalsVisibleTo Include="<App>.UI.Tests" />
  </ItemGroup>

  <Target Name="HuskyInstall" BeforeTargets="Restore;CollectPackageReferences" Condition="'$(HUSKY)' != '0' AND '$(CI)' != 'true'">
    <Exec Command="dotnet tool restore" StandardOutputImportance="Low" StandardErrorImportance="High" WorkingDirectory="$(MSBuildThisFileDirectory)../../" ContinueOnError="true" />
    <Exec Command="dotnet husky install" StandardOutputImportance="Low" StandardErrorImportance="High" WorkingDirectory="$(MSBuildThisFileDirectory)../../" ContinueOnError="true" />
  </Target>
</Project>
```

### Test csproj (`tests/<App>.Tests/<App>.Tests.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <NoWarn>$(NoWarn);CA1707;CA2007</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="JetBrains.Annotations">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\<App>\<App>.csproj" />
  </ItemGroup>

</Project>
```

### `tests/<App>.Tests/GlobalUsings.cs`

```csharp
global using Shouldly;
global using Xunit;
```

### `.config/dotnet-tools.json`

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "csharpier": { "version": "1.2.6", "commands": ["csharpier"], "rollForward": false },
    "husky":     { "version": "0.9.1", "commands": ["husky"],     "rollForward": false }
  }
}
```

### `.gitattributes`

```
* text=auto eol=lf

*.sh           text eol=lf
.husky/*       text eol=lf

*.bat          text eol=crlf
*.cmd          text eol=crlf
*.ps1          text eol=crlf

*.png  binary
*.jpg  binary
*.jpeg binary
*.gif  binary
*.ico  binary
*.icns binary
*.snk  binary
*.pdf  binary
*.woff binary
*.woff2 binary
*.ttf  binary
*.otf  binary

*.cs       text diff=csharp
*.axaml    text
*.xaml     text
*.csproj   text
*.slnx     text
*.props    text
*.targets  text
```

### `stylecop.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/DotNetAnalyzers/StyleCopAnalyzers/master/StyleCop.Analyzers/StyleCop.Analyzers/Settings/stylecop.schema.json",
  "settings": {
    "documentationRules": {
      "companyName": "Your Name",
      "copyrightText": "",
      "xmlHeader": false,
      "documentInterfaces": true,
      "documentExposedElements": false,
      "documentInternalElements": false,
      "documentPrivateElements": false,
      "documentPrivateFields": false
    },
    "orderingRules": {
      "usingDirectivesPlacement": "outsideNamespace",
      "systemUsingDirectivesFirst": true,
      "blankLinesBetweenUsingGroups": "allow"
    },
    "layoutRules": {
      "newlineAtEndOfFile": "require",
      "allowConsecutiveUsings": true
    },
    "readabilityRules": {
      "allowBuiltInTypeAliases": false
    }
  }
}
```

### `.husky/task-runner.json`

```json
{
  "$schema": "https://alirezanet.github.io/Husky.Net/schema.json",
  "tasks": [
    {
      "name": "csharpier-check-staged",
      "group": "pre-commit",
      "command": "dotnet",
      "args": ["csharpier", "check", "${staged}"],
      "include": ["**/*.cs"]
    },
    {
      "name": "build-release",
      "group": "pre-push",
      "command": "dotnet",
      "args": ["build", "<App>.slnx", "--configuration", "Release", "--nologo"]
    },
    {
      "name": "test-release",
      "group": "pre-push",
      "command": "dotnet",
      "args": ["test", "<App>.slnx", "--configuration", "Release", "--no-build", "--nologo"]
    },
    {
      "name": "csharpier-check-all",
      "group": "pre-push",
      "command": "dotnet",
      "args": ["csharpier", "check", "."]
    }
  ]
}
```

### `.husky/pre-commit`

```sh
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"

dotnet husky run --group pre-commit
```

### `.husky/pre-push`

```sh
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"

dotnet husky run --group pre-push
```

### `.husky/commit-msg`

```sh
#!/usr/bin/env sh
. "$(dirname -- "$0")/_/husky.sh"

commit_msg=$(head -n 1 "$1")
pattern='^(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\([a-z0-9 ,_/-]+\))?!?: .{1,72}$'

case "$commit_msg" in
  "Merge "*|"Revert "*|"fixup! "*|"squash! "*|"amend! "*)
    exit 0
    ;;
esac

if ! printf '%s' "$commit_msg" | grep -qE "$pattern"; then
  printf '\n\033[31mERROR\033[0m: commit message does not follow Conventional Commits.\n\n'
  printf 'Format: <type>(<optional scope>)!?: <subject>\n\n'
  printf 'Examples:\n'
  printf '  feat: add markdown file picker\n'
  printf '  fix(viewer): handle empty document\n'
  printf '  chore!: drop .NET 9 support\n\n'
  printf 'Allowed types: build, chore, ci, docs, feat, fix, perf, refactor, revert, style, test\n'
  printf 'Your message: %s\n' "$commit_msg"
  exit 1
fi
```

Make the hooks executable: `chmod +x .husky/pre-commit .husky/pre-push .husky/commit-msg`.

### `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  HUSKY: 0

permissions:
  contents: read

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: Build & Test (${{ matrix.os }})
    runs-on: ${{ matrix.os }}
    timeout-minutes: 15
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj', 'global.json') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore packages
        run: dotnet restore <App>.slnx

      - name: Format check (CSharpier)
        run: dotnet csharpier check .

      - name: Build (Release, warnings-as-errors)
        run: dotnet build <App>.slnx --configuration Release --no-restore

      - name: Test (Release)
        run: dotnet test <App>.slnx --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.os }}
          path: '**/TestResults/*.trx'
          retention-days: 14
```

### `.github/dependabot.yml`

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: /
    schedule:
      interval: weekly
      day: monday
    open-pull-requests-limit: 10
    commit-message:
      prefix: chore
      include: scope
    labels:
      - dependencies
      - nuget
    groups:
      avalonia:
        patterns:
          - "Avalonia*"
      analyzers:
        patterns:
          - "StyleCop.Analyzers"
          - "Meziantou.Analyzer"
          - "SonarAnalyzer.*"
          - "Roslynator.*"

  - package-ecosystem: github-actions
    directory: /
    schedule:
      interval: weekly
      day: monday
    open-pull-requests-limit: 5
    commit-message:
      prefix: ci
      include: scope
    labels:
      - dependencies
      - github-actions
```

## C# code style decisions (`.editorconfig`)

| Rule | Setting | Severity |
|---|---|---|
| **Braces** | Always required for `if/else/for/while/foreach/using/lock`, single-line forbidden | error |
| **Brace style** | Allman (open brace on new line) | enforced by CSharpier |
| **Indent** | 4 spaces only; tabs forbidden; mixed indentation forbidden; trailing whitespace forbidden | error |
| **End of line** | LF; final newline required | error |
| **Namespace** | File-scoped (`namespace X;`) required | error |
| **`var`** | Only when type is apparent (`new T()`, casts, factory methods named like the type) | error |
| **Expression-bodied** | Property / accessor / ctor → OK; methods / operators → block required | warning |
| **Member ordering** | StyleCop standard: const → field → ctor → prop → method; public → private; static → instance; readonly → mutable | error |
| **Sealed by default** | Internal types must be `sealed` or explicitly `abstract` (CA1852) | error |
| **Fields** | `readonly` required if not reassigned; private fields `_camelCase`; `this.` forbidden | error |
| **Type names** | `int`/`string`/`bool` keywords, BCL forms forbidden | error |
| **Pattern matching** | `is + cast` forbidden, `is null` preferred, switch expressions encouraged | warning |
| **Usings** | Outside namespace, System first, alphabetical | error |
| **Async** | `async void` forbidden; `Async` suffix required (warning); no sync-over-async | error |
| **ConfigureAwait** | Off (desktop app uses UI sync context) | none |
| **XML docs** | Off (apps don't need them; turn on if it becomes a library) | none |
| **Complexity** | Sonar/Meziantou defaults (S138, S1541, S107, MA0051) | warning |
| **IDE0008** | Disabled (conflicts with IDE0007 on built-in types) | none |
| **Code formatting** | Owned by CSharpier; many `SA1xxx` (SA1009/SA1111/SA1116/SA1117/SA1500/SA1502/SA1515/SA1518) explicitly disabled | none |

The full `.editorconfig` is ~280 lines; copy it verbatim from the Markus repo.

## Conventional Commits

```
<type>(<optional scope>)!?: <subject up to 72 chars>
```

Allowed types: `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert`, `style`, `test`.

Examples:

```
feat: add markdown file picker
fix(viewer): handle empty document
chore!: drop .NET 9 support
docs(readme): add screenshot
test: bootstrap xUnit v3 with Shouldly and NSubstitute
```

Enforced by `.husky/commit-msg`. Merge, revert, fixup, squash, and amend commits are exempt.

## Daily workflow

```bash
dotnet csharpier format .          # format
dotnet build                        # warnings = errors
dotnet test                         # xUnit v3
dotnet run --project src/<App>     # run the app

# commit
git commit -m "feat: ..."          # commit-msg + pre-commit hooks fire
git push                            # pre-push: build + test + format check
```

## Quality gates (where each one runs)

| Gate | Local pre-commit | Local pre-push | CI |
|---|:-:|:-:|:-:|
| CSharpier format check (staged) | ✓ | — | — |
| CSharpier format check (repo) | — | ✓ | ✓ |
| Build (Release, warnings-as-errors) | — | ✓ | ✓ |
| Test (Release) | — | ✓ | ✓ |
| Conventional Commits | ✓ (commit-msg) | — | — |
| Multi-OS (Linux / macOS / Windows) | — | — | ✓ |

## Notes and gotchas

- `.slnx` is the .NET 10 default solution format. CI and local `dotnet` commands accept it natively.
- `IDE0007` (use `var`) and `IDE0008` (use explicit type) conflict on `something.Replace(...)` patterns because Roslyn marks the call as "apparent" *and* the result as "built-in". We resolve by dropping the built-in-type rule and setting `IDE0008` to `none`.
- Hooks must use LF line endings or they will fail silently on Windows. `.gitattributes` handles this.
- The `HuskyInstall` MSBuild target in the main `.csproj` auto-installs Husky on `dotnet restore` (skipped when `HUSKY=0` or `CI=true`).
- `TreatWarningsAsErrors=true` plus `EnforceCodeStyleInBuild=true` means style violations break the build; adjust severity carefully.
- Test projects use Microsoft Testing Platform runner (`UseMicrosoftTestingPlatformRunner=true`); the test project is an `Exe` and can be run directly with `dotnet run`.
- `InternalsVisibleTo` in the main csproj lets tests reach internal types without changing visibility.
- Avalonia partial classes (XAML code-behind) are kept `internal sealed`; CA1852 is locally disabled for `*.axaml.cs` to avoid edge cases with generated partials.

## Related artifacts in the Markus repo

- `Markus.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` at the root.
- `.husky/` for hooks, `.github/` for CI and Dependabot.
- `src/Markus/` is the canonical example of the main project layout.
- `tests/Markus.Tests/` is the canonical example of the test project.

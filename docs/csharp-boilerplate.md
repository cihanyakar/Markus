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
| Versioning (from git tags) | MinVer | 7.0.0 |
| Test framework | xUnit v3 (Microsoft Testing Platform) | 3.2.2 |
| Assertions | Shouldly | 4.3.0 |
| Mocks | NSubstitute | 5.3.0 |
| Formatter | CSharpier | 1.2.6 |
| Hooks | Husky.Net | 0.9.1 |
| Analyzers | StyleCop / Meziantou / Sonar / Roslynator / VSTHRD / NetAnalyzers | latest stable |
| Release automation | googleapis/release-please-action | v5 |
| Release artifact upload | softprops/action-gh-release | v3 |
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
│       ├── ci.yml                 # 3-OS matrix CI
│       ├── release-please.yml     # Auto changelog + release PR
│       └── release-binaries.yml   # Build & upload cross-platform binaries
├── .gitignore
├── .husky/
│   ├── _/
│   ├── commit-msg                 # Conventional Commits enforcement
│   ├── pre-commit                 # CSharpier check (staged .cs only)
│   ├── pre-push                   # Build + Test + repo-wide format check
│   └── task-runner.json
├── .release-please-manifest.json  # Current released version (release-please)
├── CHANGELOG.md                   # Maintained automatically
├── CONTRIBUTING.md
├── Directory.Build.props          # Language, quality, analyzers, packaging, MinVer
├── Directory.Packages.props       # CPM versions
├── LICENSE                        # MIT
├── README.md                      # Project entry, CI + Release badges
├── <Solution>.slnx                # XML solution
├── release-please-config.json     # release-please rules
├── stylecop.json
├── docs/
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
dotnet new editorconfig          # then replace with the strict version

# 5. Local tools
dotnet new tool-manifest
dotnet tool install CSharpier --version 1.2.6
dotnet tool install Husky --version 0.9.1

# 6. Git
git init -b main
git config user.email "<you>@example.com"
git config user.name "Your Name"

# 7. Husky hooks
dotnet husky install

# 8. Drop the config files below into place, then:
dotnet build
dotnet test
dotnet csharpier check .
git add . && git commit -m "feat: bootstrap project"

# 9. GitHub repo (public, open source)
gh repo create <user>/<App> --public --source=. --remote=origin --push

# 10. One-time: allow Actions to open PRs (required by release-please)
gh api -X PUT repos/<user>/<App>/actions/permissions/workflow \
  -F default_workflow_permissions=write \
  -F can_approve_pull_request_reviews=true
```

After step 10, the next push to `main` triggers release-please, which opens a "release PR" with the proposed version and CHANGELOG. Merging that PR creates the tag and the GitHub Release; the release event then triggers binary publishing.

## Configuration files (copy verbatim)

Each block below is the exact content from the Markus repo. Replace `<App>` with your project name.

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

  <PropertyGroup Label="Versioning">
    <!-- Version is derived from the most recent `v*` git tag by MinVer. -->
    <MinVerTagPrefix>v</MinVerTagPrefix>
    <MinVerDefaultPreReleaseIdentifiers>alpha.0</MinVerDefaultPreReleaseIdentifiers>
    <MinVerVerbosity>quiet</MinVerVerbosity>
  </PropertyGroup>

  <ItemGroup Label="Versioning" Condition="'$(IsAnalyzerProject)' != 'true'">
    <PackageReference Include="MinVer">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

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

  <ItemGroup Label="Versioning">
    <PackageVersion Include="MinVer" Version="7.0.0" />
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

### `release-please-config.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
  "release-type": "simple",
  "include-component-in-tag": false,
  "tag-separator": "",
  "separate-pull-requests": false,
  "changelog-sections": [
    { "type": "feat", "section": "Features" },
    { "type": "fix", "section": "Bug Fixes" },
    { "type": "perf", "section": "Performance" },
    { "type": "refactor", "section": "Refactoring" },
    { "type": "docs", "section": "Documentation" },
    { "type": "build", "section": "Build" },
    { "type": "ci", "section": "Continuous Integration" },
    { "type": "test", "section": "Tests" },
    { "type": "style", "section": "Style", "hidden": true },
    { "type": "chore", "section": "Chores", "hidden": true }
  ],
  "packages": {
    ".": {
      "package-name": "<App>",
      "release-type": "simple",
      "changelog-path": "CHANGELOG.md",
      "initial-version": "0.1.0",
      "bump-minor-pre-major": true,
      "bump-patch-for-minor-pre-major": false,
      "draft": false,
      "prerelease": false,
      "include-v-in-tag": true
    }
  }
}
```

### `.release-please-manifest.json`

```json
{
  ".": "0.0.0"
}
```

After the first release PR is merged, release-please updates this to the released version.

### `CHANGELOG.md` (seed; release-please maintains thereafter)

```markdown
# Changelog

All notable changes to this project will be documented in this file.

This file is maintained automatically by [release-please](https://github.com/googleapis/release-please).
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
```

### `.github/workflows/release-please.yml`

```yaml
name: release-please

on:
  push:
    branches:
      - main
      - release/**

permissions:
  contents: write
  pull-requests: write

concurrency:
  group: release-please-${{ github.ref }}
  cancel-in-progress: false

jobs:
  release-please:
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - uses: googleapis/release-please-action@v5
        with:
          config-file: release-please-config.json
          manifest-file: .release-please-manifest.json
          target-branch: ${{ github.ref_name }}
```

### `.github/workflows/release-binaries.yml`

```yaml
name: Release binaries

on:
  release:
    types: [published]

env:
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  HUSKY: 0
  TAG_NAME: ${{ github.event.release.tag_name }}

permissions:
  contents: write

concurrency:
  group: release-binaries-${{ github.event.release.tag_name }}
  cancel-in-progress: false

jobs:
  build:
    name: Publish ${{ matrix.rid }}
    runs-on: ${{ matrix.os }}
    timeout-minutes: 20
    strategy:
      fail-fast: false
      matrix:
        include:
          - rid: win-x64
            os: windows-latest
            archive: zip
          - rid: osx-x64
            os: macos-13
            archive: tar.gz
          - rid: osx-arm64
            os: macos-latest
            archive: tar.gz
          - rid: linux-x64
            os: ubuntu-latest
            archive: tar.gz

    steps:
      - name: Checkout (tag, full history for MinVer)
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.release.tag_name }}
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore
        run: dotnet restore src/<App>/<App>.csproj -r ${{ matrix.rid }}

      - name: Publish self-contained single file
        shell: bash
        run: |
          dotnet publish src/<App>/<App>.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained true \
            --no-restore \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=false \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:DebugType=embedded \
            -o publish/${{ matrix.rid }}

      - name: Archive (tar.gz)
        if: matrix.archive == 'tar.gz'
        shell: bash
        env:
          ARCHIVE_NAME: <App>-${{ github.event.release.tag_name }}-${{ matrix.rid }}.tar.gz
        run: |
          tar -czf "$ARCHIVE_NAME" -C "publish/${{ matrix.rid }}" .
          echo "ARTIFACT=$ARCHIVE_NAME" >> "$GITHUB_ENV"

      - name: Archive (zip)
        if: matrix.archive == 'zip'
        shell: pwsh
        env:
          ARCHIVE_NAME: <App>-${{ github.event.release.tag_name }}-${{ matrix.rid }}.zip
        run: |
          Compress-Archive -Path "publish/${{ matrix.rid }}/*" -DestinationPath "$env:ARCHIVE_NAME"
          "ARTIFACT=$env:ARCHIVE_NAME" >> $env:GITHUB_ENV

      - name: Compute SHA256 (Linux/macOS)
        if: matrix.archive == 'tar.gz'
        shell: bash
        env:
          ART: ${{ env.ARTIFACT }}
        run: |
          if [ "$RUNNER_OS" = "macOS" ]; then
            shasum -a 256 "$ART" > "$ART.sha256"
          else
            sha256sum "$ART" > "$ART.sha256"
          fi

      - name: Compute SHA256 (Windows)
        if: matrix.archive == 'zip'
        shell: pwsh
        env:
          ART: ${{ env.ARTIFACT }}
        run: |
          $h = Get-FileHash -Path $env:ART -Algorithm SHA256
          "$($h.Hash.ToLower())  $env:ART" | Out-File -Encoding ASCII "$env:ART.sha256"

      - name: Upload to GitHub Release
        uses: softprops/action-gh-release@v3
        with:
          tag_name: ${{ github.event.release.tag_name }}
          files: |
            ${{ env.ARTIFACT }}
            ${{ env.ARTIFACT }}.sha256
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

The full `.editorconfig` is ~280 lines; copy it verbatim from the [Markus repo](https://github.com/cihanyakar/Markus/blob/main/.editorconfig).

## Branching and releases

### Branch model

| Branch | Purpose |
|---|---|
| `main` | Active development. Always green. PR-driven. |
| `release/X.Y` | Long-lived per-minor support branch (e.g. `release/1.0`). Created on demand for hotfixes. |
| Feature branches | Short-lived, named `feat/...`, `fix/...`, etc. |

### Versioning

[SemVer](https://semver.org). The version in built assemblies is computed by [MinVer](https://github.com/adamralph/minver) from the closest reachable `v*` git tag. There is no manual `<Version>` in any csproj.

- Released form: `vMAJOR.MINOR.PATCH` (e.g. `v0.1.0`).
- Between tags: builds get an automatic prerelease suffix (e.g. `0.2.0-alpha.0.5`).
- Pre-1.0: `bump-minor-pre-major: true` makes "breaking" Conventional Commits (`feat!`) bump minor instead of major.

### Release flow (release-please)

1. Land Conventional Commits on `main` (or `release/X.Y`).
2. release-please opens a PR titled `chore: release X.Y.Z` with the proposed bump and an updated `CHANGELOG.md`.
3. Review and merge the release PR.
4. release-please tags the merge commit (`vX.Y.Z`) and publishes a GitHub Release.
5. The `Release binaries` workflow runs on the `release: published` event and uploads `win-x64`, `osx-x64`, `osx-arm64`, `linux-x64` self-contained archives plus `.sha256` checksums.

### Hotfix recipe

```bash
git switch -c fix/short-description release/X.Y
# implement + commit (Conventional Commits)
git push -u origin fix/short-description
# open PR targeting release/X.Y

# After merge, release-please opens a patch release PR on release/X.Y.
# Once merged, the tag and Release fire automatically.

# Bring the fix to main if still relevant:
git switch main && git pull
git cherry-pick <commit-sha>
```

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
# release-please then opens / updates the release PR automatically
```

## Quality and release gates

| Gate | Local pre-commit | Local pre-push | CI | Release |
|---|:-:|:-:|:-:|:-:|
| Conventional Commits | ✓ (commit-msg) | — | — | — |
| CSharpier format check (staged) | ✓ | — | — | — |
| CSharpier format check (repo) | — | ✓ | ✓ | — |
| Build (Release, warnings-as-errors) | — | ✓ | ✓ | ✓ |
| Test (Release) | — | ✓ | ✓ | — |
| Multi-OS (Linux / macOS / Windows) | — | — | ✓ | ✓ |
| Auto changelog + version PR | — | — | — | ✓ (release-please) |
| Cross-platform self-contained binaries | — | — | — | ✓ (release-binaries) |
| SHA256 checksums on artifacts | — | — | — | ✓ |

## Notes and gotchas

- `.slnx` is the .NET 10 default solution format. CI and local `dotnet` commands accept it natively.
- `IDE0007` (use `var`) and `IDE0008` (use explicit type) conflict on `something.Replace(...)` patterns because Roslyn marks the call as "apparent" *and* the result as "built-in". We resolve by dropping the built-in-type rule and setting `IDE0008` to `none`.
- Hooks must use LF line endings or they will fail silently on Windows. `.gitattributes` handles this.
- The `HuskyInstall` MSBuild target in the main `.csproj` auto-installs Husky on `dotnet restore` (skipped when `HUSKY=0` or `CI=true`).
- `TreatWarningsAsErrors=true` plus `EnforceCodeStyleInBuild=true` means style violations break the build; adjust severity carefully.
- Test projects use Microsoft Testing Platform runner (`UseMicrosoftTestingPlatformRunner=true`); the test project is an `Exe` and can be run directly with `dotnet run`.
- `InternalsVisibleTo` in the main csproj lets tests reach internal types without changing visibility.
- Avalonia partial classes (XAML code-behind) are kept `internal sealed`; CA1852 is locally disabled for `*.axaml.cs` to avoid edge cases with generated partials.
- **release-please requires write permission + "allow Actions to create PRs"** (see step 10 of bootstrap). Without it the first run will create the release branch but fail to open the PR.
- **MinVer needs the full git history** to find tags. CI checkouts that build for publishing must use `fetch-depth: 0`.
- **`bump-minor-pre-major: true`** keeps you in 0.x even on `feat!` (breaking) commits, until you explicitly cut a 1.0 release. Drop the flag when you're ready to commit to SemVer's major-bump rules.
- **PR title** from release-please includes the target branch name (e.g. `chore: release main`). That's cosmetic; the version still appears in the PR body and on the tag.
- **`initial-version`** in `release-please-config.json` pins the very first release version. Without it, release-please defaults to `1.0.0`, which is usually too bold for a freshly bootstrapped repo.
- Avalonia self-contained binaries with **trimming disabled** are ~70 MB each. Enable trimming carefully (XAML reflection paths often need `<TrimmerRootDescriptor>` hints).

## Reference repo

Live reference implementation: https://github.com/cihanyakar/Markus

All configuration files in this snippet are pulled verbatim from that repository as of 2026-05-17.

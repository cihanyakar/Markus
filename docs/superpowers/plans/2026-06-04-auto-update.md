# Auto-Update via GitHub Releases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let Markus detect newer GitHub releases, notify the user, and on one click download plus verify the correct platform artifact and hand it to the OS to open.

**Architecture:** All decision logic is pure and unit-tested. The only network touch sits behind `IReleaseFeed`. A `UpdateViewModel` drives a dismissible banner and a manual menu check. Settings gain a channel selector and an on-launch toggle. No silent in-place replacement.

**Tech Stack:** Avalonia 12, CommunityToolkit.Mvvm, .NET 11 (NativeAOT for the shipped build), System.Text.Json source generation, xUnit v3 plus Shouldly.

---

## Critical environment notes (read before any task)

- **SDK.** The system `dotnet` is 10 and cannot build `net11.0`. Every build/test/format command MUST run with `~/.dotnet/dotnet` on PATH. Prefix commands with `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"`.
- **Solution file.** It is `Markus.slnx` (not `.sln`).
- **Warnings are errors.** `Directory.Build.props` sets `TreatWarningsAsErrors=true` with StyleCop, Meziantou, Sonar, Roslynator analyzers. A lint is a build failure. Notable consequences:
  - StyleCop SA1313 forbids PascalCase positional record parameters, so domain types here use sealed classes with `init` properties, and `SemVer` uses a struct with lowercase constructor parameters and PascalCase properties.
  - Public members need XML docs unless suppressed. These types are `internal`, and `CS1591` is already in `NoWarn`, so internal members do not require docs. Keep everything `internal`.
- **Formatting gate.** The format tool is CSharpier, not `dotnet format`. Before each commit run `PATH="$HOME/.dotnet:$PATH" dotnet csharpier format <changed files>`. The Husky pre-commit hook runs `csharpier check` on staged `.cs` files and the commit-msg hook enforces Conventional Commits.
- **Commit approval.** This repo's owner reviews before commits land. When executing, propose the commit (message included) and wait for the owner's go-ahead rather than committing unprompted.
- **Test loop speed.** Use Debug for the red/green loop (`dotnet test Markus.slnx --filter ...`). Before committing, run the full Release build plus `csharpier check .` once.

## File structure

New files, each with one responsibility:

- `src/Markus/Models/SemVer.cs` — semantic version value type, parse and compare.
- `src/Markus/Models/UpdateChannel.cs` — `Stable` / `Prerelease` enum.
- `src/Markus/Services/Updates/ReleaseModels.cs` — `ReleaseAsset`, `ReleaseInfo`, `UpdateCheckResult` domain types.
- `src/Markus/Services/Updates/RuntimeRid.cs` — current runtime identifier string (`osx-arm64` etc.).
- `src/Markus/Services/Updates/ReleaseAssetSelector.cs` — pick the asset matching a RID.
- `src/Markus/Services/Updates/Sha256Verifier.cs` — parse the `.sha256` sidecar and verify a digest.
- `src/Markus/Services/Updates/UpdatePolicy.cs` — pure `ShouldAutoCheck` debounce decision.
- `src/Markus/Services/Updates/GitHubDtos.cs` — JSON DTOs for the GitHub API.
- `src/Markus/Services/Updates/GitHubReleaseJsonContext.cs` — source-generated JSON context.
- `src/Markus/Services/Updates/IReleaseFeed.cs` — feed abstraction.
- `src/Markus/Services/Updates/GitHubReleaseFeed.cs` — HTTP implementation plus the pure `Map` helper.
- `src/Markus/Services/Updates/UpdateChecker.cs` — orchestrates feed plus channel filter plus asset selection.
- `src/Markus/Services/Updates/IVersionProvider.cs`, `AssemblyVersionProvider.cs` — running version.
- `src/Markus/Services/Updates/IUpdateDownloader.cs`, `UpdateDownloader.cs` — download plus verify.
- `src/Markus/Services/Updates/IUpdateLauncher.cs`, `UpdateLauncher.cs` — open the file / release page.
- `src/Markus/ViewModels/UpdateViewModel.cs` — banner state and commands.

Modified files:

- `src/Markus/Models/AppSettings.cs` — new fields plus `Clone()`.
- `src/Markus/Services/AppSettingsJsonContext.cs` — add `UpdateChannel` reachability (already covered by `AppSettings`).
- `src/Markus/ViewModels/SettingsViewModel.cs` — channel and on-launch properties, new category, auto-save hooks.
- `src/Markus/ViewModels/SettingsCategory.cs` — add `Updates`.
- `src/Markus/Views/SettingsWindow.axaml` — Updates category panel.
- `src/Markus/ViewModels/MainWindowViewModel.cs` — `Update` property.
- `src/Markus/Views/MainWindow.axaml` — banner row.
- `src/Markus/App.axaml` — "Check for Updates..." menu item.
- `src/Markus/Services/AppCommands.cs` — `CheckForUpdates` command.
- `src/Markus/App.axaml.cs` — compose update services, launch check.
- `src/Markus/Views/AboutWindow.axaml` + `.axaml.cs` — "Check for Updates" button.

Test files:

- `tests/Markus.Tests/Models/SemVerTests.cs`
- `tests/Markus.Tests/Services/Updates/ReleaseAssetSelectorTests.cs`
- `tests/Markus.Tests/Services/Updates/Sha256VerifierTests.cs`
- `tests/Markus.Tests/Services/Updates/UpdatePolicyTests.cs`
- `tests/Markus.Tests/Services/Updates/GitHubReleaseMappingTests.cs`
- `tests/Markus.Tests/Services/Updates/UpdateCheckerTests.cs`
- `tests/Markus.Tests/Services/Updates/FakeReleaseFeed.cs`
- `tests/Markus.Tests/Services/Updates/RuntimeRidTests.cs`
- `tests/Markus.Tests/Models/AppSettingsUpdateFieldsTests.cs`
- `tests/Markus.Tests/ViewModels/UpdateViewModelTests.cs`
- `tests/Markus.Tests/ViewModels/Updates/FakeUpdateIo.cs`

---

## Phase 1: Domain and settings (pure, TDD)

### Task 1: SemVer value type

**Files:**
- Create: `src/Markus/Models/SemVer.cs`
- Test: `tests/Markus.Tests/Models/SemVerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Markus.Tests.Models;

using Markus.Models;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("0.4.0", 0, 4, 0, null)]
    [InlineData("v0.4.0", 0, 4, 0, null)]
    [InlineData("1.2.3-alpha.0.5", 1, 2, 3, "alpha.0.5")]
    [InlineData("0.4.1-alpha.0.5+abc123", 0, 4, 1, "alpha.0.5")]
    public void Parse_ExtractsComponents(string text, int major, int minor, int patch, string? pre)
    {
        var ok = SemVer.TryParse(text, out var v);

        ok.ShouldBeTrue();
        v.Major.ShouldBe(major);
        v.Minor.ShouldBe(minor);
        v.Patch.ShouldBe(patch);
        v.PreRelease.ShouldBe(pre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    public void TryParse_RejectsGarbage(string text)
    {
        SemVer.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void Compare_HigherCoreIsGreater()
    {
        SemVer.Parse("0.5.0").CompareTo(SemVer.Parse("0.4.9")).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Compare_PrereleaseIsLessThanRelease()
    {
        SemVer.Parse("1.0.0-alpha").CompareTo(SemVer.Parse("1.0.0")).ShouldBeLessThan(0);
    }

    [Fact]
    public void Compare_PrereleaseOrderingFollowsSemver()
    {
        SemVer.Parse("1.0.0-alpha.1").CompareTo(SemVer.Parse("1.0.0-alpha.2")).ShouldBeLessThan(0);
        SemVer.Parse("1.0.0-alpha.2").CompareTo(SemVer.Parse("1.0.0-alpha.10")).ShouldBeLessThan(0);
        SemVer.Parse("1.0.0-beta").CompareTo(SemVer.Parse("1.0.0-alpha")).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Compare_BuildMetadataIgnored()
    {
        SemVer.Parse("1.0.0+a").CompareTo(SemVer.Parse("1.0.0+b")).ShouldBe(0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~SemVerTests"`
Expected: FAIL, `SemVer` does not exist.

- [ ] **Step 3: Implement SemVer**

```csharp
namespace Markus.Models;

using System.Globalization;

internal readonly struct SemVer : IComparable<SemVer>, IEquatable<SemVer>
{
    public SemVer(int major, int minor, int patch, string? preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrEmpty(preRelease) ? null : preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    // null means a stable release. A non-null value is the dot-separated
    // prerelease string, e.g. "alpha.0.5".
    public string? PreRelease { get; }

    public static SemVer Parse(string text)
    {
        return TryParse(text, out var v)
            ? v
            : throw new FormatException($"Not a semantic version: {text}");
    }

    public static bool TryParse(string? text, out SemVer value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        var plus = span.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            span = span[..plus];
        }

        string? pre = null;
        var dash = span.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            pre = span[(dash + 1)..];
            span = span[..dash];
        }

        var parts = span.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var patch)
        )
        {
            return false;
        }

        if (pre is not null && pre.Length == 0)
        {
            return false;
        }

        value = new SemVer(major, minor, patch, pre);
        return true;
    }

    public int CompareTo(SemVer other)
    {
        var core = Major.CompareTo(other.Major);
        if (core != 0)
        {
            return core;
        }

        core = Minor.CompareTo(other.Minor);
        if (core != 0)
        {
            return core;
        }

        core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public bool Equals(SemVer other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is SemVer other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch, PreRelease);
    }

    public override string ToString()
    {
        var core = $"{Major}.{Minor}.{Patch}";
        return PreRelease is null ? core : $"{core}-{PreRelease}";
    }

    public static bool operator ==(SemVer left, SemVer right) => left.Equals(right);

    public static bool operator !=(SemVer left, SemVer right) => !left.Equals(right);

    public static bool operator <(SemVer left, SemVer right) => left.CompareTo(right) < 0;

    public static bool operator >(SemVer left, SemVer right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemVer left, SemVer right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemVer left, SemVer right) => left.CompareTo(right) >= 0;

    private static int ComparePreRelease(string? a, string? b)
    {
        if (a is null && b is null)
        {
            return 0;
        }

        // A version with a prerelease has lower precedence than one without.
        if (a is null)
        {
            return 1;
        }

        if (b is null)
        {
            return -1;
        }

        var left = a.Split('.');
        var right = b.Split('.');
        var max = Math.Max(left.Length, right.Length);
        for (var i = 0; i < max; i++)
        {
            if (i >= left.Length)
            {
                return -1;
            }

            if (i >= right.Length)
            {
                return 1;
            }

            var cmp = CompareIdentifier(left[i], right[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }

    private static int CompareIdentifier(string a, string b)
    {
        var aNum = int.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out var an);
        var bNum = int.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out var bn);

        if (aNum && bNum)
        {
            return an.CompareTo(bn);
        }

        // Numeric identifiers always have lower precedence than alphanumeric.
        if (aNum)
        {
            return -1;
        }

        if (bNum)
        {
            return 1;
        }

        return string.CompareOrdinal(a, b);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~SemVerTests"`
Expected: PASS.

- [ ] **Step 5: Format and commit**

```bash
PATH="$HOME/.dotnet:$PATH" dotnet csharpier format src/Markus/Models/SemVer.cs tests/Markus.Tests/Models/SemVerTests.cs
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" git commit -m "feat(updates): add SemVer value type" \
  src/Markus/Models/SemVer.cs tests/Markus.Tests/Models/SemVerTests.cs
```
(Stage with `git add` first; confirm the message with the owner before committing.)

---

### Task 2: UpdateChannel enum and AppSettings fields

**Files:**
- Create: `src/Markus/Models/UpdateChannel.cs`
- Modify: `src/Markus/Models/AppSettings.cs`
- Test: `tests/Markus.Tests/Models/AppSettingsUpdateFieldsTests.cs`

- [ ] **Step 1: Write the failing test**

The settings round-trip uses the existing source-generated context through `SettingsService`, so this test proves the new fields persist as a string enum and survive save/load.

```csharp
namespace Markus.Tests.Models;

using Markus.Models;
using Markus.Services;

public sealed class AppSettingsUpdateFieldsTests
{
    [Fact]
    public void Defaults_AreStableAndOnLaunchEnabled()
    {
        var settings = new AppSettings();

        settings.CheckForUpdatesOnLaunch.ShouldBeTrue();
        settings.UpdateChannel.ShouldBe(UpdateChannel.Stable);
        settings.LastUpdateCheckUtc.ShouldBeNull();
        settings.SkippedVersion.ShouldBeNull();
    }

    [Fact]
    public void Clone_CopiesUpdateFields()
    {
        var when = DateTimeOffset.UtcNow;
        var settings = new AppSettings
        {
            CheckForUpdatesOnLaunch = false,
            UpdateChannel = UpdateChannel.Prerelease,
            LastUpdateCheckUtc = when,
            SkippedVersion = "v0.9.0",
        };

        var clone = settings.Clone();

        clone.CheckForUpdatesOnLaunch.ShouldBeFalse();
        clone.UpdateChannel.ShouldBe(UpdateChannel.Prerelease);
        clone.LastUpdateCheckUtc.ShouldBe(when);
        clone.SkippedVersion.ShouldBe("v0.9.0");
    }

    [Fact]
    public void SaveLoad_RoundTripsUpdateFields()
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-tests", Guid.NewGuid().ToString("N"));
        var service = new SettingsService(dir);
        var settings = new AppSettings
        {
            CheckForUpdatesOnLaunch = false,
            UpdateChannel = UpdateChannel.Prerelease,
            SkippedVersion = "v0.9.0",
        };

        service.Save(settings);
        var loaded = service.Load();

        loaded.CheckForUpdatesOnLaunch.ShouldBeFalse();
        loaded.UpdateChannel.ShouldBe(UpdateChannel.Prerelease);
        loaded.SkippedVersion.ShouldBe("v0.9.0");

        Directory.Delete(dir, recursive: true);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~AppSettingsUpdateFieldsTests"`
Expected: FAIL, `UpdateChannel` and the new properties do not exist.

- [ ] **Step 3: Create the enum**

`src/Markus/Models/UpdateChannel.cs`:

```csharp
namespace Markus.Models;

internal enum UpdateChannel
{
    Stable,
    Prerelease,
}
```

- [ ] **Step 4: Add fields to AppSettings**

In `src/Markus/Models/AppSettings.cs`, add these properties after `RestoreSessionOnLaunch`:

```csharp
    public bool CheckForUpdatesOnLaunch { get; set; } = true;

    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public string? SkippedVersion { get; set; }
```

And add these four lines to the object initializer inside `Clone()` (after `RestoreSessionOnLaunch = RestoreSessionOnLaunch,`):

```csharp
            CheckForUpdatesOnLaunch = CheckForUpdatesOnLaunch,
            UpdateChannel = UpdateChannel,
            LastUpdateCheckUtc = LastUpdateCheckUtc,
            SkippedVersion = SkippedVersion,
```

No change to `AppSettingsJsonContext` is needed. It already serializes the whole `AppSettings` graph and was configured with `UseStringEnumConverter = true`, so `UpdateChannel` persists as `"Stable"` / `"Prerelease"`.

- [ ] **Step 5: Run to verify it passes**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~AppSettingsUpdateFieldsTests"`
Expected: PASS.

- [ ] **Step 6: Format and commit**

```bash
PATH="$HOME/.dotnet:$PATH" dotnet csharpier format src/Markus/Models/UpdateChannel.cs src/Markus/Models/AppSettings.cs tests/Markus.Tests/Models/AppSettingsUpdateFieldsTests.cs
```
Commit message: `feat(updates): add update channel and on-launch settings`. Confirm with the owner before committing.

---

### Task 3: RuntimeRid

**Files:**
- Create: `src/Markus/Services/Updates/RuntimeRid.cs`
- Test: `tests/Markus.Tests/Services/Updates/RuntimeRidTests.cs`

The pure `Resolve` maps an OS token plus an `Architecture` to the canonical RID used in artifact names (`win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`). The `Current` property derives the OS token from `OperatingSystem` checks and is exercised manually.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using System.Runtime.InteropServices;
using Markus.Services.Updates;

public sealed class RuntimeRidTests
{
    [Theory]
    [InlineData("osx", Architecture.Arm64, "osx-arm64")]
    [InlineData("osx", Architecture.X64, "osx-x64")]
    [InlineData("win", Architecture.X64, "win-x64")]
    [InlineData("linux", Architecture.X64, "linux-x64")]
    public void Resolve_BuildsCanonicalRid(string os, Architecture arch, string expected)
    {
        RuntimeRid.Resolve(os, arch).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~RuntimeRidTests"`
Expected: FAIL, `RuntimeRid` does not exist.

- [ ] **Step 3: Implement**

```csharp
namespace Markus.Services.Updates;

using System.Runtime.InteropServices;

internal static class RuntimeRid
{
    public static string Current => Resolve(CurrentOs(), RuntimeInformation.ProcessArchitecture);

    public static string Resolve(string os, Architecture arch)
    {
        return $"{os}-{Arch(arch)}";
    }

    private static string CurrentOs()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        return "linux";
    }

    private static string Arch(Architecture arch)
    {
        return arch == Architecture.Arm64 ? "arm64" : "x64";
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~RuntimeRidTests"`
Expected: PASS.

- [ ] **Step 5: Format and commit**

Format both files. Commit message: `feat(updates): add runtime identifier helper`. Confirm before committing.

---

### Task 4: Release domain models

**Files:**
- Create: `src/Markus/Services/Updates/ReleaseModels.cs`

No test of its own. These types are consumed and asserted by Tasks 5, 9, and 10. Sealed classes with `init` properties keep StyleCop SA1313 happy.

- [ ] **Step 1: Create the file**

```csharp
namespace Markus.Services.Updates;

using Markus.Models;

internal sealed class ReleaseAsset
{
    public required string Name { get; init; }

    public required Uri DownloadUrl { get; init; }

    public long Size { get; init; }
}

internal sealed class ReleaseInfo
{
    public required SemVer Version { get; init; }

    public required string TagName { get; init; }

    public bool IsPrerelease { get; init; }

    public string? Notes { get; init; }

    public required Uri HtmlUrl { get; init; }

    public required IReadOnlyList<ReleaseAsset> Assets { get; init; }
}

internal sealed class UpdateCheckResult
{
    public bool UpdateAvailable { get; init; }

    public ReleaseInfo? Release { get; init; }

    public ReleaseAsset? Asset { get; init; }

    public static UpdateCheckResult None { get; } = new UpdateCheckResult { UpdateAvailable = false };
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 3: Format and commit**

Commit message: `feat(updates): add release domain models`. Confirm before committing.

---

### Task 5: ReleaseAssetSelector

**Files:**
- Create: `src/Markus/Services/Updates/ReleaseAssetSelector.cs`
- Test: `tests/Markus.Tests/Services/Updates/ReleaseAssetSelectorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using Markus.Services.Updates;

public sealed class ReleaseAssetSelectorTests
{
    private static ReleaseAsset Asset(string name)
    {
        return new ReleaseAsset { Name = name, DownloadUrl = new Uri($"https://example.test/{name}"), Size = 1 };
    }

    private static readonly IReadOnlyList<ReleaseAsset> Sample = new[]
    {
        Asset("Markus-v0.5.0-win-x64.zip"),
        Asset("Markus-v0.5.0-osx-x64.dmg"),
        Asset("Markus-v0.5.0-osx-arm64.dmg"),
        Asset("Markus-v0.5.0-linux-x64.tar.gz"),
        Asset("Markus-v0.5.0-osx-arm64.dmg.sha256"),
    };

    [Fact]
    public void Select_MatchesRidAndSkipsChecksum()
    {
        var asset = ReleaseAssetSelector.Select(Sample, "osx-arm64");

        asset.ShouldNotBeNull();
        asset!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_ReturnsNullWhenNoRidMatch()
    {
        ReleaseAssetSelector.Select(Sample, "linux-arm64").ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~ReleaseAssetSelectorTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Markus.Services.Updates;

internal static class ReleaseAssetSelector
{
    public static ReleaseAsset? Select(IReadOnlyList<ReleaseAsset> assets, string rid)
    {
        foreach (var asset in assets)
        {
            if (
                asset.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
                || !asset.Name.Contains(rid, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            return asset;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run the same filter. Expected: PASS.

- [ ] **Step 5: Format and commit**

Commit message: `feat(updates): add release asset selector`. Confirm before committing.

---

### Task 6: Sha256Verifier

**Files:**
- Create: `src/Markus/Services/Updates/Sha256Verifier.cs`
- Test: `tests/Markus.Tests/Services/Updates/Sha256VerifierTests.cs`

- [ ] **Step 1: Write the failing test**

The well-known SHA256 of the ASCII bytes `abc` is `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`.

```csharp
namespace Markus.Tests.Services.Updates;

using System.Text;
using Markus.Services.Updates;

public sealed class Sha256VerifierTests
{
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void ComputeHex_MatchesKnownVector()
    {
        Sha256Verifier.ComputeHex(Encoding.ASCII.GetBytes("abc")).ShouldBe(AbcHash);
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        Sha256Verifier.Matches(AbcHash.ToUpperInvariant(), Encoding.ASCII.GetBytes("abc")).ShouldBeTrue();
    }

    [Fact]
    public void Matches_FalseOnTamper()
    {
        Sha256Verifier.Matches(AbcHash, Encoding.ASCII.GetBytes("abcd")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("ba7816bf...  Markus-v0.5.0-osx-arm64.dmg", "ba7816bf...")]
    [InlineData("ba7816bf...", "ba7816bf...")]
    public void ParseExpectedHash_ReadsSidecar(string content, string expected)
    {
        Sha256Verifier.ParseExpectedHash(content).ShouldBe(expected);
    }

    [Fact]
    public void ParseExpectedHash_NullOnEmpty()
    {
        Sha256Verifier.ParseExpectedHash("   ").ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~Sha256VerifierTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Markus.Services.Updates;

using System.Security.Cryptography;

internal static class Sha256Verifier
{
    public static string ComputeHex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool Matches(string expectedHex, byte[] fileBytes)
    {
        return string.Equals(expectedHex.Trim(), ComputeHex(fileBytes), StringComparison.OrdinalIgnoreCase);
    }

    // Sidecar files written by the release workflow look like
    // "<hash>  <filename>" (shasum/sha256sum format). Some tools emit just
    // the hash. Take the first whitespace-delimited token.
    public static string? ParseExpectedHash(string sidecarContent)
    {
        if (string.IsNullOrWhiteSpace(sidecarContent))
        {
            return null;
        }

        var first = sidecarContent.Trim().Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        return first.Length == 0 ? null : first[0];
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 5: Format and commit**

Commit message: `feat(updates): add sha256 verifier`. Confirm before committing.

---

### Task 7: UpdatePolicy.ShouldAutoCheck

**Files:**
- Create: `src/Markus/Services/Updates/UpdatePolicy.cs`
- Test: `tests/Markus.Tests/Services/Updates/UpdatePolicyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using Markus.Services.Updates;

public sealed class UpdatePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Day = TimeSpan.FromHours(20);

    [Fact]
    public void NeverCheckedAndEnabled_ChecksOnFreshLaunch()
    {
        UpdatePolicy.ShouldAutoCheck(enabled: true, isSpawnedChild: false, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeTrue();
    }

    [Fact]
    public void Disabled_NeverChecks()
    {
        UpdatePolicy.ShouldAutoCheck(enabled: false, isSpawnedChild: false, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeFalse();
    }

    [Fact]
    public void SpawnedChild_NeverChecks()
    {
        UpdatePolicy.ShouldAutoCheck(enabled: true, isSpawnedChild: true, lastCheckUtc: null, now: Now, minInterval: Day)
            .ShouldBeFalse();
    }

    [Fact]
    public void WithinInterval_DoesNotCheck()
    {
        UpdatePolicy.ShouldAutoCheck(true, false, Now.AddHours(-1), Now, Day).ShouldBeFalse();
    }

    [Fact]
    public void PastInterval_Checks()
    {
        UpdatePolicy.ShouldAutoCheck(true, false, Now.AddHours(-21), Now, Day).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~UpdatePolicyTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

```csharp
namespace Markus.Services.Updates;

internal static class UpdatePolicy
{
    public static bool ShouldAutoCheck(
        bool enabled,
        bool isSpawnedChild,
        DateTimeOffset? lastCheckUtc,
        DateTimeOffset now,
        TimeSpan minInterval
    )
    {
        if (!enabled || isSpawnedChild)
        {
            return false;
        }

        return lastCheckUtc is null || now - lastCheckUtc.Value >= minInterval;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 5: Format and commit**

Commit message: `feat(updates): add auto-check debounce policy`. Confirm before committing.

---

## Phase 2: Feed and checker

### Task 8: GitHub DTOs, JSON context, and mapper

**Files:**
- Create: `src/Markus/Services/Updates/GitHubDtos.cs`
- Create: `src/Markus/Services/Updates/GitHubReleaseJsonContext.cs`
- Create: `src/Markus/Services/Updates/GitHubReleaseMapper.cs`
- Test: `tests/Markus.Tests/Services/Updates/GitHubReleaseMappingTests.cs`

The mapper is the testable seam. It deserializes a real GitHub payload through the source-generated context and maps to `ReleaseInfo`, dropping drafts and releases whose tag is not a semantic version.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using System.Text.Json;
using Markus.Services.Updates;

public sealed class GitHubReleaseMappingTests
{
    private const string Json = """
    {
      "tag_name": "v0.5.0",
      "name": "0.5.0",
      "body": "notes here",
      "draft": false,
      "prerelease": false,
      "html_url": "https://github.com/cihanyakar/Markus/releases/tag/v0.5.0",
      "assets": [
        {
          "name": "Markus-v0.5.0-osx-arm64.dmg",
          "browser_download_url": "https://github.com/cihanyakar/Markus/releases/download/v0.5.0/Markus-v0.5.0-osx-arm64.dmg",
          "size": 12345
        }
      ]
    }
    """;

    private static GitHubRelease Deserialize(string json)
    {
        return JsonSerializer.Deserialize(json, GitHubReleaseJsonContext.Default.GitHubRelease)!;
    }

    [Fact]
    public void Map_ParsesReleaseAndAsset()
    {
        var info = GitHubReleaseMapper.Map(Deserialize(Json));

        info.ShouldNotBeNull();
        info!.TagName.ShouldBe("v0.5.0");
        info.Version.ShouldBe(Markus.Models.SemVer.Parse("0.5.0"));
        info.IsPrerelease.ShouldBeFalse();
        info.Notes.ShouldBe("notes here");
        info.Assets.Count.ShouldBe(1);
        info.Assets[0].Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
        info.Assets[0].Size.ShouldBe(12345);
    }

    [Fact]
    public void Map_DropsDraft()
    {
        var dto = Deserialize(Json);
        dto.Draft = true;

        GitHubReleaseMapper.Map(dto).ShouldBeNull();
    }

    [Fact]
    public void Map_DropsUnparseableTag()
    {
        var dto = Deserialize(Json);
        dto.TagName = "nightly";

        GitHubReleaseMapper.Map(dto).ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~GitHubReleaseMappingTests"`
Expected: FAIL.

- [ ] **Step 3: Create the DTOs**

`src/Markus/Services/Updates/GitHubDtos.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Text.Json.Serialization;

internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
```

- [ ] **Step 4: Create the JSON context**

`src/Markus/Services/Updates/GitHubReleaseJsonContext.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Text.Json.Serialization;

// Source-generated so NativeAOT does not need the reflection serializer.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(List<GitHubRelease>))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext { }
```

- [ ] **Step 5: Create the mapper**

`src/Markus/Services/Updates/GitHubReleaseMapper.cs`:

```csharp
namespace Markus.Services.Updates;

using Markus.Models;

internal static class GitHubReleaseMapper
{
    public static ReleaseInfo? Map(GitHubRelease dto)
    {
        if (dto.Draft || string.IsNullOrWhiteSpace(dto.TagName))
        {
            return null;
        }

        if (!SemVer.TryParse(dto.TagName, out var version))
        {
            return null;
        }

        if (!Uri.TryCreate(dto.HtmlUrl, UriKind.Absolute, out var htmlUrl))
        {
            return null;
        }

        var assets = new List<ReleaseAsset>();
        foreach (var raw in dto.Assets ?? new List<GitHubAsset>())
        {
            if (
                string.IsNullOrWhiteSpace(raw.Name)
                || !Uri.TryCreate(raw.BrowserDownloadUrl, UriKind.Absolute, out var url)
            )
            {
                continue;
            }

            assets.Add(new ReleaseAsset { Name = raw.Name, DownloadUrl = url, Size = raw.Size });
        }

        return new ReleaseInfo
        {
            Version = version,
            TagName = dto.TagName,
            IsPrerelease = dto.Prerelease,
            Notes = dto.Body,
            HtmlUrl = htmlUrl,
            Assets = assets,
        };
    }
}
```

- [ ] **Step 6: Run to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 7: Format and commit**

Commit message: `feat(updates): add github release dtos and mapper`. Confirm before committing.

---

### Task 9: IReleaseFeed and GitHubReleaseFeed

**Files:**
- Create: `src/Markus/Services/Updates/IReleaseFeed.cs`
- Create: `src/Markus/Services/Updates/GitHubReleaseFeed.cs`

This is the network layer. It has no unit test (the mapper it delegates to is already covered). Verify it builds; exercise it during the manual checklist at the end.

- [ ] **Step 1: Create the interface**

`src/Markus/Services/Updates/IReleaseFeed.cs`:

```csharp
namespace Markus.Services.Updates;

internal interface IReleaseFeed
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Create the implementation**

`src/Markus/Services/Updates/GitHubReleaseFeed.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Net.Http;
using System.Net.Http.Json;

internal sealed class GitHubReleaseFeed : IReleaseFeed
{
    // Repo coordinates built at runtime to avoid the hardcoded-URI lint (S1075),
    // matching AboutWindow's approach.
    private const string RepoOwner = "cihanyakar";
    private const string RepoName = "Markus";

    private readonly HttpClient _http;
    private readonly Uri _endpoint;

    public GitHubReleaseFeed()
        : this(CreateClient()) { }

    internal GitHubReleaseFeed(HttpClient http)
    {
        _http = http;
        _endpoint = new Uri(
            $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=30"
        );
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct)
    {
        var dtos = await _http
            .GetFromJsonAsync(_endpoint, GitHubReleaseJsonContext.Default.ListGitHubRelease, ct)
            .ConfigureAwait(false);

        if (dtos is null)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var result = new List<ReleaseInfo>();
        foreach (var dto in dtos)
        {
            var info = GitHubReleaseMapper.Map(dto);
            if (info is not null)
            {
                result.Add(info);
            }
        }

        return result;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub rejects requests without a User-Agent. Version is informational.
        var version = typeof(GitHubReleaseFeed).Assembly.GetName().Version?.ToString(3) ?? "dev";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Markus/{version}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
```

Note: `GitHubReleaseJsonContext.Default.ListGitHubRelease` is generated from the `[JsonSerializable(typeof(List<GitHubRelease>))]` attribute in Task 8. If the generated member name differs in the SDK in use, build output will name it; adjust the reference accordingly.

- [ ] **Step 3: Build to verify it compiles**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 4: Format and commit**

Commit message: `feat(updates): add github release feed`. Confirm before committing.

---

### Task 10: UpdateChecker

**Files:**
- Create: `src/Markus/Services/Updates/UpdateChecker.cs`
- Create: `tests/Markus.Tests/Services/Updates/FakeReleaseFeed.cs`
- Test: `tests/Markus.Tests/Services/Updates/UpdateCheckerTests.cs`

`CheckAsync` does not know about skipped versions; that belongs to the banner layer. It returns whether a strictly newer release exists for the channel, plus the matching asset (which may be null when no artifact matches the RID, signalling the release-page fallback).

- [ ] **Step 1: Create the fake feed**

`tests/Markus.Tests/Services/Updates/FakeReleaseFeed.cs`:

```csharp
namespace Markus.Tests.Services.Updates;

using Markus.Services.Updates;

internal sealed class FakeReleaseFeed : IReleaseFeed
{
    private readonly IReadOnlyList<ReleaseInfo> _releases;

    public FakeReleaseFeed(params ReleaseInfo[] releases)
    {
        _releases = releases;
    }

    public Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken ct)
    {
        return Task.FromResult(_releases);
    }
}
```

- [ ] **Step 2: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using Markus.Models;
using Markus.Services.Updates;

public sealed class UpdateCheckerTests
{
    private const string Rid = "osx-arm64";

    private static ReleaseInfo Release(string tag, bool prerelease, params string[] assetNames)
    {
        var assets = assetNames
            .Select(n => new ReleaseAsset { Name = n, DownloadUrl = new Uri($"https://x.test/{n}"), Size = 1 })
            .ToList();
        return new ReleaseInfo
        {
            Version = SemVer.Parse(tag),
            TagName = tag,
            IsPrerelease = prerelease,
            Notes = null,
            HtmlUrl = new Uri($"https://x.test/{tag}"),
            Assets = assets,
        };
    }

    [Fact]
    public async Task NewerStableRelease_IsAvailableWithAsset()
    {
        var feed = new FakeReleaseFeed(Release("v0.5.0", false, "Markus-v0.5.0-osx-arm64.dmg"));
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Stable,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.UpdateAvailable.ShouldBeTrue();
        result.Release!.TagName.ShouldBe("v0.5.0");
        result.Asset!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public async Task SameOrOlder_NotAvailable()
    {
        var feed = new FakeReleaseFeed(Release("v0.4.0", false, "Markus-v0.4.0-osx-arm64.dmg"));
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Stable,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.UpdateAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task StableChannel_IgnoresPrerelease()
    {
        var feed = new FakeReleaseFeed(Release("v0.5.0-alpha.1", true, "Markus-v0.5.0-alpha.1-osx-arm64.dmg"));
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Stable,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.UpdateAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task PrereleaseChannel_ConsidersPrerelease()
    {
        var feed = new FakeReleaseFeed(Release("v0.5.0-alpha.1", true, "Markus-v0.5.0-alpha.1-osx-arm64.dmg"));
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Prerelease,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.UpdateAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task NewerReleaseWithoutMatchingAsset_AvailableButNoAsset()
    {
        var feed = new FakeReleaseFeed(Release("v0.5.0", false, "Markus-v0.5.0-win-x64.zip"));
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Stable,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.UpdateAvailable.ShouldBeTrue();
        result.Asset.ShouldBeNull();
        result.Release!.HtmlUrl.ShouldNotBeNull();
    }

    [Fact]
    public async Task PicksHighestAcrossMultipleReleases()
    {
        var feed = new FakeReleaseFeed(
            Release("v0.5.0", false, "Markus-v0.5.0-osx-arm64.dmg"),
            Release("v0.6.0", false, "Markus-v0.6.0-osx-arm64.dmg"),
            Release("v0.4.5", false, "Markus-v0.4.5-osx-arm64.dmg")
        );
        var checker = new UpdateChecker(feed);

        var result = await checker.CheckAsync(
            SemVer.Parse("0.4.0"),
            UpdateChannel.Stable,
            Rid,
            TestContext.Current.CancellationToken
        );

        result.Release!.TagName.ShouldBe("v0.6.0");
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~UpdateCheckerTests"`
Expected: FAIL.

- [ ] **Step 4: Implement**

```csharp
namespace Markus.Services.Updates;

using Markus.Models;

internal sealed class UpdateChecker
{
    private readonly IReleaseFeed _feed;

    public UpdateChecker(IReleaseFeed feed)
    {
        _feed = feed;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        SemVer current,
        UpdateChannel channel,
        string rid,
        CancellationToken ct
    )
    {
        var releases = await _feed.GetReleasesAsync(ct).ConfigureAwait(false);

        ReleaseInfo? best = null;
        foreach (var release in releases)
        {
            if (channel == UpdateChannel.Stable && release.IsPrerelease)
            {
                continue;
            }

            if (best is null || release.Version > best.Version)
            {
                best = release;
            }
        }

        if (best is null || best.Version <= current)
        {
            return UpdateCheckResult.None;
        }

        var asset = ReleaseAssetSelector.Select(best.Assets, rid);
        return new UpdateCheckResult
        {
            UpdateAvailable = true,
            Release = best,
            Asset = asset,
        };
    }
}
```

- [ ] **Step 5: Run to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 6: Format and commit**

Commit message: `feat(updates): add update checker`. Confirm before committing.

---

### Task 11: IVersionProvider and AssemblyVersionProvider

**Files:**
- Create: `src/Markus/Services/Updates/IVersionProvider.cs`
- Create: `src/Markus/Services/Updates/AssemblyVersionProvider.cs`
- Test: `tests/Markus.Tests/Services/Updates/AssemblyVersionProviderTests.cs`

The provider reads `AssemblyInformationalVersion` (stamped by MinVer) and parses it through `SemVer`, which already strips build metadata. A test-only constructor takes the raw string so parsing is verified without depending on the build's stamped version.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Markus.Tests.Services.Updates;

using Markus.Models;
using Markus.Services.Updates;

public sealed class AssemblyVersionProviderTests
{
    [Fact]
    public void Current_ParsesInformationalVersion()
    {
        var provider = new AssemblyVersionProvider("0.4.1-alpha.0.5+abc123");

        provider.Current.ShouldBe(SemVer.Parse("0.4.1-alpha.0.5"));
    }

    [Fact]
    public void Current_FallsBackToZeroOnGarbage()
    {
        var provider = new AssemblyVersionProvider("not-a-version");

        provider.Current.ShouldBe(new SemVer(0, 0, 0, null));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~AssemblyVersionProviderTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

`IVersionProvider.cs`:

```csharp
namespace Markus.Services.Updates;

using Markus.Models;

internal interface IVersionProvider
{
    SemVer Current { get; }
}
```

`AssemblyVersionProvider.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Reflection;
using Markus.Models;

internal sealed class AssemblyVersionProvider : IVersionProvider
{
    private readonly SemVer _current;

    public AssemblyVersionProvider()
        : this(ReadInformationalVersion()) { }

    internal AssemblyVersionProvider(string informationalVersion)
    {
        _current = SemVer.TryParse(informationalVersion, out var v) ? v : new SemVer(0, 0, 0, null);
    }

    public SemVer Current => _current;

    private static string ReadInformationalVersion()
    {
        return typeof(AssemblyVersionProvider)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "0.0.0";
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 5: Format and commit**

Commit message: `feat(updates): add assembly version provider`. Confirm before committing.

---

## Phase 3: Download and launch (thin IO, manual verification)

### Task 12: IUpdateDownloader and UpdateDownloader

**Files:**
- Create: `src/Markus/Services/Updates/IUpdateDownloader.cs`
- Create: `src/Markus/Services/Updates/UpdateDownloader.cs`

Downloads the artifact and its `.sha256` sidecar, verifies the digest, writes the artifact into `targetDir`, and returns the local path. Throws `InvalidOperationException` on a digest mismatch after deleting the partial file. No unit test (the verifier it uses is covered); exercise it in the manual checklist.

- [ ] **Step 1: Create the interface**

`IUpdateDownloader.cs`:

```csharp
namespace Markus.Services.Updates;

internal interface IUpdateDownloader
{
    Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct);
}
```

- [ ] **Step 2: Create the implementation**

`UpdateDownloader.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Net.Http;

internal sealed class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader()
        : this(CreateClient()) { }

    internal UpdateDownloader(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct)
    {
        Directory.CreateDirectory(targetDir);
        var localPath = Path.Combine(targetDir, asset.Name);

        var bytes = await _http.GetByteArrayAsync(asset.DownloadUrl, ct).ConfigureAwait(false);

        var sidecarUrl = new Uri(asset.DownloadUrl.AbsoluteUri + ".sha256");
        var expected = await TryGetExpectedHashAsync(sidecarUrl, ct).ConfigureAwait(false);

        if (expected is not null && !Sha256Verifier.Matches(expected, bytes))
        {
            throw new InvalidOperationException($"Checksum mismatch for {asset.Name}.");
        }

        await File.WriteAllBytesAsync(localPath, bytes, ct).ConfigureAwait(false);
        return localPath;
    }

    private async Task<string?> TryGetExpectedHashAsync(Uri sidecarUrl, CancellationToken ct)
    {
        try
        {
            var content = await _http.GetStringAsync(sidecarUrl, ct).ConfigureAwait(false);
            return Sha256Verifier.ParseExpectedHash(content);
        }
        catch (HttpRequestException)
        {
            // No sidecar published. Proceed without verification rather than
            // blocking the user; the asset still came from the release.
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var version = typeof(UpdateDownloader).Assembly.GetName().Version?.ToString(3) ?? "dev";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Markus/{version}");
        return http;
    }
}
```

- [ ] **Step 3: Build, format, commit**

Build with `~/.dotnet`. Commit message: `feat(updates): add artifact downloader`. Confirm before committing.

---

### Task 13: IUpdateLauncher and UpdateLauncher

**Files:**
- Create: `src/Markus/Services/Updates/IUpdateLauncher.cs`
- Create: `src/Markus/Services/Updates/UpdateLauncher.cs`

Opens the downloaded artifact via the OS (on macOS this mounts a dmg) and opens the release page as a fallback. Mirrors `AboutWindow.OpenUrl`'s `Process.Start` posture.

- [ ] **Step 1: Create the interface**

`IUpdateLauncher.cs`:

```csharp
namespace Markus.Services.Updates;

internal interface IUpdateLauncher
{
    void OpenArtifact(string localPath);

    void OpenReleasePage(Uri htmlUrl);
}
```

- [ ] **Step 2: Create the implementation**

`UpdateLauncher.cs`:

```csharp
namespace Markus.Services.Updates;

using System.Diagnostics;

internal sealed class UpdateLauncher : IUpdateLauncher
{
    public void OpenArtifact(string localPath)
    {
        if (OperatingSystem.IsMacOS())
        {
            // `open` mounts a dmg or opens a zip in Finder. The file was fetched
            // by HttpClient, so it carries no quarantine attribute.
            StartProcess("/usr/bin/open", localPath);
            return;
        }

        Shell(localPath);
    }

    public void OpenReleasePage(Uri htmlUrl)
    {
        Shell(htmlUrl.AbsoluteUri);
    }

    private static void Shell(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No shell association available; nothing more we can do here.
        }
        catch (FileNotFoundException)
        {
            // Same posture.
        }
    }

    private static void StartProcess(string fileName, string argument)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
            psi.ArgumentList.Add(argument);
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Launch failed; the banner stays so the user can retry or open the
            // release page.
        }
        catch (FileNotFoundException)
        {
            // Same posture.
        }
    }
}
```

- [ ] **Step 3: Build, format, commit**

Commit message: `feat(updates): add update launcher`. Confirm before committing.

---

## Phase 4: UpdateViewModel (TDD with fakes)

### Task 14: UpdateViewModel state and commands

**Files:**
- Create: `src/Markus/ViewModels/UpdateViewModel.cs`
- Create: `tests/Markus.Tests/ViewModels/Updates/FakeUpdateIo.cs`
- Test: `tests/Markus.Tests/ViewModels/UpdateViewModelTests.cs`

The view model holds banner state and commands. It reads the channel and skipped-version from settings on each check and persists the last-check time and skip. It does not touch the dispatcher; the app layer marshals the launch check onto the UI thread.

- [ ] **Step 1: Create the fakes**

`tests/Markus.Tests/ViewModels/Updates/FakeUpdateIo.cs`:

```csharp
namespace Markus.Tests.ViewModels.Updates;

using Markus.Services.Updates;

internal sealed class FakeUpdateDownloader : IUpdateDownloader
{
    public ReleaseAsset? DownloadedAsset { get; private set; }

    public string ReturnPath { get; set; } = "/tmp/Markus-update.dmg";

    public Task<string> DownloadAndVerifyAsync(ReleaseAsset asset, string targetDir, CancellationToken ct)
    {
        DownloadedAsset = asset;
        return Task.FromResult(ReturnPath);
    }
}

internal sealed class FakeUpdateLauncher : IUpdateLauncher
{
    public string? OpenedArtifact { get; private set; }

    public Uri? OpenedReleasePage { get; private set; }

    public void OpenArtifact(string localPath)
    {
        OpenedArtifact = localPath;
    }

    public void OpenReleasePage(Uri htmlUrl)
    {
        OpenedReleasePage = htmlUrl;
    }
}

internal sealed class FixedVersionProvider : IVersionProvider
{
    public FixedVersionProvider(string version)
    {
        Current = Markus.Models.SemVer.Parse(version);
    }

    public Markus.Models.SemVer Current { get; }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
namespace Markus.Tests.ViewModels;

using Markus.Models;
using Markus.Services;
using Markus.Services.Updates;
using Markus.Tests.Services.Updates;
using Markus.Tests.ViewModels.Updates;
using Markus.ViewModels;

public sealed class UpdateViewModelTests
{
    private const string Rid = "osx-arm64";

    private static ReleaseInfo Release(string tag, params string[] assetNames)
    {
        var assets = assetNames
            .Select(n => new ReleaseAsset { Name = n, DownloadUrl = new Uri($"https://x.test/{n}"), Size = 1 })
            .ToList();
        return new ReleaseInfo
        {
            Version = SemVer.Parse(tag),
            TagName = tag,
            IsPrerelease = false,
            Notes = "what's new",
            HtmlUrl = new Uri($"https://x.test/{tag}"),
            Assets = assets,
        };
    }

    private static (UpdateViewModel Vm, SettingsService Settings, FakeUpdateDownloader Dl, FakeUpdateLauncher Lf) Build(
        string currentVersion,
        params ReleaseInfo[] releases
    )
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-tests", Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(dir);
        var checker = new UpdateChecker(new FakeReleaseFeed(releases));
        var dl = new FakeUpdateDownloader();
        var lf = new FakeUpdateLauncher();
        var vm = new UpdateViewModel(checker, new FixedVersionProvider(currentVersion), dl, lf, settings, Rid);
        return (vm, settings, dl, lf);
    }

    [Fact]
    public async Task CheckOnLaunch_NewerVersion_ShowsBanner()
    {
        var (vm, _, _, _) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));

        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        vm.IsUpdateAvailable.ShouldBeTrue();
        vm.AvailableVersion.ShouldBe("0.5.0");
        vm.ReleaseNotes.ShouldBe("what's new");
    }

    [Fact]
    public async Task CheckOnLaunch_SkippedVersion_NoBanner()
    {
        var (vm, settings, _, _) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        var s = settings.Load();
        s.SkippedVersion = "v0.5.0";
        settings.Save(s);

        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        vm.IsUpdateAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckOnLaunch_RecordsLastCheckTime()
    {
        var (vm, settings, _, _) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));

        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        settings.Load().LastUpdateCheckUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ManualCheck_UpToDate_SetsStatusNotBanner()
    {
        var (vm, _, _, _) = Build("0.5.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        vm.IsUpdateAvailable.ShouldBeFalse();
        vm.StatusMessage.ShouldContain("latest");
    }

    [Fact]
    public async Task Skip_PersistsAndHidesBanner()
    {
        var (vm, settings, _, _) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        vm.SkipCommand.Execute(null);

        vm.IsUpdateAvailable.ShouldBeFalse();
        settings.Load().SkippedVersion.ShouldBe("v0.5.0");
    }

    [Fact]
    public async Task Download_WithAsset_VerifiesAndOpensArtifact()
    {
        var (vm, _, dl, lf) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        await vm.DownloadCommand.ExecuteAsync(null);

        dl.DownloadedAsset!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
        lf.OpenedArtifact.ShouldBe(dl.ReturnPath);
    }

    [Fact]
    public async Task Download_NoMatchingAsset_OpensReleasePage()
    {
        var (vm, _, _, lf) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-win-x64.zip"));
        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        await vm.DownloadCommand.ExecuteAsync(null);

        lf.OpenedReleasePage.ShouldNotBeNull();
        lf.OpenedArtifact.ShouldBeNull();
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx --filter "FullyQualifiedName~UpdateViewModelTests"`
Expected: FAIL, `UpdateViewModel` does not exist.

- [ ] **Step 4: Implement**

`src/Markus/ViewModels/UpdateViewModel.cs`:

```csharp
namespace Markus.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Services;
using Markus.Services.Updates;

internal sealed partial class UpdateViewModel : ViewModelBase
{
    private readonly UpdateChecker _checker;
    private readonly IVersionProvider _version;
    private readonly IUpdateDownloader _downloader;
    private readonly IUpdateLauncher _launcher;
    private readonly SettingsService _settings;
    private readonly string _rid;

    private ReleaseInfo? _release;
    private ReleaseAsset? _asset;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _availableVersion = string.Empty;

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public UpdateViewModel(
        UpdateChecker checker,
        IVersionProvider version,
        IUpdateDownloader downloader,
        IUpdateLauncher launcher,
        SettingsService settings,
        string rid
    )
    {
        _checker = checker;
        _version = version;
        _downloader = downloader;
        _launcher = launcher;
        _settings = settings;
        _rid = rid;
    }

    public async Task CheckOnLaunchAsync(CancellationToken ct)
    {
        try
        {
            var settings = _settings.Load();
            var result = await _checker
                .CheckAsync(_version.Current, settings.UpdateChannel, _rid, ct)
                .ConfigureAwait(true);

            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save(settings);

            if (
                result.UpdateAvailable
                && result.Release is not null
                && !string.Equals(result.Release.TagName, settings.SkippedVersion, StringComparison.Ordinal)
            )
            {
                Apply(result);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Launch checks fail silently; the manual command surfaces errors.
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking for updates...";
        try
        {
            var settings = _settings.Load();
            var result = await _checker
                .CheckAsync(_version.Current, settings.UpdateChannel, _rid, CancellationToken.None)
                .ConfigureAwait(true);

            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save(settings);

            if (result.UpdateAvailable && result.Release is not null)
            {
                Apply(result);
                StatusMessage = $"Markus {result.Release.Version} is available.";
            }
            else
            {
                IsUpdateAvailable = false;
                StatusMessage = $"You're on the latest version ({_version.Current}).";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            StatusMessage = "Could not check for updates.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (_release is null)
        {
            return;
        }

        if (_asset is null)
        {
            _launcher.OpenReleasePage(_release.HtmlUrl);
            return;
        }

        IsBusy = true;
        StatusMessage = "Downloading update...";
        try
        {
            var dir = Path.Combine(_settings.SettingsDirectory, "updates");
            var path = await _downloader
                .DownloadAndVerifyAsync(_asset, dir, CancellationToken.None)
                .ConfigureAwait(true);
            _launcher.OpenArtifact(path);
            StatusMessage = "Opening installer...";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            StatusMessage = "Download failed. Opening the release page instead.";
            _launcher.OpenReleasePage(_release.HtmlUrl);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ViewReleaseNotes()
    {
        if (_release is not null)
        {
            _launcher.OpenReleasePage(_release.HtmlUrl);
        }
    }

    [RelayCommand]
    private void Dismiss()
    {
        IsUpdateAvailable = false;
    }

    [RelayCommand]
    private void Skip()
    {
        if (_release is null)
        {
            return;
        }

        var settings = _settings.Load();
        settings.SkippedVersion = _release.TagName;
        _settings.Save(settings);
        IsUpdateAvailable = false;
    }

    private void Apply(UpdateCheckResult result)
    {
        _release = result.Release;
        _asset = result.Asset;
        AvailableVersion = result.Release!.Version.ToString();
        ReleaseNotes = result.Release.Notes;
        IsUpdateAvailable = true;
    }
}
```

Note: `HttpRequestException` and `TaskCanceledException` come from `System.Net.Http` and `System.Threading.Tasks`, both already globally imported via `ImplicitUsings`. If the build flags either as not found, add `using System.Net.Http;` at the top.

- [ ] **Step 5: Run to verify it passes**

Same filter. Expected: PASS (all 7).

- [ ] **Step 6: Format and commit**

Commit message: `feat(updates): add update view model`. Confirm before committing.

---

## Phase 5: UI and app wiring (manual verification)

These tasks have no unit tests. After each, build with `~/.dotnet`; full UI behavior is checked in the manual checklist (Task 21).

### Task 15: Expose Update on MainWindowViewModel

**Files:**
- Modify: `src/Markus/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Add the property**

Add this observable property alongside the other `[ObservableProperty]` fields (for example right after `_lastModifiedText` near the top of the class). It is assigned by `App` after construction, so bindings to `Update.*` are null-safe until then.

```csharp
    [ObservableProperty]
    private UpdateViewModel? _update;
```

- [ ] **Step 2: Build**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 3: Format and commit**

Commit message: `feat(updates): expose update view model on main window`. Confirm before committing.

---

### Task 16: Compose update services and the launch check in App

**Files:**
- Modify: `src/Markus/App.axaml.cs`

- [ ] **Step 1: Build and assign the update view model**

Inside `OnFrameworkInitializationCompleted`, in the `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` block, insert the following immediately after the existing line `var isSpawnedChild = FileOpenRouter.IsSpawnMarker(desktop.Args);`:

```csharp
            var updateVm = new MainWindowViewModelUpdateFactory().Create();
            vm.Update = updateVm;
```

Rather than a factory indirection, inline it directly (simpler, no new file):

```csharp
            var updateVm = new ViewModels.UpdateViewModel(
                new Services.Updates.UpdateChecker(new Services.Updates.GitHubReleaseFeed()),
                new Services.Updates.AssemblyVersionProvider(),
                new Services.Updates.UpdateDownloader(),
                new Services.Updates.UpdateLauncher(),
                Services.ServiceLocator.Settings,
                Services.Updates.RuntimeRid.Current
            );
            vm.Update = updateVm;
```

(Use the inline version. The factory line above is illustrative only; do not create a factory.)

- [ ] **Step 2: Add the launch check**

After the existing `Views.Platform.MacosAppleEventHandler.Register(...)` line (still inside the desktop block), add:

```csharp
            if (
                Services.Updates.UpdatePolicy.ShouldAutoCheck(
                    settings.CheckForUpdatesOnLaunch,
                    isSpawnedChild,
                    settings.LastUpdateCheckUtc,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromHours(20)
                )
            )
            {
                Dispatcher.UIThread.Post(() => _ = updateVm.CheckOnLaunchAsync(App.ShutdownToken));
            }
```

`Dispatcher` is already imported in this file. `UpdateViewModel` lives in `Markus.ViewModels`, referenced here fully qualified as `ViewModels.UpdateViewModel`.

- [ ] **Step 3: Build**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 4: Format and commit**

Commit message: `feat(updates): compose update services and launch check`. Confirm before committing.

---

### Task 17: Update banner in MainWindow

**Files:**
- Modify: `src/Markus/Views/MainWindow.axaml`

The main content grid is `<Grid RowDefinitions="Auto,*,Auto">` with the toolbar in row 0, content in row 1, and the status bar in row 2. Insert a new auto-height row for the banner between the toolbar and the content.

- [ ] **Step 1: Add a row to the grid**

Find `<Grid RowDefinitions="Auto,*,Auto">` and change it to:

```xml
    <Grid RowDefinitions="Auto,Auto,*,Auto">
```

- [ ] **Step 2: Renumber the content and status rows**

Find `<Grid Grid.Row="1" ColumnDefinitions="Auto,Auto,*,Auto,Auto">` and change `Grid.Row="1"` to `Grid.Row="2"`.

Find `<Border Grid.Row="2" Classes="glass statusbar"` and change `Grid.Row="2"` to `Grid.Row="3"`.

- [ ] **Step 3: Insert the banner**

Immediately before the content grid (the `<Grid Grid.Row="2" ColumnDefinitions="Auto,Auto,*,Auto,Auto">` element you just renumbered), insert:

```xml
      <!-- Update banner: shown only when a newer release was found. -->
      <Border Grid.Row="1"
              IsVisible="{Binding Update.IsUpdateAvailable, FallbackValue=False}"
              Background="{DynamicResource AccentSoft}"
              BorderBrush="{DynamicResource GlassBorder}"
              BorderThickness="0,0,0,1"
              Padding="16,8">
        <Grid ColumnDefinitions="*,Auto,Auto,Auto,Auto">
          <TextBlock Grid.Column="0"
                     VerticalAlignment="Center"
                     Foreground="{DynamicResource TextHigh}"
                     Text="{Binding Update.AvailableVersion, StringFormat='A new version of Markus ({0}) is available'}"/>
          <Button Grid.Column="1" Margin="8,0,0,0" Content="Download"
                  Command="{Binding Update.DownloadCommand}"/>
          <Button Grid.Column="2" Margin="8,0,0,0" Content="Release notes"
                  Command="{Binding Update.ViewReleaseNotesCommand}"/>
          <Button Grid.Column="3" Margin="8,0,0,0" Content="Skip"
                  Command="{Binding Update.SkipCommand}"/>
          <Button Grid.Column="4" Margin="8,0,0,0" Content="Dismiss"
                  Command="{Binding Update.DismissCommand}"/>
        </Grid>
      </Border>
```

- [ ] **Step 4: Build and run the app**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success. Visual verification happens in Task 21.

- [ ] **Step 5: Format and commit**

CSharpier does not format `.axaml`. Commit message: `feat(updates): add update banner to main window`. Confirm before committing.

---

### Task 18: Menu item and AppCommands.CheckForUpdates

**Files:**
- Modify: `src/Markus/Services/AppCommands.cs`
- Modify: `src/Markus/App.axaml`

- [ ] **Step 1: Add the command**

In `AppCommands.cs`, add a property next to `About`:

```csharp
    public static ICommand CheckForUpdates { get; } = new RelayCommand(RunCheckForUpdates);
```

And add the handler (place it with the other private methods):

```csharp
    private static void RunCheckForUpdates()
    {
        if (
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel vm
            && vm.Update is not null
        )
        {
            vm.Update.CheckForUpdatesCommand.Execute(null);
        }
    }
```

- [ ] **Step 2: Add the menu item**

In `App.axaml`, inside `<NativeMenu>`, add this item right after the `About Markus` item (before the first separator):

```xml
      <NativeMenuItem Header="Check for Updates…"
                      Command="{x:Static services:AppCommands.CheckForUpdates}"/>
```

- [ ] **Step 3: Build**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 4: Format and commit**

Commit message: `feat(updates): add check-for-updates menu item`. Confirm before committing.

---

### Task 19: Updates settings category

**Files:**
- Modify: `src/Markus/ViewModels/SettingsCategory.cs`
- Modify: `src/Markus/ViewModels/SettingsViewModel.cs`
- Modify: `src/Markus/Views/SettingsWindow.axaml`

- [ ] **Step 1: Add the enum value**

In `SettingsCategory.cs`, add `Updates,` after `General,`:

```csharp
internal enum SettingsCategory
{
    Appearance,
    View,
    Shortcuts,
    General,
    Updates,
}
```

- [ ] **Step 2: Extend SettingsViewModel**

In `SettingsViewModel.cs`:

a) Add a channels list near the other `AvailableX` lists:

```csharp
    public static readonly IReadOnlyList<UpdateChannel> AvailableUpdateChannels = new UpdateChannel[]
    {
        UpdateChannel.Stable,
        UpdateChannel.Prerelease,
    };
```

b) Add the category to the `Categories` array (after the `General` entry):

```csharp
        new SettingsCategoryItem(SettingsCategory.Updates, "Updates", IconData.Refresh),
```

c) Add a `NotifyPropertyChangedFor` to `_selectedCategory` (with the existing ones):

```csharp
    [NotifyPropertyChangedFor(nameof(IsUpdatesSelected))]
```

d) Add the selection helper (next to `IsGeneralSelected`):

```csharp
    public bool IsUpdatesSelected => SelectedCategory.Kind is SettingsCategory.Updates;
```

e) Add the two observable properties (with the other `[ObservableProperty]` fields):

```csharp
    [ObservableProperty]
    private bool _checkForUpdatesOnLaunch;

    [ObservableProperty]
    private UpdateChannel _updateChannel;
```

f) Initialize them in the constructor (with the other `_x = settings.X;` lines):

```csharp
        _checkForUpdatesOnLaunch = settings.CheckForUpdatesOnLaunch;
        _updateChannel = settings.UpdateChannel;
```

g) Add them to `RestoreDefaults` (with the other `X = defaults.X;` lines):

```csharp
        CheckForUpdatesOnLaunch = defaults.CheckForUpdatesOnLaunch;
        UpdateChannel = defaults.UpdateChannel;
```

h) Add the auto-save hooks (with the other `partial void OnXChanged`):

```csharp
    partial void OnCheckForUpdatesOnLaunchChanged(bool value)
    {
        Settings.CheckForUpdatesOnLaunch = value;
        Service.Save(Settings);
    }

    partial void OnUpdateChannelChanged(UpdateChannel value)
    {
        Settings.UpdateChannel = value;
        Service.Save(Settings);
    }
```

- [ ] **Step 3: Add the settings panel**

In `SettingsWindow.axaml`, after the `<!-- ====== General ====== -->` StackPanel closes, add:

```xml
        <!-- ====== Updates ====== -->
        <StackPanel IsVisible="{Binding IsUpdatesSelected}" Spacing="0">
          <Grid Classes="row" ColumnDefinitions="160,*">
            <TextBlock Grid.Column="0" Text="On launch:" Classes="rowLabel"/>
            <StackPanel Grid.Column="1" Spacing="6">
              <CheckBox Content="Check for updates automatically"
                        IsChecked="{Binding CheckForUpdatesOnLaunch}"/>
              <TextBlock Classes="hint"
                         Text="Markus checks GitHub at most once a day and shows a banner when a newer version is available."/>
            </StackPanel>
          </Grid>

          <Grid Classes="row" ColumnDefinitions="160,*">
            <TextBlock Grid.Column="0" Text="Channel:" Classes="rowLabel"/>
            <StackPanel Grid.Column="1" Spacing="6">
              <ComboBox MinWidth="220"
                        HorizontalAlignment="Left"
                        ItemsSource="{x:Static vm:SettingsViewModel.AvailableUpdateChannels}"
                        SelectedItem="{Binding UpdateChannel}"/>
              <TextBlock Classes="hint"
                         Text="Stable offers released versions. Prerelease also offers alpha and beta builds."/>
            </StackPanel>
          </Grid>
        </StackPanel>
```

- [ ] **Step 4: Build**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx`
Expected: success.

- [ ] **Step 5: Format and commit**

Format the two `.cs` files with CSharpier. Commit message: `feat(updates): add updates settings category`. Confirm before committing.

---

### Task 20: Check-for-Updates button in About (optional polish)

**Files:**
- Modify: `src/Markus/Views/AboutWindow.axaml`

Read `AboutWindow.axaml` first to match its button layout and `xmlns`. Add a `services` namespace (`xmlns:services="using:Markus.Services"`) if absent, then a button near the GitHub/License buttons:

```xml
<Button Content="Check for Updates"
        Command="{x:Static services:AppCommands.CheckForUpdates}"/>
```

This reuses the same command as the menu. Because the About window is modal over the main window, the banner appears on the main window after it closes.

- [ ] **Step 1: Build, format, commit**

Commit message: `feat(updates): add check-for-updates button to about`. Confirm before committing. This task is optional and may be skipped without affecting the core feature.

---

## Phase 6: Final gate and manual verification

### Task 21: Full build, tests, format, and manual checklist

**Files:** none (verification only).

- [ ] **Step 1: Full Release build (warnings are errors)**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet build Markus.slnx -c Release`
Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Full test suite**

Run: `PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test Markus.slnx -c Release`
Expected: all tests pass, including the prior suite plus the new update tests.

- [ ] **Step 3: Format gate**

Run: `PATH="$HOME/.dotnet:$PATH" dotnet csharpier check .`
Expected: no files reported as needing formatting.

- [ ] **Step 4: Manual verification (cannot be automated here)**

Build the local app bundle and exercise the flow:

```bash
RID=osx-arm64 ./scripts/build-macos-app.sh
open dist/Markus.app
```

Check each of these:
- With `CheckForUpdatesOnLaunch` on and the installed version older than the latest GitHub release, the banner appears shortly after launch. If the installed version equals the latest, no banner appears.
- The menu item "Check for Updates…" triggers a check. When up to date it reports the latest version (observe via status / banner state); when newer it shows the banner.
- "Download" fetches the matching `osx-arm64` dmg into `~/Library/Application Support/Markus/updates/`, verifies the checksum, and mounts the dmg with the drag-to-Applications view. Confirm the mounted app launches without a Gatekeeper quarantine prompt.
- "Skip" hides the banner and does not show it again for that version on the next launch. "Dismiss" hides it for the session only.
- In Settings, the new "Updates" category toggles auto-check and switches the channel. With the channel set to Prerelease, a prerelease tag newer than the installed version produces a banner.
- Offline (disable network): launch shows no banner and no error; the manual menu check reports a friendly "Could not check for updates."
- Edge case: temporarily remove the matching asset reference (or test against a release that has no `osx-arm64` asset) and confirm "Download" opens the release page instead.

- [ ] **Step 5: Final commit**

If any formatting changes were applied in Step 3, commit them. Commit message: `chore(updates): formatting and final gate`. Confirm before committing.

---

## Self-Review (plan author)

**Spec coverage.** Every spec section maps to tasks:
- Automation level (notify plus one-click download): Tasks 12, 13, 14, 17.
- Trigger (launch debounce plus manual menu): Tasks 7, 16, 18; manual command in Task 14.
- On click (download, verify, open file): Tasks 12, 13, 14.
- Channel (selectable, default Stable): Tasks 2, 10, 19.
- Distribution context / source-generated JSON: Task 8.
- Version comparison and asset selection: Tasks 1, 3, 5, 10, 11.
- Settings additions: Tasks 2, 19.
- UI (banner, menu, settings, About): Tasks 17, 18, 19, 20.
- Error handling and edge cases: Tasks 10 (no-asset fallback), 12 (checksum), 14 (silent launch / friendly manual / release-page fallback), 7 and 16 (spawned-child gate, debounce).
- Test strategy: Tasks 1, 2, 3, 5, 6, 7, 8, 10, 11, 14 are TDD; IO and UI are manual per Task 21.
- Out of scope (no silent replacement, no relaunch trampoline): respected; nothing implements in-place swap.

**Placeholder scan.** No "TBD"/"TODO". The only illustrative-not-literal snippet (the factory line in Task 16) is explicitly flagged with the inline replacement to use.

**Type consistency.** `UpdateCheckResult` uses `UpdateAvailable`, `Release`, `Asset` everywhere. `ReleaseInfo` uses `Version`, `TagName`, `IsPrerelease`, `Notes`, `HtmlUrl`, `Assets` consistently across Tasks 4, 8, 10, 14. `UpdateChecker.CheckAsync(SemVer, UpdateChannel, string, CancellationToken)` matches its call sites in Tasks 10 and 14. `IUpdateDownloader.DownloadAndVerifyAsync(ReleaseAsset, string, CancellationToken)` and `IUpdateLauncher.OpenArtifact/OpenReleasePage` match the fakes and the view model. `SemVer.TryParse/Parse/CompareTo` and operators match all usages.

**Known soft spot.** The generated JSON member name `GitHubReleaseJsonContext.Default.ListGitHubRelease` (Task 9) depends on the source generator's naming for `List<GitHubRelease>`. If it differs, the build output names the correct member; adjust the reference. This does not affect the unit-tested mapper, which uses `.GitHubRelease`.

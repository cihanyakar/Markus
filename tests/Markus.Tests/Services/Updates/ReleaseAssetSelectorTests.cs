using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class ReleaseAssetSelectorTests
{
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

    [Fact]
    public void Select_SkipsBlockmapSidecar()
    {
        // electron-updater-style auto-update metadata that shipped alongside
        // the real installer must never be downloaded as the artifact itself.
        var assets = new[] { Asset("Markus-v0.5.0-osx-arm64.dmg.blockmap"), Asset("Markus-v0.5.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_SkipsYamlMetadata()
    {
        var assets = new[] { Asset("latest-mac.yml"), Asset("Markus-v0.5.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_PrefersCanonicalAssetOverDebugBuild()
    {
        // Both contain the rid; the canonical installer (shorter, no suffix)
        // is the one users expect to install.
        var assets = new[] { Asset("Markus-v0.5.0-osx-arm64-debug.dmg"), Asset("Markus-v0.5.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_PrefersCanonicalRegardlessOfOrder()
    {
        // Same as above but with the canonical asset listed second — the
        // original first-match logic would have picked the debug build.
        var assets = new[] { Asset("Markus-v0.5.0-osx-arm64.dmg"), Asset("Markus-v0.5.0-osx-arm64-symbols.zip") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_SkipsNonInstallerExtensions()
    {
        // Standalone .txt/.md/.json that happen to embed the rid in their
        // filename must not be treated as installers.
        var assets = new[] { Asset("Markus-v0.5.0-osx-arm64-changelog.txt"), Asset("Markus-v0.5.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_PrefersInstallerExtensionOverPortableArchive()
    {
        // Two assets of comparable length: one canonical installer (.dmg) and
        // one portable archive (.zip). The original shortest-name tie-break
        // had no semantic relationship to "canonical installer" and could pick
        // the .zip non-deterministically. Installer extensions must win.
        var assets = new[] { Asset("Markus-v0.5.0-osx-arm64.zip"), Asset("Markus-v0.5.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v0.5.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_PrefersInstallerExtensionEvenWhenLonger()
    {
        // The canonical installer name has the version embedded
        // (`Markus-v1.0.0-osx-arm64.dmg`, 28 chars). A portable archive
        // without the version (`Markus-osx-arm64.zip`, 20 chars) is shorter.
        // Installer-extension priority must beat shortest-name.
        var assets = new[] { Asset("Markus-osx-arm64.zip"), Asset("Markus-v1.0.0-osx-arm64.dmg") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v1.0.0-osx-arm64.dmg");
    }

    [Fact]
    public void Select_FallsBackToShortestWhenNoInstallerExtension()
    {
        // No .dmg/.pkg/etc; both candidates are archives. Tie-break by
        // shortest filename for deterministic selection.
        var assets = new[] { Asset("Markus-v1.0.0-osx-arm64-debug.zip"), Asset("Markus-v1.0.0-osx-arm64.zip") };

        ReleaseAssetSelector.Select(assets, "osx-arm64")!.Name.ShouldBe("Markus-v1.0.0-osx-arm64.zip");
    }

    private static ReleaseAsset Asset(string name)
    {
        return new ReleaseAsset
        {
            Name = name,
            DownloadUrl = new Uri($"https://example.test/{name}"),
            Size = 1,
        };
    }
}

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

using Markus.Models;
using Markus.Services.Updates;

namespace Markus.Tests.Services.Updates;

public sealed class UpdateCheckerTests
{
    private const string Rid = "osx-arm64";

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

    private static ReleaseInfo Release(string tag, bool prerelease, params string[] assetNames)
    {
        var assets = assetNames
            .Select(n => new ReleaseAsset
            {
                Name = n,
                DownloadUrl = new Uri($"https://x.test/{n}"),
                Size = 1,
            })
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
}

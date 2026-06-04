using Markus.Models;
using Markus.Services;
using Markus.Services.Updates;
using Markus.Tests.Services.Updates;
using Markus.Tests.ViewModels.Updates;
using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

public sealed class UpdateViewModelTests
{
    private const string Rid = "osx-arm64";

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

    private static ReleaseInfo Release(string tag, params string[] assetNames)
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
}

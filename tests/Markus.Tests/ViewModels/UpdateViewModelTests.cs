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

    [Fact]
    public async Task CheckOnLaunch_DoesNotClobberConcurrentSettingsWrites()
    {
        // A slow CheckOnLaunchAsync is awaiting GitHub when an unrelated
        // settings write fires (Skip command, recent-files persistence, any
        // auto-save partial). Re-loading settings inside the post-await save
        // must preserve the concurrent writer's changes — the original code
        // wrote a stale snapshot back, wiping them.
        var feedGate = new TaskCompletionSource();
        var feed = new GatedReleaseFeed(feedGate.Task, Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        var (vm, settings) = BuildWithFeed("0.4.0", feed);

        var checkTask = vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        var s = settings.Load();
        s.SkippedVersion = "v0.9.9";
        settings.Save(s);

        feedGate.SetResult();
        await checkTask;

        var final = settings.Load();
        final.SkippedVersion.ShouldBe("v0.9.9");
        final.LastUpdateCheckUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ManualCheck_DoesNotClobberConcurrentSettingsWrites()
    {
        var feedGate = new TaskCompletionSource();
        var feed = new GatedReleaseFeed(feedGate.Task, Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        var (vm, settings) = BuildWithFeed("0.4.0", feed);

        var checkTask = vm.CheckForUpdatesCommand.ExecuteAsync(null);

        var s = settings.Load();
        s.SkippedVersion = "v0.9.9";
        settings.Save(s);

        feedGate.SetResult();
        await checkTask;

        settings.Load().SkippedVersion.ShouldBe("v0.9.9");
    }

    [Fact]
    public async Task Skip_DoesNotClobberConcurrentSettingsWrites()
    {
        // Skip itself does Load/mutate/Save with no await, so the race window
        // is narrower than CheckOnLaunch, but a concurrent writer firing
        // between Skip's Load and Save (e.g. a Changed-event subscriber that
        // synchronously saves on another field) would still get clobbered.
        // The fix re-Loads inside Skip and applies only its own field.
        var (vm, settings, _, _) = Build("0.4.0", Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        await vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        // Simulate a concurrent writer mutating an unrelated field; Skip
        // must preserve it.
        var concurrent = settings.Load();
        concurrent.Theme = "ConcurrentTheme";
        settings.Save(concurrent);

        vm.SkipCommand.Execute(null);

        var final = settings.Load();
        final.SkippedVersion.ShouldBe("v0.5.0");
        final.Theme.ShouldBe("ConcurrentTheme");
    }

    [Fact]
    public async Task CheckOnLaunch_PreservesSettings_WhenFileBecomesUnreadableDuringAwait()
    {
        // BUG-3 fix did `var fresh = _settings.Load()` post-await, but Load
        // silently returns a fresh-defaults AppSettings on corruption /
        // missing file. If the settings file becomes briefly unreadable
        // during the await (sibling process crash, backup tool, transient
        // permission drop), the subsequent Save(fresh) would wipe every
        // user customization on disk. The fix uses TryLoad and falls back
        // to the pre-await snapshot when the re-load fails.
        var feedGate = new TaskCompletionSource();
        var feed = new GatedReleaseFeed(feedGate.Task, Release("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg"));
        var (vm, settings) = BuildWithFeed("0.4.0", feed);

        // Pre-populate real user settings.
        var s = settings.Load();
        s.Theme = "PreservedTheme";
        s.FontSize = 22.0;
        s.SkippedVersion = "v0.4.5";
        settings.Save(s);

        var checkTask = vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        // Simulate transient corruption during the await: write invalid JSON.
        var path = Path.Combine(settings.SettingsDirectory, "settings.json");
        await File.WriteAllTextAsync(path, "{not valid json", TestContext.Current.CancellationToken);

        feedGate.SetResult();
        await checkTask;

        // After the check completes, restore-by-re-write must NOT have
        // happened in a way that wipes user fields.
        // Either the file stays unreadable (Save was skipped — TryLoad
        // returned false, no fallback save) or it was rewritten with the
        // pre-await snapshot (TryLoad returned false, fallback to snapshot
        // preserved Theme/FontSize/SkippedVersion).
        var final = settings.Load();
        final.Theme.ShouldBe("PreservedTheme");
        final.FontSize.ShouldBe(22.0);
        final.SkippedVersion.ShouldBe("v0.4.5");
    }

    [Fact]
    public async Task CheckOnLaunch_HonorsChannelChangedDuringAwait()
    {
        // BUG-3 fix re-loaded settings post-await to preserve concurrent
        // writes — but the channel used for the network query was still the
        // pre-await snapshot. If the user switches channels during the
        // round-trip, the recorded LastUpdateCheckUtc pins a query that
        // doesn't match the user's now-current channel, so UpdatePolicy then
        // suppresses re-checks against the new channel. The fix re-derives
        // the channel from the fresh settings and re-queries if it changed.
        var feedGate = new TaskCompletionSource();
        var release = PrereleaseInfo("v0.5.0", "Markus-v0.5.0-osx-arm64.dmg");
        var feed = new GatedReleaseFeed(feedGate.Task, release);
        var (vm, settings) = BuildWithFeed("0.4.0", feed);

        // Start on Stable, then flip to Prerelease while the await is pending.
        var initial = settings.Load();
        initial.UpdateChannel = Markus.Models.UpdateChannel.Stable;
        settings.Save(initial);

        var checkTask = vm.CheckOnLaunchAsync(TestContext.Current.CancellationToken);

        var mid = settings.Load();
        mid.UpdateChannel = Markus.Models.UpdateChannel.Prerelease;
        settings.Save(mid);

        feedGate.SetResult();
        await checkTask;

        // With the channel flipped to Prerelease during the await, the user
        // should see the v0.5.0 prerelease as available — the recorded
        // timestamp must correspond to the post-await channel, not the
        // pre-await one.
        vm.IsUpdateAvailable.ShouldBeTrue();
        vm.AvailableVersion.ShouldBe("0.5.0");
    }

    private static ReleaseInfo Release(string tag, params string[] assetNames)
    {
        return BuildRelease(tag, isPrerelease: false, assetNames);
    }

    private static ReleaseInfo PrereleaseInfo(string tag, params string[] assetNames)
    {
        return BuildRelease(tag, isPrerelease: true, assetNames);
    }

    private static ReleaseInfo BuildRelease(string tag, bool isPrerelease, string[] assetNames)
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
            IsPrerelease = isPrerelease,
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

    private static (UpdateViewModel Vm, SettingsService Settings) BuildWithFeed(
        string currentVersion,
        IReleaseFeed feed
    )
    {
        var dir = Path.Combine(Path.GetTempPath(), "markus-tests", Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(dir);
        var checker = new UpdateChecker(feed);
        var dl = new FakeUpdateDownloader();
        var lf = new FakeUpdateLauncher();
        var vm = new UpdateViewModel(checker, new FixedVersionProvider(currentVersion), dl, lf, settings, Rid);
        return (vm, settings);
    }
}

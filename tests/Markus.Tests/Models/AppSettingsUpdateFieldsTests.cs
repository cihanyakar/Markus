using Markus.Models;
using Markus.Services;

namespace Markus.Tests.Models;

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

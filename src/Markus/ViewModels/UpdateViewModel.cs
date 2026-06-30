using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Services;
using Markus.Services.Updates;

namespace Markus.ViewModels;

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
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
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

            // Re-load before the save so concurrent writers (Skip, auto-save
            // partials, recent-files persistence) made during the await are
            // preserved. TryLoad's `false` return means the file became
            // unreadable during the await; fall back to the pre-await
            // snapshot so we never overwrite user customizations with the
            // defaults Load would have substituted on read failure.
            var fresh = _settings.TryLoad(out var reloaded) ? reloaded : settings;
            // If the user flipped channels during the await, the filtered
            // result reflects the stale channel. Re-query so the recorded
            // LastUpdateCheckUtc corresponds to the channel the user is now on.
            if (fresh.UpdateChannel != settings.UpdateChannel)
            {
                result = await _checker
                    .CheckAsync(_version.Current, fresh.UpdateChannel, _rid, ct)
                    .ConfigureAwait(true);
            }
            fresh.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save(fresh);

            if (
                result.UpdateAvailable
                && result.Release is not null
                && !string.Equals(result.Release.TagName, fresh.SkippedVersion, StringComparison.Ordinal)
            )
            {
                Apply(result);
            }
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or OperationCanceledException
                        or InvalidOperationException
                        or System.Text.Json.JsonException
            )
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
                .CheckAsync(_version.Current, settings.UpdateChannel, _rid, Markus.App.ShutdownToken)
                .ConfigureAwait(true);

            // Re-load before save to preserve concurrent writes during the
            // network round-trip. See CheckOnLaunchAsync for the same rule.
            // Fall back to the pre-await snapshot when the file became
            // briefly unreadable so we never wipe user settings with defaults.
            var fresh = _settings.TryLoad(out var reloaded) ? reloaded : settings;
            // Re-query if channel changed during the await so the recorded
            // timestamp matches the user's now-current channel.
            if (fresh.UpdateChannel != settings.UpdateChannel)
            {
                result = await _checker
                    .CheckAsync(_version.Current, fresh.UpdateChannel, _rid, Markus.App.ShutdownToken)
                    .ConfigureAwait(true);
            }
            fresh.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settings.Save(fresh);

            ApplyManualResult(result);
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or OperationCanceledException
                        or InvalidOperationException
                        or System.Text.Json.JsonException
            )
        {
            StatusMessage = "Could not check for updates.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyManualResult(UpdateCheckResult result)
    {
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

    private bool CanDownload()
    {
        return !IsBusy;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (_release is null)
        {
            return;
        }

        if (_asset is null)
        {
            StatusMessage = "No installer for this platform; opening the release page.";
            _launcher.OpenReleasePage(_release.HtmlUrl);
            return;
        }

        IsBusy = true;
        StatusMessage = "Downloading update...";
        try
        {
            var dir = Path.Combine(_settings.SettingsDirectory, "updates");
            var path = await _downloader
                .DownloadAndVerifyAsync(_asset, dir, Markus.App.ShutdownToken)
                .ConfigureAwait(true);
            _launcher.OpenArtifact(path);
            StatusMessage = "Opening installer...";
        }
        catch (Exception ex)
            when (ex
                    is HttpRequestException
                        or OperationCanceledException
                        or InvalidOperationException
                        or System.Text.Json.JsonException
            )
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

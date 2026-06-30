using System.Text.Json;
using Markus.Models;

namespace Markus.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Process-lifetime singleton (ServiceLocator); FlushPendingSave is the explicit release point for the timer."
)]
internal sealed class SettingsService
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    private readonly string _settingsPath;
    private readonly Lock _debounceLock = new Lock();
    private AppSettings? _pendingSave;

    // Timer is intentionally not Disposed in the service lifetime: the
    // service lives for the entire process and any pending callback is
    // a Save we want to run before exit. FlushPendingSave (called on
    // shutdown paths) is the explicit release point.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "S2930:IDisposables should be disposed",
        Justification = "Process-lifetime singleton; FlushPendingSave is the release point."
    )]
    private System.Threading.Timer? _saveTimer;

    public SettingsService()
        : this(ResolveSettingsDirectory()) { }

    internal SettingsService(string settingsDirectory)
    {
        SettingsDirectory = settingsDirectory;
        _settingsPath = Path.Combine(SettingsDirectory, "settings.json");
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public string SettingsDirectory { get; }

    public AppSettings Load()
    {
        TryLoad(out var loaded);
        return loaded;
    }

    /// <summary>
    /// Loads settings, returning false (and a fresh defaults instance) when
    /// the file is missing or unreadable. Callers that perform a load /
    /// long-running operation / save sequence MUST use this method on the
    /// post-await re-load so a transient corruption during the long-running
    /// operation does not cause the subsequent save to wipe the user's
    /// preferences with default values.
    /// </summary>
    public bool TryLoad(out AppSettings settings)
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                settings = new AppSettings();
                return false;
            }

            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
            if (loaded is null)
            {
                settings = new AppSettings();
                return false;
            }
            // Clamp out-of-range numeric values from a corrupted or hand-edited
            // file so they cannot break the UI (invisible text, zero tab, etc.).
            loaded.Normalize();
            settings = loaded;
            return true;
        }
        catch (JsonException)
        {
            settings = new AppSettings();
            return false;
        }
        catch (IOException)
        {
            settings = new AppSettings();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // A permission problem on settings.json must not crash launch (Save
            // already swallows the same); fall back to defaults for the session.
            settings = new AppSettings();
            return false;
        }
    }

    public void Save(AppSettings settings)
    {
        // Save sits on hot paths (slider drags, partial-property setters,
        // PersistSession at close). AtomicFileWriter can raise IOException
        // for transient AV / sharing-violation / quota conditions; absorbing
        // it here keeps a transient persistence failure from crashing the
        // UI thread. Listeners on Changed are still notified so in-memory
        // state stays consistent for the running session.
        var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        try
        {
            AtomicFileWriter.WriteAllText(_settingsPath, json);
        }
        catch (IOException)
        {
            // Best-effort persistence; the user's next save will retry.
        }
        catch (UnauthorizedAccessException)
        {
            // Same posture as IOException.
        }
        Changed?.Invoke(this, new SettingsChangedEventArgs(settings));
    }

    /// <summary>
    /// Schedules a Save that runs after <paramref name="delay"/> of quiet
    /// (default 250 ms). Successive calls coalesce: only the last settings
    /// snapshot is written. Slider-bound properties (FontSize, MermaidScale)
    /// fire many ValueChanged events per drag; this collapses them into a
    /// single trailing write so the UI thread is not blocked on a fsync per
    /// tick. Pair with FlushPendingSave on shutdown so the last value is
    /// not lost when the user closes the window mid-drag.
    /// </summary>
    public void SaveDebounced(AppSettings settings, TimeSpan? delay = null)
    {
        var interval = delay ?? DefaultDebounce;
        lock (_debounceLock)
        {
            _pendingSave = settings;
            _saveTimer?.Dispose();
            _saveTimer = new System.Threading.Timer(_ => FlushPendingSave(), null, interval, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Forces any pending debounced save to run synchronously. Call from
    /// shutdown paths (PersistSession on window close) so a slider drag in
    /// progress at exit lands on disk.
    /// </summary>
    public void FlushPendingSave()
    {
        AppSettings? toSave;
        lock (_debounceLock)
        {
            toSave = _pendingSave;
            _pendingSave = null;
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
        if (toSave is not null)
        {
            Save(toSave);
        }
    }

    private static string ResolveSettingsDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Markus");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Markus");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdg))
        {
            return Path.Combine(xdg, "Markus");
        }

        var unixHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(unixHome, ".config", "Markus");
    }
}

internal sealed class SettingsChangedEventArgs : EventArgs
{
    public SettingsChangedEventArgs(AppSettings settings)
    {
        Settings = settings;
    }

    public AppSettings Settings { get; }
}

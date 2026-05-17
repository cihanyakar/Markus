using System.Text.Json;
using System.Text.Json.Serialization;
using Markus.Models;

namespace Markus.Services;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        SettingsDirectory = ResolveSettingsDirectory();
        _settingsPath = Path.Combine(SettingsDirectory, "settings.json");
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public string SettingsDirectory { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return loaded ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
        Changed?.Invoke(this, new SettingsChangedEventArgs(settings));
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

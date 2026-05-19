using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;

namespace Markus.Services;

/// <summary>
/// Reads/writes a user-editable map of action id → KeyGesture overrides.
/// Persisted as <c>shortcuts.json</c> next to <c>settings.json</c>. Falls
/// back to <see cref="ShortcutAction.DefaultGesture"/> when no override is
/// set so a brand-new install ships with the default layout.
/// </summary>
internal sealed class KeyBindingService
{
    private readonly string _path;
    private readonly KeyBindingMap _map;

    public KeyBindingService(string settingsDirectory)
    {
        _path = Path.Combine(settingsDirectory, "shortcuts.json");
        _map = Load();
    }

    public event EventHandler? Changed;

    public KeyGesture? GetGesture(ShortcutAction action)
    {
        if (_map.Overrides.TryGetValue(action.Id, out var raw))
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }
            try
            {
                return KeyGesture.Parse(raw);
            }
            catch (FormatException)
            {
                // Fall through to default.
            }
            catch (ArgumentException)
            {
                // Fall through to default.
            }
        }
        return action.DefaultGesture;
    }

    public void SetGesture(string actionId, KeyGesture? gesture)
    {
        if (gesture is null)
        {
            // Explicit empty string flags "user cleared this action". A null
            // entry from Overrides means "use the default".
            _map.Overrides[actionId] = string.Empty;
        }
        else
        {
            _map.Overrides[actionId] = gesture.ToString(null, CultureInfo.InvariantCulture);
        }
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetGesture(string actionId)
    {
        if (_map.Overrides.Remove(actionId))
        {
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResetAll()
    {
        if (_map.Overrides.Count == 0)
        {
            return;
        }
        _map.Overrides.Clear();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string? FindConflict(string actionId, KeyGesture gesture)
    {
        var raw = gesture.ToString(null, CultureInfo.InvariantCulture);
        foreach (var action in ShortcutActions.All)
        {
            if (string.Equals(action.Id, actionId, StringComparison.Ordinal))
            {
                continue;
            }
            var current = GetGesture(action);
            if (current is null)
            {
                continue;
            }
            if (string.Equals(current.ToString(null, CultureInfo.InvariantCulture), raw, StringComparison.Ordinal))
            {
                return action.DisplayName;
            }
        }
        return null;
    }

    private KeyBindingMap Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new KeyBindingMap();
            }
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize(json, KeyBindingMapJsonContext.Default.KeyBindingMap)
                ?? new KeyBindingMap();
        }
        catch (JsonException)
        {
            return new KeyBindingMap();
        }
        catch (IOException)
        {
            return new KeyBindingMap();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? string.Empty);
            var json = JsonSerializer.Serialize(_map, KeyBindingMapJsonContext.Default.KeyBindingMap);
            File.WriteAllText(_path, json);
        }
        catch (IOException)
        {
            // Best-effort: persistence failure should not crash the editor.
        }
    }
}

internal sealed class KeyBindingMap
{
    public Dictionary<string, string> Overrides { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(KeyBindingMap))]
internal sealed partial class KeyBindingMapJsonContext : JsonSerializerContext { }

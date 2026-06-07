using Avalonia;
using Avalonia.Styling;

namespace Markus.Views;

internal static class TextMateThemeResolver
{
    // "Auto" follows the app theme variant; any other value must match a key in
    // ThemeFiles, otherwise it falls back to Dark+.
    public const string AutoKey = "Auto";

    // Calm, low-contrast palettes for Auto in both variants: Quiet Light and the
    // warm, muted Kimbie Dark, so markdown headings and links read as soft tones
    // rather than the saturated defaults.
    private const string AutoLightFile = "quietlight-color-theme.json";
    private const string AutoDarkFile = "kimbie-dark-color-theme.json";
    private const string FallbackFile = "dark_plus.json";

    // Maps a stored code-theme key to its embedded theme resource. Keys mirror the
    // CodeThemeOption values in SettingsViewModel.
    private static readonly Dictionary<string, string> ThemeFiles = new(StringComparer.Ordinal)
    {
        ["LightPlus"] = "light_plus.json",
        ["DarkPlus"] = "dark_plus.json",
        ["Light"] = "light_vs.json",
        ["Dark"] = "dark_vs.json",
        ["Monokai"] = "monokai-color-theme.json",
        ["MonokaiDimmed"] = "dimmed-monokai-color-theme.json",
        ["DimmedMonokai"] = "dimmed-monokai-color-theme.json",
        ["SolarizedLight"] = "solarized-light-color-theme.json",
        ["SolarizedDark"] = "solarized-dark-color-theme.json",
        ["QuietLight"] = "quietlight-color-theme.json",
        ["KimbieDark"] = "kimbie-dark-color-theme.json",
        ["Abbys"] = "abyss-color-theme.json",
        ["Red"] = "Red-color-theme.json",
        ["TomorrowNightBlue"] = "tomorrow-night-blue-color-theme.json",
    };

    public static event EventHandler? Changed;

    public static string Key { get; private set; } = AutoKey;

    public static void Update(string? value)
    {
        var next = string.IsNullOrEmpty(value) ? AutoKey : value;
        if (string.Equals(Key, next, StringComparison.Ordinal))
        {
            return;
        }
        Key = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // Returns the embedded theme resource filename for the current selection.
    public static string Resolve()
    {
        if (string.Equals(Key, AutoKey, StringComparison.Ordinal))
        {
            var variant = Application.Current?.ActualThemeVariant;
            return variant == ThemeVariant.Light ? AutoLightFile : AutoDarkFile;
        }
        return ThemeFiles.TryGetValue(Key, out var file) ? file : FallbackFile;
    }
}

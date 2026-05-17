using Avalonia;
using Avalonia.Styling;
using TextMateSharp.Grammars;

namespace Markus.Views;

internal static class TextMateThemeResolver
{
    // "Auto" follows the app theme variant; any other value must parse to ThemeName.
    public const string AutoKey = "Auto";

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

    public static ThemeName Resolve()
    {
        if (string.Equals(Key, AutoKey, StringComparison.Ordinal))
        {
            var variant = Application.Current?.ActualThemeVariant;
            return variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
        }
        return Enum.TryParse<ThemeName>(Key, ignoreCase: false, out var parsed) ? parsed : ThemeName.DarkPlus;
    }
}

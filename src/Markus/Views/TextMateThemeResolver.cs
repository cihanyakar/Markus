using Avalonia;
using Avalonia.Styling;
using TextMateSharp.Grammars;

namespace Markus.Views;

internal static class TextMateThemeResolver
{
    public static ThemeName Resolve()
    {
        var variant = Application.Current?.ActualThemeVariant;
        return variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
    }
}

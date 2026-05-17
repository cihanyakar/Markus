using Avalonia;
using Avalonia.Styling;

namespace Markus.Services;

internal static class ThemeApplicator
{
    public static void Apply(string mode)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.RequestedThemeVariant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Models;
using Markus.Services;

namespace Markus.ViewModels;

internal sealed partial class SettingsViewModel : ViewModelBase
{
    public static readonly IReadOnlyList<RendererKind> AvailableRenderers = new RendererKind[]
    {
        RendererKind.Native,
        RendererKind.Placeholder,
    };

    public static readonly IReadOnlyList<ViewMode> AvailableViewModes = new ViewMode[]
    {
        ViewMode.Source,
        ViewMode.Preview,
        ViewMode.SplitVertical,
        ViewMode.SplitHorizontal,
        ViewMode.Detached,
    };

    public static readonly IReadOnlyList<LanguageOption> AvailableLanguages = new LanguageOption[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("tr", "Türkçe"),
    };

    public static readonly IReadOnlyList<ThemeOption> AvailableThemes = new ThemeOption[]
    {
        new ThemeOption("GitHubLight", "GitHub Light", false),
        new ThemeOption("GitHubDark", "GitHub Dark", true),
        new ThemeOption("SolarizedLight", "Solarized Light", false),
        new ThemeOption("SolarizedDark", "Solarized Dark", true),
        new ThemeOption("Nord", "Nord", true),
        new ThemeOption("TokyoNight", "Tokyo Night", true),
    };

    [ObservableProperty]
    private RendererKind _renderer;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private ViewMode _defaultViewMode;

    [ObservableProperty]
    private bool _showOutline;

    [ObservableProperty]
    private double _fontSize;

    public SettingsViewModel(SettingsService service, AppSettings settings)
    {
        Service = service;
        Settings = settings;
        _renderer = settings.Renderer;
        _language = settings.Language;
        _theme = settings.Theme;
        _defaultViewMode = settings.DefaultViewMode;
        _showOutline = settings.ShowOutline;
        _fontSize = settings.FontSize;
    }

    public SettingsService Service { get; }

    public AppSettings Settings { get; }

    public string SettingsDirectory => Service.SettingsDirectory;

    [RelayCommand]
    private void Save()
    {
        Settings.Renderer = Renderer;
        Settings.Language = Language;
        Settings.Theme = Theme;
        Settings.DefaultViewMode = DefaultViewMode;
        Settings.ShowOutline = ShowOutline;
        Settings.FontSize = FontSize;
        Service.Save(Settings);
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        Renderer = defaults.Renderer;
        Language = defaults.Language;
        Theme = defaults.Theme;
        DefaultViewMode = defaults.DefaultViewMode;
        ShowOutline = defaults.ShowOutline;
        FontSize = defaults.FontSize;
    }
}

internal sealed record LanguageOption(string code, string display)
{
    public string Code => code;

    public string Display => display;
}

internal sealed record ThemeOption(string key, string display, bool isDark)
{
    public string Key => key;

    public string Display => display;

    public bool IsDark => isDark;
}

using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Markus.Models;
using Markus.Services;
using Markus.Views;

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

    public static readonly IReadOnlyList<SettingsCategoryItem> Categories = new SettingsCategoryItem[]
    {
        new SettingsCategoryItem(SettingsCategory.Appearance, "Appearance", IconData.Palette),
        new SettingsCategoryItem(SettingsCategory.View, "View", IconData.ViewQuilt),
        new SettingsCategoryItem(SettingsCategory.General, "General", IconData.Tune),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppearanceSelected))]
    [NotifyPropertyChangedFor(nameof(IsViewSelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralSelected))]
    private SettingsCategoryItem _selectedCategory = Categories[0];

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

    public bool IsAppearanceSelected => SelectedCategory.Kind is SettingsCategory.Appearance;

    public bool IsViewSelected => SelectedCategory.Kind is SettingsCategory.View;

    public bool IsGeneralSelected => SelectedCategory.Kind is SettingsCategory.General;

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

    // ---- Auto-save on any property change ---------------------------------

    partial void OnRendererChanged(RendererKind value)
    {
        Settings.Renderer = value;
        Service.Save(Settings);
    }

    partial void OnLanguageChanged(string value)
    {
        Settings.Language = value;
        Service.Save(Settings);
    }

    partial void OnThemeChanged(string value)
    {
        Settings.Theme = value;
        Service.Save(Settings);
    }

    partial void OnDefaultViewModeChanged(ViewMode value)
    {
        Settings.DefaultViewMode = value;
        Service.Save(Settings);
    }

    partial void OnShowOutlineChanged(bool value)
    {
        Settings.ShowOutline = value;
        Service.Save(Settings);
    }

    partial void OnFontSizeChanged(double value)
    {
        Settings.FontSize = value;
        Service.Save(Settings);
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

internal sealed record SettingsCategoryItem(SettingsCategory kind, string name, StreamGeometry icon)
{
    public SettingsCategory Kind => kind;

    public string Name => name;

    public StreamGeometry Icon => icon;
}

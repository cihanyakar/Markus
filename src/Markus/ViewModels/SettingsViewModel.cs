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

    public static readonly IReadOnlyList<OutlinePlacement> AvailableOutlinePlacements = new OutlinePlacement[]
    {
        OutlinePlacement.Left,
        OutlinePlacement.Right,
    };

    public static readonly IReadOnlyList<LanguageOption> AvailableLanguages = new LanguageOption[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("tr", "Türkçe"),
    };

    public static readonly IReadOnlyList<string> AvailableMonoFonts = new[]
    {
        "Iosevka",
        "JetBrains Mono",
        "Cascadia Code",
    };

    public static readonly IReadOnlyList<string> AvailableThemeModes = new[] { "System", "Light", "Dark" };

    public static readonly IReadOnlyList<ThemeOption> AvailableThemes = new ThemeOption[]
    {
        new ThemeOption("GitHubLight", "GitHub Light", false),
        new ThemeOption("GitHubDark", "GitHub Dark", true),
        new ThemeOption("SolarizedLight", "Solarized Light", false),
        new ThemeOption("SolarizedDark", "Solarized Dark", true),
        new ThemeOption("Nord", "Nord", true),
        new ThemeOption("TokyoNight", "Tokyo Night", true),
    };

    public static readonly IReadOnlyList<CodeThemeOption> AvailableCodeThemes = new CodeThemeOption[]
    {
        new CodeThemeOption("Auto", "Auto (match app)"),
        new CodeThemeOption("LightPlus", "Light+"),
        new CodeThemeOption("DarkPlus", "Dark+"),
        new CodeThemeOption("Light", "Light"),
        new CodeThemeOption("Dark", "Dark"),
        new CodeThemeOption("Monokai", "Monokai"),
        new CodeThemeOption("MonokaiDimmed", "Monokai Dimmed"),
        new CodeThemeOption("DimmedMonokai", "Dimmed Monokai"),
        new CodeThemeOption("SolarizedLight", "Solarized Light"),
        new CodeThemeOption("SolarizedDark", "Solarized Dark"),
        new CodeThemeOption("QuietLight", "Quiet Light"),
        new CodeThemeOption("KimbieDark", "Kimbie Dark"),
        new CodeThemeOption("Abbys", "Abyss"),
        new CodeThemeOption("Red", "Red"),
        new CodeThemeOption("TomorrowNightBlue", "Tomorrow Night Blue"),
    };

    public static readonly IReadOnlyList<SettingsCategoryItem> Categories = new SettingsCategoryItem[]
    {
        new SettingsCategoryItem(SettingsCategory.Appearance, "Appearance", IconData.Palette),
        new SettingsCategoryItem(SettingsCategory.View, "View", IconData.ViewQuilt),
        new SettingsCategoryItem(SettingsCategory.Shortcuts, "Shortcuts", IconData.UnfoldLess),
        new SettingsCategoryItem(SettingsCategory.General, "General", IconData.Tune),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppearanceSelected))]
    [NotifyPropertyChangedFor(nameof(IsViewSelected))]
    [NotifyPropertyChangedFor(nameof(IsShortcutsSelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralSelected))]
    private SettingsCategoryItem _selectedCategory = Categories[0];

    [ObservableProperty]
    private RendererKind _renderer;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private string _codeTheme;

    [ObservableProperty]
    private ViewMode _defaultViewMode;

    [ObservableProperty]
    private bool _showOutline;

    [ObservableProperty]
    private OutlinePlacement _outlinePlacement;

    [ObservableProperty]
    private bool _restoreSessionOnLaunch;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private string _monoFont;

    [ObservableProperty]
    private string _themeMode;

    [ObservableProperty]
    private bool _isSourceSoftWrap;

    [ObservableProperty]
    private bool _isPreviewSoftWrap;

    [ObservableProperty]
    private double _mermaidScale;

    public SettingsViewModel(SettingsService service, AppSettings settings)
    {
        Service = service;
        Settings = settings;
        _renderer = settings.Renderer;
        _language = settings.Language;
        _theme = settings.Theme;
        _codeTheme = settings.CodeTheme;
        _defaultViewMode = settings.DefaultViewMode;
        _showOutline = settings.ShowOutline;
        _outlinePlacement = settings.OutlinePlacement;
        _restoreSessionOnLaunch = settings.RestoreSessionOnLaunch;
        _fontSize = settings.FontSize;
        _monoFont = settings.MonoFont;
        _themeMode = settings.ThemeMode;
        _isSourceSoftWrap = settings.IsSourceSoftWrap;
        _isPreviewSoftWrap = settings.IsPreviewSoftWrap;
        _mermaidScale = settings.MermaidScale;
    }

    public SettingsService Service { get; }

    public AppSettings Settings { get; }

    public string SettingsDirectory => Service.SettingsDirectory;

    public bool IsAppearanceSelected => SelectedCategory.Kind is SettingsCategory.Appearance;

    public bool IsViewSelected => SelectedCategory.Kind is SettingsCategory.View;

    public bool IsShortcutsSelected => SelectedCategory.Kind is SettingsCategory.Shortcuts;

    public bool IsGeneralSelected => SelectedCategory.Kind is SettingsCategory.General;

    public IReadOnlyList<ShortcutBindingViewModel> Shortcuts { get; } =
        Markus
            .Services.ShortcutActions.All.Select(a => new ShortcutBindingViewModel(
                a,
                Markus.Services.ServiceLocator.Keys
            ))
            .ToList();

    [RelayCommand]
    private void RestoreDefaults()
    {
        var defaults = new AppSettings();
        Renderer = defaults.Renderer;
        Language = defaults.Language;
        Theme = defaults.Theme;
        CodeTheme = defaults.CodeTheme;
        DefaultViewMode = defaults.DefaultViewMode;
        ShowOutline = defaults.ShowOutline;
        OutlinePlacement = defaults.OutlinePlacement;
        RestoreSessionOnLaunch = defaults.RestoreSessionOnLaunch;
        FontSize = defaults.FontSize;
        MonoFont = defaults.MonoFont;
        ThemeMode = defaults.ThemeMode;
        IsSourceSoftWrap = defaults.IsSourceSoftWrap;
        IsPreviewSoftWrap = defaults.IsPreviewSoftWrap;
        MermaidScale = defaults.MermaidScale;
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

    partial void OnCodeThemeChanged(string value)
    {
        Settings.CodeTheme = value;
        Service.Save(Settings);
    }

    partial void OnDefaultViewModeChanged(ViewMode value)
    {
        Settings.DefaultViewMode = value;
        Service.Save(Settings);
    }

    partial void OnOutlinePlacementChanged(OutlinePlacement value)
    {
        Settings.OutlinePlacement = value;
        Service.Save(Settings);
    }

    partial void OnRestoreSessionOnLaunchChanged(bool value)
    {
        Settings.RestoreSessionOnLaunch = value;
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

    partial void OnMonoFontChanged(string value)
    {
        Settings.MonoFont = value;
        Service.Save(Settings);
    }

    partial void OnThemeModeChanged(string value)
    {
        Settings.ThemeMode = value;
        Service.Save(Settings);
    }

    partial void OnIsSourceSoftWrapChanged(bool value)
    {
        Settings.IsSourceSoftWrap = value;
        Service.Save(Settings);
    }

    partial void OnIsPreviewSoftWrapChanged(bool value)
    {
        Settings.IsPreviewSoftWrap = value;
        Service.Save(Settings);
    }

    partial void OnMermaidScaleChanged(double value)
    {
        Settings.MermaidScale = value;
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

    public Avalonia.Media.IBrush Swatch
    {
        get
        {
            var theme = Rendering.MarkdownThemes.Resolve(key);
            return new Avalonia.Media.SolidColorBrush(theme.Background);
        }
    }
}

internal sealed record CodeThemeOption(string key, string display)
{
    public string Key => key;

    public string Display => display;
}

internal sealed record SettingsCategoryItem(SettingsCategory kind, string name, StreamGeometry icon)
{
    public SettingsCategory Kind => kind;

    public string Name => name;

    public StreamGeometry Icon => icon;
}

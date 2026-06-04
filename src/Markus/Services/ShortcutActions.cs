using Avalonia.Input;

namespace Markus.Services;

/// <summary>
/// Canonical catalog of every action Markus binds to a keyboard shortcut.
/// Editing this list expands what the Settings → Shortcuts pane shows and
/// what the user can rebind through <see cref="KeyBindingService"/>.
/// </summary>
internal static class ShortcutActions
{
    public const string CategoryFile = "File";
    public const string CategoryEdit = "Edit";
    public const string CategoryView = "View";
    public const string CategoryMarkdown = "Markdown";
    public const string CategoryTools = "Tools";

    public static readonly ShortcutAction OpenFile = new(
        "file.open",
        "Open file…",
        CategoryFile,
        new KeyGesture(Key.O, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction Save = new(
        "file.save",
        "Save",
        CategoryFile,
        new KeyGesture(Key.S, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction Reload = new(
        "file.reload",
        "Reload",
        CategoryFile,
        new KeyGesture(Key.R, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction NewScratch = new(
        "file.new",
        "New scratch",
        CategoryFile,
        new KeyGesture(Key.N, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction Find = new(
        "edit.find",
        "Find",
        CategoryEdit,
        new KeyGesture(Key.F, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction FindNext = new(
        "edit.find-next",
        "Find Next",
        CategoryEdit,
        new KeyGesture(Key.G, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction FindPrevious = new(
        "edit.find-previous",
        "Find Previous",
        CategoryEdit,
        new KeyGesture(Key.G, KeyModifiers.Meta | KeyModifiers.Shift)
    );

    public static readonly ShortcutAction FormatTables = new(
        "edit.format-tables",
        "Format Tables",
        CategoryEdit,
        new KeyGesture(Key.F, KeyModifiers.Meta | KeyModifiers.Shift)
    );

    public static readonly ShortcutAction ToggleOutline = new(
        "view.toggle-outline",
        "Toggle Outline",
        CategoryView,
        new KeyGesture(Key.B, KeyModifiers.Meta | KeyModifiers.Alt)
    );

    public static readonly ShortcutAction ToggleScrollLock = new(
        "view.scroll-lock",
        "Toggle Scroll Lock",
        CategoryView,
        new KeyGesture(Key.L, KeyModifiers.Meta | KeyModifiers.Shift)
    );

    public static readonly ShortcutAction ToggleFocusMode = new(
        "view.focus-mode",
        "Toggle Focus Mode",
        CategoryView,
        new KeyGesture(Key.M, KeyModifiers.Meta | KeyModifiers.Shift)
    );

    public static readonly ShortcutAction SourceOnly = new(
        "view.source",
        "Source View",
        CategoryView,
        new KeyGesture(Key.D1, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction PreviewOnly = new(
        "view.preview",
        "Preview View",
        CategoryView,
        new KeyGesture(Key.D2, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction SplitVertical = new(
        "view.split-vertical",
        "Split Vertical",
        CategoryView,
        new KeyGesture(Key.D3, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction SplitHorizontal = new(
        "view.split-horizontal",
        "Split Horizontal",
        CategoryView,
        new KeyGesture(Key.D4, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction DetachedPreview = new(
        "view.detached",
        "Detached Preview",
        CategoryView,
        new KeyGesture(Key.D5, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction Bold = new(
        "markdown.bold",
        "Bold",
        CategoryMarkdown,
        new KeyGesture(Key.B, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction Italic = new(
        "markdown.italic",
        "Italic",
        CategoryMarkdown,
        new KeyGesture(Key.I, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction SelectLine = new(
        "markdown.select-line",
        "Select Line",
        CategoryMarkdown,
        new KeyGesture(Key.L, KeyModifiers.Meta)
    );

    public static readonly ShortcutAction CommandPalette = new(
        "tools.command-palette",
        "Command Palette",
        CategoryTools,
        new KeyGesture(Key.K, KeyModifiers.Meta)
    );

    public static readonly IReadOnlyList<ShortcutAction> All = new[]
    {
        OpenFile,
        Save,
        Reload,
        NewScratch,
        Find,
        FindNext,
        FindPrevious,
        FormatTables,
        ToggleOutline,
        ToggleScrollLock,
        ToggleFocusMode,
        SourceOnly,
        PreviewOnly,
        SplitVertical,
        SplitHorizontal,
        DetachedPreview,
        Bold,
        Italic,
        SelectLine,
        CommandPalette,
    };
}

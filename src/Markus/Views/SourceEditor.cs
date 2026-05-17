using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Markus.Views;

internal sealed class SourceEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<SourceEditor, string?>(
        nameof(Text),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );

    public static readonly StyledProperty<string> GrammarLanguageProperty = AvaloniaProperty.Register<
        SourceEditor,
        string
    >(nameof(GrammarLanguage), "md");

    public static readonly StyledProperty<FontFamily> MonoFontFamilyProperty = AvaloniaProperty.Register<
        SourceEditor,
        FontFamily
    >(nameof(MonoFontFamily), new FontFamily("Iosevka,JetBrains Mono,Consolas,Menlo,monospace"));

    private readonly TextEditor _editor;
    private RegistryOptions? _registry;
    private TextMate.Installation? _textMate;
    private bool _syncing;

    public SourceEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = false,
            FontSize = 14,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.Transparent,
            Padding = new Thickness(28, 24),
        };
        _editor.TextChanged += OnEditorTextChanged;
        Content = _editor;
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string GrammarLanguage
    {
        get => GetValue(GrammarLanguageProperty);
        set => SetValue(GrammarLanguageProperty, value);
    }

    public FontFamily MonoFontFamily
    {
        get => GetValue(MonoFontFamilyProperty);
        set => SetValue(MonoFontFamilyProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InstallTextMate();
        // Re-apply current text *after* TextMate has been installed, in case
        // installation rebuilt the document.
        ApplyText(Text);

        if (Application.Current is { } app)
        {
            app.PropertyChanged += OnApplicationPropertyChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.PropertyChanged -= OnApplicationPropertyChanged;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            ApplyText(change.GetNewValue<string?>());
        }
        else if (change.Property == MonoFontFamilyProperty)
        {
            _editor.FontFamily = change.GetNewValue<FontFamily>();
        }
        else if (change.Property == GrammarLanguageProperty && _textMate is not null)
        {
            ApplyGrammar();
        }
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Application.ActualThemeVariantProperty && _textMate is not null)
        {
            InstallTextMate();
            ApplyText(Text);
        }
    }

    private void InstallTextMate()
    {
        _textMate?.Dispose();
        var themeName = TextMateThemeResolver.Resolve();
        _registry = new RegistryOptions(themeName);
        _textMate = _editor.InstallTextMate(_registry);
        ApplyGrammar();
    }

    private void ApplyGrammar()
    {
        if (_registry is null || _textMate is null)
        {
            return;
        }
        var grammar = _registry.GetScopeByLanguageId(GrammarLanguage);
        if (!string.IsNullOrEmpty(grammar))
        {
            _textMate.SetGrammar(grammar);
        }
    }

    private void ApplyText(string? value)
    {
        if (_syncing)
        {
            return;
        }
        var next = value ?? string.Empty;
        if (string.Equals(_editor.Document.Text, next, StringComparison.Ordinal))
        {
            return;
        }
        _syncing = true;
        _editor.Document.Text = next;
        _syncing = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        Text = _editor.Document.Text;
        _syncing = false;
    }
}

internal static class TextMateThemeResolver
{
    public static ThemeName Resolve()
    {
        var variant = Application.Current?.ActualThemeVariant;
        return variant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;
    }
}

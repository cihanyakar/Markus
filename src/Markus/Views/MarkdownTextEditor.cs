using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Markus.Views;

/// <summary>
/// AvaloniaEdit's TextEditor.Text is a plain CLR property (not a StyledProperty),
/// so XAML compiled bindings cannot reach it. This subclass adds a real
/// StyledProperty (<see cref="BoundTextProperty"/>) that mirrors
/// <c>Document.Text</c> in both directions, and wires TextMate for markdown
/// highlighting on attach.
/// </summary>
internal sealed class MarkdownTextEditor : TextEditor
{
    public static readonly StyledProperty<string?> BoundTextProperty = AvaloniaProperty.Register<
        MarkdownTextEditor,
        string?
    >(nameof(BoundText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> GrammarLanguageProperty = AvaloniaProperty.Register<
        MarkdownTextEditor,
        string
    >(nameof(GrammarLanguage), "markdown");

    private RegistryOptions? _registry;
    private TextMate.Installation? _textMate;
    private SearchPanel? _searchPanel;
    private bool _syncing;

    public MarkdownTextEditor()
    {
        ShowLineNumbers = false;
        FontSize = 14;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Padding = new Thickness(28, 24);
    }

    public string? BoundText
    {
        get => GetValue(BoundTextProperty);
        set => SetValue(BoundTextProperty, value);
    }

    public string GrammarLanguage
    {
        get => GetValue(GrammarLanguageProperty);
        set => SetValue(GrammarLanguageProperty, value);
    }

    public string SearchPattern
    {
        get => _searchPanel?.SearchPattern ?? string.Empty;
        set
        {
            if (EnsureSearchPanel() is { } panel)
            {
                panel.SearchPattern = value;
            }
        }
    }

    public bool SearchMatchCase
    {
        get => _searchPanel?.MatchCase ?? false;
        set
        {
            if (EnsureSearchPanel() is { } panel)
            {
                panel.MatchCase = value;
            }
        }
    }

    // Avalonia matches control styles by exact type. Without this override
    // the inherited TextEditor template isn't applied to MarkdownTextEditor,
    // so the control measures to zero and renders blank.
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public void FindNextMatch()
    {
        EnsureSearchPanel()?.FindNext();
    }

    public void FindPreviousMatch()
    {
        EnsureSearchPanel()?.FindPrevious();
    }

    public void Replace(string replacement)
    {
        if (EnsureSearchPanel() is { } panel)
        {
            panel.ReplacePattern = replacement;
            panel.ReplaceNext();
        }
    }

    public void ReplaceAll(string replacement)
    {
        if (EnsureSearchPanel() is { } panel)
        {
            panel.ReplacePattern = replacement;
            panel.ReplaceAll();
        }
    }

    public void CloseSearch()
    {
        _searchPanel?.Close();
        SearchPattern = string.Empty;
    }

    public int CountMatches(string term, bool caseSensitive)
    {
        return Markus.Services.TextSearchMath.CountMatches(Document.Text, term, caseSensitive);
    }

    public int CurrentMatchIndex(string term, bool caseSensitive)
    {
        var anchor = SelectionStart >= 0 ? SelectionStart : CaretOffset;
        return Markus.Services.TextSearchMath.CurrentMatchIndex(Document.Text, term, anchor, caseSensitive);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InstallTextMate();
        ApplyBoundText(BoundText);
        EnsureSearchPanel();

        if (Application.Current is { } app)
        {
            app.PropertyChanged += OnApplicationPropertyChanged;
        }
        TextMateThemeResolver.Changed += OnCodeThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Application.Current is { } app)
        {
            app.PropertyChanged -= OnApplicationPropertyChanged;
        }
        TextMateThemeResolver.Changed -= OnCodeThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundTextProperty)
        {
            ApplyBoundText(change.GetNewValue<string?>());
            return;
        }
        if (change.Property == GrammarLanguageProperty && _textMate is not null)
        {
            ApplyGrammar();
        }
    }

    private static void ApplyBrush(TextMate.Installation installation, string colorKey, Action<IBrush> apply)
    {
        if (!installation.TryGetThemeColor(colorKey, out var colorString))
        {
            return;
        }
        if (!Color.TryParse(colorString, out var color))
        {
            return;
        }
        apply(new SolidColorBrush(color));
    }

    private SearchPanel? EnsureSearchPanel()
    {
        _searchPanel ??= SearchPanel.Install(this);
        if (_searchPanel is { } panel)
        {
            // Open() activates the search engine + colorizer; without it,
            // setting SearchPattern silently no-ops. We hide its built-in UI
            // afterwards so our SearchOverlay is the only one on screen.
            panel.Open();
            panel.IsVisible = false;
            return panel;
        }
        return null;
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Application.ActualThemeVariantProperty && _textMate is not null)
        {
            InstallTextMate();
        }
    }

    private void OnCodeThemeChanged(object? sender, EventArgs e)
    {
        if (_textMate is not null)
        {
            InstallTextMate();
        }
    }

    private void InstallTextMate()
    {
        _textMate?.Dispose();
        var themeName = TextMateThemeResolver.Resolve();
        _registry = new RegistryOptions(themeName);
        _textMate = this.InstallTextMate(_registry);
        _textMate.AppliedTheme += OnTextMateThemeApplied;
        ApplyGrammar();
        ApplyThemeBrushes(_textMate);
    }

    private void OnTextMateThemeApplied(object? sender, TextMate.Installation installation)
    {
        ApplyThemeBrushes(installation);
    }

    private void ApplyThemeBrushes(TextMate.Installation installation)
    {
        ApplyBrush(installation, "editor.background", b => Background = b);
        ApplyBrush(installation, "editor.foreground", b => Foreground = b);
        ApplyBrush(installation, "editor.selectionBackground", b => TextArea.SelectionBrush = b);
        ApplyBrush(installation, "editorLineNumber.foreground", b => LineNumbersForeground = b);
    }

    private void ApplyGrammar()
    {
        if (_registry is null || _textMate is null)
        {
            return;
        }
        var scope = _registry.GetScopeByLanguageId(GrammarLanguage);
        if (!string.IsNullOrEmpty(scope))
        {
            _textMate.SetGrammar(scope);
        }
    }

    private void ApplyBoundText(string? value)
    {
        if (_syncing)
        {
            return;
        }
        var next = value ?? string.Empty;
        if (string.Equals(Document.Text, next, StringComparison.Ordinal))
        {
            return;
        }
        _syncing = true;
        // Replace the document instance the way the official demo does; setting
        // Document.Text alone doesn't always trigger the layout/render pipeline.
        Document = new TextDocument(next);
        Document.TextChanged += (_, _) =>
        {
            if (_syncing)
            {
                return;
            }
            _syncing = true;
            SetCurrentValue(BoundTextProperty, Document.Text);
            _syncing = false;
        };
        _syncing = false;
    }
}

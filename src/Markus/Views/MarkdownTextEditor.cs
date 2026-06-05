using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
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

    public static readonly StyledProperty<bool> TypewriterModeProperty = AvaloniaProperty.Register<
        MarkdownTextEditor,
        bool
    >(nameof(TypewriterMode));

    private static readonly Dictionary<string, string> AutoPairs = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
    {
        ["("] = ")",
        ["["] = "]",
        ["{"] = "}",
        ["\""] = "\"",
        ["`"] = "`",
    };

    private static readonly Regex ListPrefixRegex = new Regex(
        @"^(?<indent>\s*)(?<marker>[-*+]|\d+\.)\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromMilliseconds(50)
    );

    private RegistryOptions? _registry;
    private TextMate.Installation? _textMate;
    private SearchPanel? _searchPanel;
    private FoldingManager? _foldingManager;
    private MarkdownFoldingStrategy? _foldingStrategy;
    private bool _syncing;
    private bool _suppressTypewriter;

    public MarkdownTextEditor()
    {
        ShowLineNumbers = false;
        FontSize = 14;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Padding = new Thickness(18, 14);
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

    public bool TypewriterMode
    {
        get => GetValue(TypewriterModeProperty);
        set => SetValue(TypewriterModeProperty, value);
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
        InstallFolding();
        TextArea.TextEntering += OnTextEntering;
        TextArea.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        TextArea.Caret.PositionChanged += OnCaretPositionChanged;

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
        TextArea.TextEntering -= OnTextEntering;
        TextArea.RemoveHandler(KeyDownEvent, OnKeyDown);
        TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        if (_foldingManager is not null)
        {
            FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }
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

    private static string NextMarker(string marker)
    {
        if (!marker.EndsWith('.'))
        {
            return marker;
        }
        var numberPart = marker.AsSpan(0, marker.Length - 1);
        if (
            !int.TryParse(
                numberPart,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var n
            )
        )
        {
            return marker;
        }
        return (n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";
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
        // Folding is bound to the current Document; tear it down before the
        // swap so leftover folds don't reference offsets from the previous
        // document (which raises ArgumentException in the next layout pass).
        var hadFolding = _foldingManager is not null;
        if (_foldingManager is not null)
        {
            FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }
        // Replace the document instance the way the official demo does; setting
        // Document.Text alone doesn't always trigger the layout/render pipeline.
        Document = new TextDocument(next);
        Document.TextChanged += OnDocumentTextChanged;
        if (hadFolding)
        {
            InstallFolding();
        }
        _syncing = false;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(BoundTextProperty, Document.Text);
        _syncing = false;
        UpdateFoldings();
    }

    private void InstallFolding()
    {
        if (_foldingManager is not null)
        {
            return;
        }
        _foldingManager = FoldingManager.Install(TextArea);
        _foldingStrategy = new MarkdownFoldingStrategy();
        UpdateFoldings();
    }

    private void UpdateFoldings()
    {
        if (_foldingManager is null || _foldingStrategy is null)
        {
            return;
        }
        var foldings = MarkdownFoldingStrategy.CreateFoldings(Document).ToList();
        _foldingManager.UpdateFoldings(foldings, -1);
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        if (!AutoPairs.TryGetValue(e.Text, out var closing))
        {
            return;
        }
        var sel = TextArea.Selection;
        if (sel.IsEmpty)
        {
            Document.Insert(CaretOffset, e.Text + closing);
            CaretOffset -= closing.Length;
            e.Handled = true;
            return;
        }
        var seg = sel.SurroundingSegment;
        if (seg is null)
        {
            return;
        }
        var inner = Document.GetText(seg.Offset, seg.Length);
        Document.Replace(seg.Offset, seg.Length, e.Text + inner + closing);
        TextArea.ClearSelection();
        CaretOffset = seg.Offset + e.Text.Length + inner.Length;
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleMarkdownShortcut(e))
        {
            return;
        }
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && TryContinueList())
        {
            e.Handled = true;
        }
    }

    private bool HandleMarkdownShortcut(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.B:
                WrapSelection("**");
                e.Handled = true;
                return true;
            case Key.I:
                WrapSelection("*");
                e.Handled = true;
                return true;
            case Key.L:
                SelectCurrentLine();
                e.Handled = true;
                return true;
        }
        return false;
    }

    private void WrapSelection(string marker)
    {
        var sel = TextArea.Selection;
        if (sel.IsEmpty)
        {
            Document.Insert(CaretOffset, marker + marker);
            CaretOffset -= marker.Length;
            return;
        }
        var seg = sel.SurroundingSegment;
        if (seg is null)
        {
            return;
        }
        var inner = Document.GetText(seg.Offset, seg.Length);
        var doubled = marker.Length * 2;
        if (
            inner.Length >= doubled
            && inner.StartsWith(marker, StringComparison.Ordinal)
            && inner.EndsWith(marker, StringComparison.Ordinal)
        )
        {
            var stripped = inner.Substring(marker.Length, inner.Length - doubled);
            Document.Replace(seg.Offset, seg.Length, stripped);
            Select(seg.Offset, stripped.Length);
            return;
        }
        Document.Replace(seg.Offset, seg.Length, marker + inner + marker);
        Select(seg.Offset + marker.Length, inner.Length);
    }

    private void SelectCurrentLine()
    {
        var line = Document.GetLineByOffset(CaretOffset);
        Select(line.Offset, line.Length);
    }

    private bool TryContinueList()
    {
        var line = Document.GetLineByOffset(CaretOffset);
        var text = Document.GetText(line.Offset, line.Length);
        var match = ListPrefixRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }
        var prefix = match.Value;
        var content = text[prefix.Length..];
        if (string.IsNullOrWhiteSpace(content))
        {
            // Empty list item: strip the prefix to break out of the list.
            Document.Replace(line.Offset, line.Length, string.Empty);
            CaretOffset = line.Offset;
            return true;
        }
        var indent = match.Groups["indent"].Value;
        var marker = NextMarker(match.Groups["marker"].Value);
        var insertion = "\n" + indent + marker + " ";
        Document.Insert(CaretOffset, insertion);
        CaretOffset += insertion.Length;
        return true;
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (!TypewriterMode || _suppressTypewriter)
        {
            return;
        }
        var view = TextArea.TextView;
        var visual = view.GetVisualLine(TextArea.Caret.Line);
        if (visual is null)
        {
            return;
        }
        var sv = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (sv is null)
        {
            return;
        }
        var targetY = visual.VisualTop - ((sv.Viewport.Height - view.DefaultLineHeight) / 2);
        if (targetY < 0)
        {
            targetY = 0;
        }
        _suppressTypewriter = true;
        try
        {
            sv.Offset = new Vector(sv.Offset.X, targetY);
        }
        finally
        {
            _suppressTypewriter = false;
        }
    }
}

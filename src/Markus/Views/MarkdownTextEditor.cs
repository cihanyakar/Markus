using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaEdit;
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
    >(nameof(GrammarLanguage), "md");

    private RegistryOptions? _registry;
    private TextMate.Installation? _textMate;
    private bool _syncing;

    public MarkdownTextEditor()
    {
        ShowLineNumbers = false;
        FontSize = 14;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Background = Brushes.Transparent;
        Padding = new Thickness(28, 24);
        TextChanged += OnEditorTextChanged;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InstallTextMate();
        ApplyBoundText(BoundText);

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
        if (change.Property == BoundTextProperty)
        {
            ApplyBoundText(change.GetNewValue<string?>());
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
            ApplyBoundText(BoundText);
        }
    }

    private void InstallTextMate()
    {
        _textMate?.Dispose();
        var themeName = TextMateThemeResolver.Resolve();
        _registry = new RegistryOptions(themeName);
        _textMate = this.InstallTextMate(_registry);
        ApplyGrammar();
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
        Document.Text = next;
        _syncing = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_syncing)
        {
            return;
        }
        _syncing = true;
        SetCurrentValue(BoundTextProperty, Document.Text);
        _syncing = false;
    }
}

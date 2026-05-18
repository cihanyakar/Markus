using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Markus.Views;

internal sealed partial class SearchOverlay : UserControl
{
    public static readonly StyledProperty<string?> SearchTextProperty = AvaloniaProperty.Register<
        SearchOverlay,
        string?
    >(nameof(SearchText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> ReplaceTextProperty = AvaloniaProperty.Register<
        SearchOverlay,
        string?
    >(nameof(ReplaceText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> CaseSensitiveProperty = AvaloniaProperty.Register<SearchOverlay, bool>(
        nameof(CaseSensitive),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );

    public static readonly StyledProperty<bool> SupportsReplaceProperty = AvaloniaProperty.Register<
        SearchOverlay,
        bool
    >(nameof(SupportsReplace));

    public static readonly StyledProperty<bool> IsReplaceVisibleProperty = AvaloniaProperty.Register<
        SearchOverlay,
        bool
    >(nameof(IsReplaceVisible), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> MatchCountProperty = AvaloniaProperty.Register<SearchOverlay, int>(
        nameof(MatchCount)
    );

    public static readonly StyledProperty<int> ActiveIndexProperty = AvaloniaProperty.Register<SearchOverlay, int>(
        nameof(ActiveIndex),
        defaultValue: -1
    );

    public SearchOverlay()
    {
        InitializeComponent();
        if (this.FindControl<TextBox>("SearchInput") is { } input)
        {
            input.KeyDown += OnInputKeyDown;
        }
        if (this.FindControl<TextBox>("ReplaceInput") is { } replace)
        {
            replace.KeyDown += OnReplaceKeyDown;
        }
        WireButton("PrevButton", () => Prev?.Invoke(this, EventArgs.Empty));
        WireButton("NextButton", () => Next?.Invoke(this, EventArgs.Empty));
        WireButton("CloseButton", () => Close?.Invoke(this, EventArgs.Empty));
        WireButton("ReplaceButton", () => ReplaceCurrent?.Invoke(this, EventArgs.Empty));
        WireButton("ReplaceAllButton", () => ReplaceAll?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Next;

    public event EventHandler? Prev;

    public event EventHandler? Close;

    public event EventHandler? ReplaceCurrent;

    public event EventHandler? ReplaceAll;

    public string? SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string? ReplaceText
    {
        get => GetValue(ReplaceTextProperty);
        set => SetValue(ReplaceTextProperty, value);
    }

    public bool CaseSensitive
    {
        get => GetValue(CaseSensitiveProperty);
        set => SetValue(CaseSensitiveProperty, value);
    }

    public bool SupportsReplace
    {
        get => GetValue(SupportsReplaceProperty);
        set => SetValue(SupportsReplaceProperty, value);
    }

    public bool IsReplaceVisible
    {
        get => GetValue(IsReplaceVisibleProperty);
        set => SetValue(IsReplaceVisibleProperty, value);
    }

    public int MatchCount
    {
        get => GetValue(MatchCountProperty);
        set => SetValue(MatchCountProperty, value);
    }

    public int ActiveIndex
    {
        get => GetValue(ActiveIndexProperty);
        set => SetValue(ActiveIndexProperty, value);
    }

    public void FocusInput()
    {
        // Posting defers focus until after the layout pass that follows
        // IsVisible flipping to true. A direct Focus() call here is a no-op
        // because the TextBox isn't measured yet on the first frame.
        Dispatcher.UIThread.Post(
            () =>
            {
                var input = this.FindControl<TextBox>("SearchInput");
                input?.Focus();
                input?.SelectAll();
            },
            DispatcherPriority.Loaded
        );
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MatchCountProperty || change.Property == ActiveIndexProperty)
        {
            UpdateCounter();
            return;
        }
        if (change.Property == SupportsReplaceProperty && !SupportsReplace)
        {
            IsReplaceVisible = false;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WireButton(string name, Action onClick)
    {
        if (this.FindControl<Button>(name) is { } button)
        {
            button.Click += (_, _) => onClick();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var handler = e.Key switch
        {
            Key.Escape => Close,
            Key.Enter => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? Prev : Next,
            _ => null,
        };
        if (handler is null)
        {
            return;
        }
        handler.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void OnReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        var handler = e.Key switch
        {
            Key.Escape => Close,
            Key.Enter => ReplaceCurrent,
            _ => null,
        };
        if (handler is null)
        {
            return;
        }
        handler.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void UpdateCounter()
    {
        var counter = this.FindControl<TextBlock>("Counter");
        if (counter is null)
        {
            return;
        }
        counter.Text = MatchCount == 0 ? "0 / 0" : $"{ActiveIndex + 1} / {MatchCount}";
    }
}

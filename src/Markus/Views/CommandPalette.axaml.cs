using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Markus.Models;

namespace Markus.Views;

internal sealed partial class CommandPalette : UserControl
{
    public static readonly StyledProperty<string?> QueryProperty = AvaloniaProperty.Register<CommandPalette, string?>(
        nameof(Query),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );

    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<CommandPalette, int>(
        nameof(SelectedIndex),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay
    );

    private IReadOnlyList<CommandItem> _allItems = Array.Empty<CommandItem>();

    public CommandPalette()
    {
        InitializeComponent();
        if (this.FindControl<TextBox>("QueryInput") is { } input)
        {
            input.KeyDown += OnKeyDown;
        }
        if (this.FindControl<ListBox>("ResultsList") is { } list)
        {
            list.DoubleTapped += (_, _) => InvokeSelected();
        }
    }

    public event EventHandler? CloseRequested;

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public ObservableCollection<CommandItem> FilteredItems { get; } = new ObservableCollection<CommandItem>();

    public void SetItems(IReadOnlyList<CommandItem> items)
    {
        _allItems = items;
        ApplyFilter();
    }

    public void FocusInput()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                var input = this.FindControl<TextBox>("QueryInput");
                input?.Focus();
                input?.SelectAll();
            },
            DispatcherPriority.Loaded
        );
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty)
        {
            ApplyFilter();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ApplyFilter()
    {
        var query = Query ?? string.Empty;
        FilteredItems.Clear();
        foreach (var item in Match(_allItems, query))
        {
            FilteredItems.Add(item);
        }
        SelectedIndex = FilteredItems.Count > 0 ? 0 : -1;
    }

    private static IEnumerable<CommandItem> Match(IReadOnlyList<CommandItem> source, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return source;
        }
        var trimmed = query.Trim();
        return source.Where(item => MatchesQuery(item, trimmed));
    }

    private static bool MatchesQuery(CommandItem item, string query)
    {
        if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return item.Group.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var handled = e.Key switch
        {
            Key.Escape => HandleEscape(),
            Key.Enter => HandleEnter(),
            Key.Down => Move(1),
            Key.Up => Move(-1),
            _ => false,
        };
        if (handled)
        {
            e.Handled = true;
        }
    }

    private bool HandleEscape()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool HandleEnter()
    {
        InvokeSelected();
        return true;
    }

    private bool Move(int delta)
    {
        if (FilteredItems.Count == 0)
        {
            return false;
        }
        var next = SelectedIndex + delta;
        if (next < 0)
        {
            next = FilteredItems.Count - 1;
        }
        if (next >= FilteredItems.Count)
        {
            next = 0;
        }
        SelectedIndex = next;
        return true;
    }

    private void InvokeSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count)
        {
            return;
        }
        var item = FilteredItems[SelectedIndex];
        CloseRequested?.Invoke(this, EventArgs.Empty);
        item.Execute();
    }
}

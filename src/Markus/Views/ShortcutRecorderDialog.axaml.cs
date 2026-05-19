using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Markus.Views;

internal sealed partial class ShortcutRecorderDialog : Window
{
    private KeyGesture? _captured;

    public ShortcutRecorderDialog()
    {
        InitializeComponent();
        // Tunnel so we see modifier+key before any focused control consumes it.
        AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        Focusable = true;
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
            return;
        }
        // Pure modifier presses don't form a valid gesture on their own; wait
        // for the user to also press a real key.
        if (IsPureModifier(e.Key))
        {
            return;
        }
        _captured = new KeyGesture(e.Key, e.KeyModifiers);
        if (this.FindControl<TextBlock>("CapturedLabel") is { } label)
        {
            label.Text = _captured.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            label.Tag = true;
        }
        e.Handled = true;
    }

    private static bool IsPureModifier(Key key)
    {
        return key
            is Key.LeftShift
                or Key.RightShift
                or Key.LeftCtrl
                or Key.RightCtrl
                or Key.LeftAlt
                or Key.RightAlt
                or Key.LWin
                or Key.RWin;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Close(_captured);
    }
}

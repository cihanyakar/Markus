using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Markus.Views;

internal sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

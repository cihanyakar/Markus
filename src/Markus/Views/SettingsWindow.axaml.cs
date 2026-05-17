using Avalonia.Controls;
using Avalonia.Interactivity;
using Markus.Models;
using Markus.ViewModels;

namespace Markus.Views;

internal sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public AppSettings? Result { get; private set; }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            Result = vm.Settings;
        }

        Close(Result);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close(null);
    }
}

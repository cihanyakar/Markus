using Avalonia.Controls;

namespace Markus.Views;

internal sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Icon = Markus.Services.IconLoader.LoadWindowIcon();
    }
}

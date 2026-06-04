using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markus.ViewModels;

namespace Markus.Views;

/// <summary>
/// A small modal confirmation window built in code so it inherits the Fluent
/// theme without a dedicated axaml file. Each button closes the dialog with a
/// caller-supplied result that <see cref="Window.ShowDialog{TResult}"/> returns.
/// </summary>
internal sealed class ConfirmDialog : Window
{
    private readonly StackPanel _buttonBar;

    private ConfirmDialog(string heading, string message)
    {
        Title = heading;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 400;

        _buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 15,
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 440,
                },
                _buttonBar,
            },
        };
    }

    public static Task<UnsavedChangesChoice> AskDiscardAsync(Window owner, string documentTitle)
    {
        var dialog = new ConfirmDialog(
            "Unsaved changes",
            $"\"{documentTitle}\" has unsaved changes. Save them before continuing?"
        );
        dialog.AddButton("Save", UnsavedChangesChoice.Save, isDefault: true, isCancel: false);
        dialog.AddButton("Don't Save", UnsavedChangesChoice.Discard, isDefault: false, isCancel: false);
        dialog.AddButton("Cancel", UnsavedChangesChoice.Cancel, isDefault: false, isCancel: true);
        return dialog.ShowDialog<UnsavedChangesChoice>(owner);
    }

    public static Task<bool> AskReloadAsync(Window owner, string documentTitle)
    {
        var dialog = new ConfirmDialog(
            "File changed on disk",
            $"\"{documentTitle}\" changed on disk and you have unsaved edits. Reload from disk and discard your changes?"
        );
        dialog.AddButton("Reload", true, isDefault: false, isCancel: false);
        dialog.AddButton("Keep my changes", false, isDefault: true, isCancel: true);
        return dialog.ShowDialog<bool>(owner);
    }

    private void AddButton(string label, object? result, bool isDefault, bool isCancel)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 92,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        button.Click += (_, _) => Close(result);
        _buttonBar.Children.Add(button);
    }
}

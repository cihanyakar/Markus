using Avalonia.Controls;
using Markus.Models;

namespace Markus.Views;

internal sealed partial class OutlinePanel : UserControl
{
    public OutlinePanel()
    {
        InitializeComponent();
    }

    public event EventHandler<OutlineNodeSelectedEventArgs>? NodeSelected;

    private void OutlineTree_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0)
        {
            return;
        }
        if (e.AddedItems[0] is not OutlineNode node)
        {
            return;
        }
        NodeSelected?.Invoke(this, new OutlineNodeSelectedEventArgs(node));
    }
}

internal sealed class OutlineNodeSelectedEventArgs : EventArgs
{
    public OutlineNodeSelectedEventArgs(OutlineNode node)
    {
        Node = node;
    }

    public OutlineNode Node { get; }
}

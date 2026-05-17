using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Markus.Views;

internal sealed partial class DetachedPreviewWindow : Window
{
    public DetachedPreviewWindow()
    {
        InitializeComponent();
    }

    public MarkdownPreviewControl? FindDescendantPreview()
    {
        return this.GetVisualDescendants().OfType<MarkdownPreviewControl>().FirstOrDefault();
    }
}

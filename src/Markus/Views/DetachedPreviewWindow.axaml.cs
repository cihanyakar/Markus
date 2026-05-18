using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Markus.Views;

internal sealed partial class DetachedPreviewWindow : Window
{
    public DetachedPreviewWindow()
    {
        InitializeComponent();
        Icon = Markus.Services.IconLoader.LoadWindowIcon();
    }

    public MarkdownPreviewControl? FindDescendantPreview()
    {
        return this.GetVisualDescendants().OfType<MarkdownPreviewControl>().FirstOrDefault();
    }
}

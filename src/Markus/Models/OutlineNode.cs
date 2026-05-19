using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Markus.Models;

internal sealed partial class OutlineNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisible = true;

    public OutlineNode(int level, string text, int sourceLine)
    {
        Level = level;
        Text = text;
        SourceLine = sourceLine;
        Children = new ObservableCollection<OutlineNode>();
    }

    public int Level { get; }

    public string Text { get; }

    public int SourceLine { get; }

    public ObservableCollection<OutlineNode> Children { get; }
}

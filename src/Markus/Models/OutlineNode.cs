using System.Collections.ObjectModel;

namespace Markus.Models;

internal sealed class OutlineNode
{
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

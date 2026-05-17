using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markus.Models;

namespace Markus.Services;

internal static class OutlineBuilder
{
    public static IReadOnlyList<OutlineNode> Build(MarkdownDocument document)
    {
        var roots = new List<OutlineNode>();
        var stack = new Stack<OutlineNode>();

        foreach (var headingBlock in document.Descendants<HeadingBlock>())
        {
            var node = new OutlineNode(
                level: headingBlock.Level,
                text: ExtractPlainText(headingBlock.Inline),
                sourceLine: headingBlock.Line
            );

            while (stack.Count > 0 && stack.Peek().Level >= node.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Children.Add(node);
            }

            stack.Push(node);
        }

        return roots;
    }

    private static string ExtractPlainText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var inline in container)
        {
            AppendInlineText(sb, inline);
        }

        return sb.ToString().Trim();
    }

    private static void AppendInlineText(System.Text.StringBuilder sb, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                sb.Append(lit.Content.ToString());
                break;
            case CodeInline code:
                sb.Append(code.Content);
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    AppendInlineText(sb, child);
                }
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case AutolinkInline auto:
                sb.Append(auto.Url);
                break;
        }
    }
}

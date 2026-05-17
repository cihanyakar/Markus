using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markus.Rendering;

internal static class MarkdownRenderer
{
    public static FontFamily MonoFamily { get; set; } =
        new FontFamily("Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace");

    public static IEnumerable<RenderedBlock> Render(MarkdownDocument? document)
    {
        if (document is null)
        {
            yield break;
        }

        foreach (var block in document)
        {
            var control = RenderBlock(block);
            if (control is not null)
            {
                yield return new RenderedBlock(control, block.Line);
            }
        }
    }

    private static Control? RenderBlock(Block block)
    {
        return block switch
        {
            HeadingBlock h => RenderHeading(h),
            ParagraphBlock p => RenderParagraph(p),
            FencedCodeBlock f => RenderFencedCode(f),
            CodeBlock c => RenderCodeBlock(c),
            QuoteBlock q => RenderQuote(q),
            ListBlock l => RenderList(l),
            ThematicBreakBlock => RenderThematicBreak(),
            HtmlBlock html => RenderHtmlAsCode(html),
            Table t => RenderTable(t),
            _ => null,
        };
    }

    private static Control RenderHeading(HeadingBlock heading)
    {
        var size = heading.Level switch
        {
            1 => 30.0,
            2 => 24.0,
            3 => 20.0,
            4 => 17.0,
            5 => 15.0,
            _ => 14.0,
        };

        var block = new SelectableTextBlock
        {
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, heading.Level == 1 ? 8 : 12, 0, 4),
            TextWrapping = TextWrapping.Wrap,
        };
        FillInlines(block.Inlines!, heading.Inline);
        return block;
    }

    private static Control RenderParagraph(ParagraphBlock paragraph)
    {
        var block = new SelectableTextBlock
        {
            FontSize = 15,
            LineHeight = 24,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8),
        };
        FillInlines(block.Inlines!, paragraph.Inline);
        return block;
    }

    private static Control RenderFencedCode(FencedCodeBlock code)
    {
        var text = code.Lines.ToString();
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28, 128, 128, 128)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 6, 0, 10),
            Child = new SelectableTextBlock
            {
                Text = text,
                FontFamily = MonoFamily,
                FontSize = 13,
                TextWrapping = TextWrapping.NoWrap,
            },
        };
    }

    private static Control RenderCodeBlock(CodeBlock code)
    {
        var text = code.Lines.ToString();
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(28, 128, 128, 128)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 6, 0, 10),
            Child = new SelectableTextBlock
            {
                Text = text,
                FontFamily = MonoFamily,
                FontSize = 13,
                TextWrapping = TextWrapping.NoWrap,
            },
        };
    }

    private static Control RenderQuote(QuoteBlock quote)
    {
        var panel = new StackPanel { Spacing = 4 };
        foreach (var child in quote)
        {
            var rendered = RenderBlock(child);
            if (rendered is not null)
            {
                panel.Children.Add(rendered);
            }
        }

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(14, 4, 0, 4),
            Margin = new Thickness(0, 6, 0, 10),
            Child = panel,
        };
    }

    private static Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 8) };
        var index = 1;
        foreach (var listItem in list)
        {
            if (listItem is not ListItemBlock item)
            {
                continue;
            }

            var bullet = list.IsOrdered ? $"{index}." : "•";
            var marker = new TextBlock
            {
                Text = bullet,
                FontSize = 15,
                Margin = new Thickness(4, 0, 8, 0),
                MinWidth = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128)),
                VerticalAlignment = VerticalAlignment.Top,
            };

            var content = new StackPanel { Spacing = 2 };
            foreach (var child in item)
            {
                var rendered = RenderBlock(child);
                if (rendered is not null)
                {
                    content.Children.Add(rendered);
                }
            }

            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };
            Grid.SetColumn(marker, 0);
            Grid.SetColumn(content, 1);
            row.Children.Add(marker);
            row.Children.Add(content);

            panel.Children.Add(row);
            index++;
        }

        return panel;
    }

    private static Control RenderThematicBreak()
    {
        return new Border
        {
            Height = 1,
            Margin = new Thickness(0, 12, 0, 12),
            Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
        };
    }

    private static Control RenderHtmlAsCode(HtmlBlock html)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 4, 0, 8),
            CornerRadius = new CornerRadius(4),
            Child = new SelectableTextBlock
            {
                Text = html.Lines.ToString(),
                FontFamily = MonoFamily,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            },
        };
    }

    private static Control RenderTable(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        if (rows.Count == 0)
        {
            return new SelectableTextBlock { Text = "(empty table)" };
        }

        var grid = BuildTableGrid(rows);
        grid.Margin = new Thickness(0, 6, 0, 10);
        return grid;
    }

    private static Grid BuildTableGrid(List<TableRow> rows)
    {
        var grid = new Grid();
        var columnCount = rows.Max(r => r.Count);
        for (int c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }
        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
            {
                var cellBorder = RenderTableCell(rows[r][c] as TableCell);
                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        return grid;
    }

    private static Border RenderTableCell(TableCell? cell)
    {
        var cellPanel = new StackPanel();
        if (cell is not null)
        {
            foreach (var child in cell)
            {
                var rendered = RenderBlock(child);
                if (rendered is not null)
                {
                    cellPanel.Children.Add(rendered);
                }
            }
        }

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4),
            Child = cellPanel,
        };
    }

    private static void FillInlines(InlineCollection target, ContainerInline? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var inline in source)
        {
            AppendInline(target, inline);
        }
    }

    private static void AppendInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                return;
            case EmphasisInline em:
                target.Add(BuildEmphasis(em));
                return;
            case CodeInline code:
                target.Add(BuildInlineCode(code));
                return;
            case LinkInline link:
                target.Add(BuildLink(link));
                return;
            case LineBreakInline:
                target.Add(new LineBreak());
                return;
            case TaskList task:
                target.Add(new Run(task.Checked ? "☑ " : "☐ "));
                return;
            case AutolinkInline auto:
                target.Add(BuildAutoLink(auto));
                return;
            case HtmlInline raw:
                target.Add(new Run(raw.Tag));
                return;
            default:
                target.Add(new Run(inline.ToString() ?? string.Empty));
                return;
        }
    }

    private static Span BuildEmphasis(EmphasisInline em)
    {
        var span = new Span();
        if (em.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeight.Bold;
        }
        else
        {
            span.FontStyle = FontStyle.Italic;
        }
        FillInlines(span.Inlines, em);
        return span;
    }

    private static Run BuildInlineCode(CodeInline code)
    {
        return new Run(code.Content)
        {
            FontFamily = MonoFamily,
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
        };
    }

    private static Run BuildLink(LinkInline link)
    {
        var label = link.FirstChild is LiteralInline first ? first.Content.ToString() : link.Url;
        return new Run(label ?? string.Empty)
        {
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xa6, 0xff)),
        };
    }

    private static Run BuildAutoLink(AutolinkInline auto)
    {
        return new Run(auto.Url)
        {
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.FromRgb(0x58, 0xa6, 0xff)),
        };
    }
}

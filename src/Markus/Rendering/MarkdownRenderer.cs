using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Markus.Rendering;

internal static class MarkdownRenderer
{
    private static readonly FontFamily EmojiFamily = new FontFamily(
        "Apple Color Emoji,Segoe UI Emoji,Noto Color Emoji"
    );

    public static FontFamily MonoFamily { get; set; } =
        new FontFamily("Iosevka,JetBrains Mono,Cascadia Code,Consolas,Menlo,monospace");

    public static MarkdownTheme Theme { get; set; } = MarkdownThemes.GitHubDark;

    public static bool WrapCode { get; set; } = true;

    public static double BaseFontSize { get; set; } = 16.0;

    public static double MermaidScale { get; set; } = 1.0;

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
            MathBlock math => RenderMathBlock(math),
            FencedCodeBlock f when IsMermaid(f) => RenderMermaid(f),
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

    private static bool IsMermaid(FencedCodeBlock fenced)
    {
        return string.Equals(fenced.Info, "mermaid", StringComparison.OrdinalIgnoreCase);
    }

    private static Control RenderPlaceholder(string kind, string source)
    {
        var label = new TextBlock
        {
            Text = $"{kind.ToUpperInvariant()} — not yet rendered",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x70)),
            Margin = new Thickness(0, 0, 0, 6),
        };

        var raw = new SelectableTextBlock
        {
            Text = source.Trim(),
            FontFamily = MonoFamily,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6E, 0x84)),
            TextWrapping = TextWrapping.Wrap,
        };

        var panel = new StackPanel { Spacing = 0 };
        panel.Children.Add(label);
        panel.Children.Add(raw);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 0xFF, 0x3D, 0x55)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0xFF, 0x3D, 0x55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12),
            Margin = new Thickness(0, 6, 0, 10),
            Child = panel,
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

        var block = new Markus.Views.LinkInlineTextBlock
        {
            FontSize = Fs(size),
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Theme.Foreground),
            Margin = new Thickness(0, heading.Level == 1 ? 3 : 6, 0, 2),
            TextWrapping = TextWrapping.Wrap,
            Tag = heading.TryGetAttributes()?.Id,
        };
        var ctx = new InlineContext();
        FillInlines(block.Inlines!, heading.Inline, ctx);
        AttachLinks(block, ctx);
        return block;
    }

    private static Control RenderParagraph(ParagraphBlock paragraph)
    {
        var block = new Markus.Views.LinkInlineTextBlock
        {
            FontSize = Fs(15),
            LineHeight = Fs(22),
            Foreground = new SolidColorBrush(Theme.Foreground),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 4),
        };
        var ctx = new InlineContext();
        FillInlines(block.Inlines!, paragraph.Inline, ctx);
        AttachLinks(block, ctx);
        return block;
    }

    private static Control RenderFencedCode(FencedCodeBlock code)
    {
        return BuildCodeCard(code.Lines.ToString());
    }

    private static Control RenderCodeBlock(CodeBlock code)
    {
        return BuildCodeCard(code.Lines.ToString());
    }

    private static Border BuildCodeCard(string text)
    {
        return new Border
        {
            Background = new SolidColorBrush(Theme.CodeBackground),
            BorderBrush = new SolidColorBrush(Theme.CodeBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9),
            Margin = new Thickness(0, 3, 0, 5),
            Child = new SelectableTextBlock
            {
                Text = text,
                FontFamily = MonoFamily,
                FontSize = Fs(13),
                Foreground = new SolidColorBrush(Theme.CodeForeground),
                TextWrapping = WrapCode ? TextWrapping.Wrap : TextWrapping.NoWrap,
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
            BorderBrush = new SolidColorBrush(Theme.QuoteAccent),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(11, 2, 0, 2),
            Margin = new Thickness(0, 3, 0, 5),
            Child = panel,
        };
    }

    private static Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 5) };
        // Honor the source's starting number ("5. foo / 6. bar" should render
        // as 5, 6, ...) and its delimiter (period vs. paren). Falls back to 1.
        var index =
            list.IsOrdered
            && int.TryParse(
                list.OrderedStart,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var start
            )
                ? start
                : 1;
        var delimiter = list.OrderedDelimiter == '\0' ? '.' : list.OrderedDelimiter;
        foreach (var listItem in list)
        {
            if (listItem is not ListItemBlock item)
            {
                continue;
            }

            var bullet = list.IsOrdered ? $"{index}{delimiter}" : "•";
            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };
            var marker = CreateListMarker(bullet);
            var content = BuildListItemContent(item);
            Grid.SetColumn(marker, 0);
            Grid.SetColumn(content, 1);
            row.Children.Add(marker);
            row.Children.Add(content);

            panel.Children.Add(row);
            index++;
        }

        return panel;
    }

    private static TextBlock CreateListMarker(string bullet)
    {
        return new TextBlock
        {
            Text = bullet,
            FontSize = Fs(15),
            // Match the paragraph's line box (LineHeight) so the glyph sits on
            // the first text line instead of floating above it. The list strips
            // per-item paragraph margins, so the small top nudge is all that's
            // needed to optically center the dot.
            LineHeight = Fs(22),
            Margin = new Thickness(2, 2, 4, 0),
            MinWidth = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128)),
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    private static StackPanel BuildListItemContent(ListItemBlock item)
    {
        var content = new StackPanel { Spacing = 2 };
        foreach (var child in item)
        {
            var rendered = RenderBlock(child);
            if (rendered is null)
            {
                continue;
            }
            // The list owns its inter-item gap via the panel Spacing; drop a
            // direct text block's prose margins so items stay compact.
            if (rendered is SelectableTextBlock tb)
            {
                tb.Margin = new Thickness(0);
            }
            content.Children.Add(rendered);
        }
        return content;
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
                FontSize = Fs(12),
                Foreground = new SolidColorBrush(Theme.CodeForeground),
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

        var grid = BuildTableGrid(rows, table.ColumnDefinitions);
        grid.Margin = new Thickness(0, 6, 0, 10);
        return grid;
    }

    private static Grid BuildTableGrid(List<TableRow> rows, List<TableColumnDefinition> columns)
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
                var cellBorder = RenderTableCell(rows[r][c] as TableCell, ColumnAlignment(columns, c));
                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        return grid;
    }

    private static HorizontalAlignment ColumnAlignment(List<TableColumnDefinition> columns, int column)
    {
        if (column >= columns.Count)
        {
            return HorizontalAlignment.Left;
        }
        return columns[column].Alignment switch
        {
            TableColumnAlign.Center => HorizontalAlignment.Center,
            TableColumnAlign.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
    }

    private static Border RenderTableCell(TableCell? cell, HorizontalAlignment align)
    {
        var cellPanel = new StackPanel { HorizontalAlignment = align };
        var textAlign = align switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        if (cell is not null)
        {
            foreach (var child in cell)
            {
                var rendered = RenderBlock(child);
                if (rendered is null)
                {
                    continue;
                }
                if (rendered is TextBlock tb)
                {
                    tb.TextAlignment = textAlign;
                }
                cellPanel.Children.Add(rendered);
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

    private static void FillInlines(InlineCollection target, ContainerInline? source, InlineContext ctx)
    {
        if (source is null)
        {
            return;
        }

        foreach (var inline in source)
        {
            AppendInline(target, inline, ctx);
        }
    }

    private static void AppendInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline, InlineContext ctx)
    {
        switch (inline)
        {
            case LiteralInline lit:
                AppendText(target, lit.Content.ToString(), ctx);
                return;
            case EmphasisInline em:
                target.Add(BuildEmphasis(em, ctx));
                return;
            case CodeInline code:
                target.Add(BuildInlineCode(code));
                ctx.Offset += code.Content.Length;
                return;
            case LinkInline link when link.IsImage:
                target.Add(BuildImage(link));
                ctx.Offset += 1;
                return;
            case LinkInline link:
                AppendLink(target, link, ctx);
                return;
            // A hard line break, or an inline <br> / <br/> / <br /> which GitHub
            // renders as a hard break even though other raw HTML stays literal.
            case LineBreakInline hardBreak when hardBreak.IsHard:
            case HtmlInline brTag when IsLineBreakTag(brTag.Tag):
                target.Add(new LineBreak());
                ctx.Offset += 1;
                return;
            case LineBreakInline:
                // A soft break is whitespace: render it as a space so the source
                // lines flow and wrap, instead of forcing a hard line break.
                AppendText(target, " ", ctx);
                return;
            case TaskList task:
                AppendText(target, task.Checked ? "☑ " : "☐ ", ctx);
                return;
            case AutolinkInline auto:
                AppendLinkRun(target, auto.Url, auto.Url, isAnchor: false, ctx);
                return;
            case HtmlEntityInline entity:
                // Decode &copy; / &amp; / &#42; to their characters rather than
                // falling through to the default, which prints the type name.
                AppendText(target, entity.Transcoded.ToString(), ctx);
                return;
            case HtmlInline raw:
                AppendText(target, raw.Tag, ctx);
                return;
            case MathInline math:
                target.Add(BuildInlineMath(math));
                ctx.Offset += 1;
                return;
            default:
                AppendText(target, inline.ToString() ?? string.Empty, ctx);
                return;
        }
    }

    private static bool IsLineBreakTag(string tag)
    {
        // Matches <br>, <br/>, <br />, and <br ...attributes> (case-insensitive),
        // the inline HTML GitHub renders as a hard break. The "br" must be the
        // whole tag name, so <brr>, <break>, and <hr> stay literal text.
        var span = tag.AsSpan().Trim();
        if (span.Length < 4 || span[0] != '<' || span[^1] != '>')
        {
            return false;
        }
        var inner = span[1..^1].TrimStart();
        if (inner.Length < 2 || !inner[..2].Equals("br", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        // After "br" the tag ends, self-closes, or continues with attributes.
        return inner.Length == 2 || inner[2] == '/' || char.IsWhiteSpace(inner[2]);
    }

    private static void AppendText(InlineCollection target, string text, InlineContext ctx)
    {
        // Emit emoji clusters in their own runs so a following space is shaped in
        // the body font, not the wide emoji font (which otherwise leaves a large
        // gap after the emoji). The total text length is unchanged, so the link
        // offsets recorded elsewhere still line up.
        if (text.Length > 0)
        {
            var sb = new System.Text.StringBuilder();
            bool? segmentEmoji = null;
            var clusters = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (clusters.MoveNext())
            {
                var cluster = (string)clusters.Current;
                var clusterEmoji = IsEmojiCluster(cluster);
                if (segmentEmoji is { } prev && prev != clusterEmoji && sb.Length > 0)
                {
                    FlushTextRun(target, sb, prev);
                }
                segmentEmoji = clusterEmoji;
                sb.Append(cluster);
            }
            if (segmentEmoji is { } last && sb.Length > 0)
            {
                FlushTextRun(target, sb, last);
            }
        }
        ctx.Offset += text.Length;
    }

    private static void FlushTextRun(InlineCollection target, System.Text.StringBuilder sb, bool isEmoji)
    {
        var run = new Run(sb.ToString());
        if (isEmoji)
        {
            // Render the whole emoji cluster in the color-emoji font so sequences
            // whose base is ASCII (keycaps like 1 + combining enclosing keycap)
            // form the emoji glyph instead of falling back to a tofu box.
            run.FontFamily = EmojiFamily;
        }
        target.Add(run);
        sb.Clear();
    }

    private static bool IsEmojiCluster(string cluster)
    {
        if (cluster.Length == 0)
        {
            return false;
        }
        // The base scalar carries the emoji identity; ZWJ/skin-tone/selector
        // continuations live in the same grapheme cluster. A trailing U+FE0F
        // also forces emoji presentation onto an otherwise text symbol.
        var first =
            char.IsHighSurrogate(cluster[0]) && cluster.Length > 1
                ? char.ConvertToUtf32(cluster[0], cluster[1])
                : cluster[0];
        return IsEmojiScalar(first) || cluster.Contains('\uFE0F');
    }

    private static bool IsEmojiScalar(int v)
    {
        return v
            is >= 0x1F000
                and <= 0x1FAFF
                or >= 0x2600
                and <= 0x27BF
                or >= 0x2B00
                and <= 0x2BFF
                or >= 0x2300
                and <= 0x23FF;
    }

    private static void AppendLink(InlineCollection target, LinkInline link, InlineContext ctx)
    {
        var isAnchor = link.Url is { } url && url.StartsWith('#');
        var dest = isAnchor ? link.Url![1..] : link.Url ?? string.Empty;
        // Render the link's own children (which may be bold, code, multiple
        // parts) inside an accent + underline span, instead of flattening to the
        // first literal (which dropped formatted/multi-part text or fell back to
        // the raw URL). The recorded range spans all of them for click hit-testing.
        var start = ctx.Offset;
        var span = new Span
        {
            Foreground = new SolidColorBrush(Theme.Accent),
            TextDecorations = TextDecorations.Underline,
        };
        FillInlines(span.Inlines, link, ctx);
        if (ctx.Offset == start)
        {
            // Empty link text: show the destination so the link is still visible.
            var fallback = link.Url ?? string.Empty;
            span.Inlines.Add(new Run(fallback));
            ctx.Offset += fallback.Length;
        }
        target.Add(span);
        ctx.Links.Add(new Markus.Views.LinkInlineTextBlock.LinkRange(start, ctx.Offset - start, dest, isAnchor));
    }

    // Links are plain runs (styled, not embedded controls) so they share the
    // surrounding text's baseline. The character range is recorded so the
    // owning LinkInlineTextBlock can resolve clicks by hit-testing the layout.
    private static void AppendLinkRun(
        InlineCollection target,
        string label,
        string dest,
        bool isAnchor,
        InlineContext ctx
    )
    {
        target.Add(
            new Run(label)
            {
                Foreground = new SolidColorBrush(Theme.Accent),
                TextDecorations = TextDecorations.Underline,
            }
        );
        ctx.Links.Add(new Markus.Views.LinkInlineTextBlock.LinkRange(ctx.Offset, label.Length, dest, isAnchor));
        ctx.Offset += label.Length;
    }

    private static void AttachLinks(Markus.Views.LinkInlineTextBlock block, InlineContext ctx)
    {
        if (ctx.Links.Count == 0)
        {
            return;
        }
        block.SetLinks(ctx.Links);
        // Scroll only the preview that owns the clicked link, not every preview
        // (split view shows two), by walking up to the containing control.
        block.AnchorActivated = id =>
            Avalonia
                .VisualTree.VisualExtensions.FindAncestorOfType<Markus.Views.MarkdownPreviewControl>(block)
                ?.ScrollToAnchor(id);
        block.UrlActivated = url => OpenUrl(url);
    }

    private static Span BuildEmphasis(EmphasisInline em, InlineContext ctx)
    {
        var span = new Span();
        ApplyEmphasisStyle(span, em.DelimiterChar, em.DelimiterCount);
        FillInlines(span.Inlines, em, ctx);
        return span;
    }

    // The delimiter character, not just its count, decides the style. A double
    // tilde is strikethrough (it was being rendered bold); single tilde is
    // subscript, caret is superscript, double plus is inserted, double equals is
    // marked. Asterisk and underscore fall back to bold or italic.
    private static void ApplyEmphasisStyle(Span span, char delimiter, int count)
    {
        switch (delimiter)
        {
            case '~' when count >= 2:
                span.TextDecorations = TextDecorations.Strikethrough;
                break;
            case '~':
                span.BaselineAlignment = BaselineAlignment.Subscript;
                span.FontSize = Fs(11);
                break;
            case '^':
                span.BaselineAlignment = BaselineAlignment.Superscript;
                span.FontSize = Fs(11);
                break;
            case '+' when count >= 2:
                span.TextDecorations = TextDecorations.Underline;
                break;
            case '=' when count >= 2:
                span.Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xE0, 0x66));
                break;
            default:
                if (count >= 2)
                {
                    span.FontWeight = FontWeight.Bold;
                }
                else
                {
                    span.FontStyle = FontStyle.Italic;
                }
                break;
        }
    }

    private static Run BuildInlineCode(CodeInline code)
    {
        return new Run(code.Content)
        {
            FontFamily = MonoFamily,
            FontSize = 13,
            Foreground = new SolidColorBrush(Theme.CodeForeground),
            Background = new SolidColorBrush(Theme.CodeBackground),
        };
    }

    private static InlineUIContainer BuildImage(LinkInline image)
    {
        var alt = image.FirstChild is LiteralInline first ? first.Content.ToString() : null;
        return new InlineUIContainer(LoadImageOrPlaceholder(image.Url, alt));
    }

    private static Control LoadImageOrPlaceholder(string? url, string? alt)
    {
        var bitmap = TryLoadBitmap(url);
        if (bitmap is null)
        {
            return new SelectableTextBlock
            {
                Text = $"[image: {alt ?? url ?? "?"}]",
                FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Theme.Muted),
            };
        }
        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            MaxWidth = 720,
        };
    }

    private static Avalonia.Media.Imaging.Bitmap? TryLoadBitmap(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        try
        {
            if (System.IO.File.Exists(url))
            {
                return new Avalonia.Media.Imaging.Bitmap(url);
            }
        }
        catch (System.IO.IOException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        return null;
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }
            );
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Shell couldn't open the URL; swallow so a bad link doesn't crash the app.
        }
        catch (System.IO.FileNotFoundException)
        {
            // No-op: link target missing on disk.
        }
    }

    private static Avalonia.Controls.Documents.Inline BuildInlineMath(MathInline math)
    {
        var latex = math.Content.ToString();
        var painter = new CSharpMath.Avalonia.MathPainter
        {
            LaTeX = latex,
            FontSize = (float)Fs(15),
            TextColor = Theme.Foreground,
        };
        var size = painter.Measure();
        if (size.Width <= 0 || size.Height <= 0)
        {
            return new Run(latex)
            {
                FontFamily = MonoFamily,
                FontSize = Fs(13),
                Foreground = new SolidColorBrush(Theme.CodeForeground),
            };
        }
        var control = new MathPainterControl(painter, size.Width, size.Height);
        return new InlineUIContainer(control);
    }

    private static Control RenderMermaid(FencedCodeBlock fenced)
    {
        var source = fenced.Lines.ToString();
        if (!Services.MermaidRenderer.IsAvailable)
        {
            return RenderPlaceholder("mermaid", source);
        }

        return new MermaidControl(source);
    }

    private static Control RenderMathBlock(MathBlock math)
    {
        var latex = math.Lines.ToString().Trim();
        var painter = new CSharpMath.Avalonia.MathPainter
        {
            LaTeX = latex,
            FontSize = (float)Fs(20),
            TextColor = Theme.Foreground,
        };
        var size = painter.Measure();
        if (size.Width <= 0 || size.Height <= 0)
        {
            return RenderPlaceholder("math", latex);
        }
        return new MathPainterControl(painter, size.Width, size.Height)
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 12),
        };
    }

    private static double Fs(double size)
    {
        return size * BaseFontSize / 16.0;
    }

    // Accumulates inline-fill state for one text block: the running character
    // offset (so link ranges line up with the rendered TextLayout) and the
    // links discovered while walking the inlines.
    private sealed class InlineContext
    {
        public int Offset { get; set; }

        public List<Markus.Views.LinkInlineTextBlock.LinkRange> Links { get; } = new();
    }
}

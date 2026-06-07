using System.Text;
using BenchmarkDotNet.Attributes;
using Markus.Rendering;
using Markus.Services;

namespace Markus.Benchmarks;

// Measures the file-open preview pipeline: Markdig parse and the control-tree
// build done by MarkdownRenderer.Render. This is the dominant cost when opening
// a document, so the two stages are timed separately to show which one to
// attack. Render returns a lazy sequence, so it is fully enumerated to force the
// Avalonia control construction that the preview actually pays for.
[MemoryDiagnoser]
public class ParseRenderBenchmarks
{
    private string _document = string.Empty;

    [Params(40, 150)]
    public int Sections { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < Sections; i++)
        {
            builder.Append("# Heading ").Append(i).Append("\n\n");
            builder.Append("This is a paragraph with **bold**, *italic*, ~~strike~~ and `inline code`. ");
            builder.Append("It has a [link](https://example.com) and enough prose to wrap.\n\n");
            builder.Append("- list item one\n- list item two\n- list item three\n\n");
            builder.Append("| Col A | Col B | Col C |\n|:--|:-:|--:|\n| a | b | c |\n| dd | ee | ff |\n\n");
            builder.Append("```csharp\nvar x = Compute(i);\nConsole.WriteLine(x);\n```\n\n");
            builder.Append("> A blockquote line for structural variety.\n\n");
        }
        _document = builder.ToString();

        // Match production renderer state so the build path is representative.
        MarkdownRenderer.Theme = MarkdownThemes.GitHubDark;
        MarkdownRenderer.BaseFontSize = 16.0;
        MarkdownRenderer.WrapCode = true;
    }

    [Benchmark(Baseline = true)]
    public int ParseOnly()
    {
        var document = MarkdownPipeline.Parse(_document);
        return document.Count;
    }

    [Benchmark]
    public int ParseAndRender()
    {
        var document = MarkdownPipeline.Parse(_document);
        var blocks = 0;
        foreach (var block in MarkdownRenderer.Render(document))
        {
            _ = block.Control;
            blocks++;
        }
        return blocks;
    }
}

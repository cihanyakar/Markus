using System.Text;
using BenchmarkDotNet.Attributes;

namespace Markus.Benchmarks;

// Compares the original pipe-table cell splitter (StringBuilder accumulation per
// character) against the current span-slicing splitter used by
// MarkdownTableFormatter. The row used here has no escaped pipes, so both paths
// produce identical cells and the comparison is purely algorithmic.
[MemoryDiagnoser]
public class TableSplitBenchmarks
{
    private string _row = string.Empty;

    [Params(8, 32)]
    public int Columns { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var builder = new StringBuilder();
        builder.Append('|');
        for (var c = 0; c < Columns; c++)
        {
            builder.Append(" cell ").Append(c).Append(" |");
        }
        _row = builder.ToString();
    }

    [Benchmark(Baseline = true)]
    public int Old() => SplitCellsOld(_row).Count;

    [Benchmark]
    public int New() => SplitCellsNew(_row).Count;

    // Original implementation: accumulate each character into a StringBuilder,
    // flushing a cell at every unescaped pipe.
    private static List<string> SplitCellsOld(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }
        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        var current = new StringBuilder();
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == '|' && i > 0 && trimmed[i - 1] == '\\')
            {
                current[^1] = '\\';
                current.Append('|');
            }
            else if (trimmed[i] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(trimmed[i]);
            }
        }
        cells.Add(current.ToString().Trim());
        return cells;
    }

    // Current implementation: scan a span for unescaped pipes and slice cells
    // out directly, allocating only the final per-cell strings.
    private static List<string> SplitCellsNew(string line)
    {
        var span = line.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '|')
        {
            span = span[1..];
        }
        if (span.Length > 0 && span[^1] == '|')
        {
            span = span[..^1];
        }

        var cells = new List<string>();
        var start = 0;
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == '|' && (i == 0 || span[i - 1] != '\\'))
            {
                cells.Add(span[start..i].Trim().ToString());
                start = i + 1;
            }
        }
        cells.Add(span[start..].Trim().ToString());
        return cells;
    }
}

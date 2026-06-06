using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Markus.Benchmarks;

// Compares the original char-by-char word counter against a SearchValues based
// variant that was prototyped for MainWindowViewModel.WordCount.
//
// Result: the SearchValues variant measured about 10x SLOWER on real prose, so
// it was NOT adopted and production keeps the char loop (the "Old" method here).
// The whitespace set spans 25 code points up to U+3000, so SearchValues cannot
// use its ASCII/Latin1 bitmap fast path and falls back to a probabilistic map;
// calling IndexOfAnyExcept / IndexOfAny twice per word over that map costs far
// more than a single linear char.IsWhiteSpace pass. This benchmark is kept as
// the evidence for that decision.
[MemoryDiagnoser]
public class WordCountBenchmarks
{
    // Match the Unicode whitespace set used by the production implementation.
    private static readonly SearchValues<char> WhitespaceChars = SearchValues.Create(
        "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000"
    );

    private string _document = string.Empty;

    [Params(2_000, 50_000)]
    public int Words { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // A realistic prose document with mixed word lengths and line breaks,
        // so the counter walks many short whitespace gaps like real Markdown.
        const string sample =
            "The quick brown fox jumps over the lazy dog while the morning sun rises slowly over the quiet hills.\n";
        var wordsPerLine = sample.Split(' ').Length;
        var lines = (Words / wordsPerLine) + 1;
        var builder = new StringBuilder(sample.Length * lines);
        for (var i = 0; i < lines; i++)
        {
            builder.Append(sample);
        }
        _document = builder.ToString();
    }

    [Benchmark(Baseline = true)]
    public int Old() => CountWordsOld(_document);

    [Benchmark]
    public int New() => CountWordsNew(_document);

    // Original implementation: iterate every char, toggling an in-word flag.
    private static int CountWordsOld(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                inWord = false;
                continue;
            }
            if (inWord)
            {
                continue;
            }
            inWord = true;
            count++;
        }
        return count;
    }

    // Current implementation: skip whitespace and find the next word boundary
    // with vectorized span searches.
    private static int CountWordsNew(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        var span = text.AsSpan();
        var count = 0;
        while (!span.IsEmpty)
        {
            var wordStart = span.IndexOfAnyExcept(WhitespaceChars);
            if (wordStart < 0)
            {
                break;
            }
            count++;
            span = span[wordStart..];
            var wordEnd = span.IndexOfAny(WhitespaceChars);
            if (wordEnd < 0)
            {
                break;
            }
            span = span[wordEnd..];
        }
        return count;
    }
}

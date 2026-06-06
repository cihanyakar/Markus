using Markdig;
using Markdig.Syntax;

namespace Markus.Services;

internal static class MarkdownPipeline
{
    private static readonly Markdig.MarkdownPipeline Pipeline = Build();

    public static MarkdownDocument Parse(string source)
    {
        return Markdown.Parse(source ?? string.Empty, Pipeline);
    }

    private static Markdig.MarkdownPipeline Build()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseYamlFrontMatter()
            .UseAutoLinks()
            .UseTaskLists()
            // Smileys off: the ASCII smiley parser turns ordinary punctuation
            // into emoji (e.g. the `:*` inside `**bold:**` becomes a kiss face
            // and breaks the emphasis). Keep `:shortcode:` emoji, drop smileys.
            .UseEmojiAndSmiley(false)
            .UseMathematics()
            .Build();
    }
}

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

// Tests share mutable static state (MarkdownRenderer.Theme/BaseFontSize) and
// lazily-initialized Avalonia presets (e.g. TextDecorations), which race when
// test classes run in parallel. The suite runs in well under a second, so
// keeping it sequential costs nothing.

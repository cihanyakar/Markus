using System.ComponentModel;
using Markus.Models;
using Markus.Services;
using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void DefaultViewMode_FollowsSettings()
    {
        var sut = new MainWindowViewModel();

        sut.CurrentViewMode.ShouldBe(sut.Settings.DefaultViewMode);
    }

    // --- ViewMode mutual-exclusivity (parameterized) ---

    [Theory]
    [InlineData(0, true, false, false, false)]
    [InlineData(1, false, true, false, false)]
    [InlineData(2, false, false, true, false)]
    [InlineData(3, false, false, false, true)]
    [InlineData(4, false, false, false, false)]
    public void ViewMode_SetsExactlyOneFlag(
        int modeInt,
        bool sourceOnly,
        bool previewOnly,
        bool splitVertical,
        bool splitHorizontal
    )
    {
        var mode = (ViewMode)modeInt;
        var sut = new MainWindowViewModel { CurrentViewMode = mode };

        sut.IsSourceOnly.ShouldBe(sourceOnly);
        sut.IsPreviewOnly.ShouldBe(previewOnly);
        sut.IsSplitVerticalActive.ShouldBe(splitVertical);
        sut.IsSplitHorizontalActive.ShouldBe(splitHorizontal);
    }

    // --- WordCount: counting words, not characters ---

    [Fact]
    public void WordCount_CountsWordsNotCharacters()
    {
        var sut = new MainWindowViewModel { SourceText = "Hello world foo bar" };

        sut.WordCount.ShouldBe(4);
    }

    [Fact]
    public void WordCount_EmptySource_ReturnsZero()
    {
        var sut = new MainWindowViewModel { SourceText = string.Empty };

        sut.WordCount.ShouldBe(0);
    }

    [Fact]
    public void WordCount_WhitespaceOnly_ReturnsZero()
    {
        var sut = new MainWindowViewModel { SourceText = "   \t  \n  " };

        sut.WordCount.ShouldBe(0);
    }

    [Fact]
    public void WordCount_MultipleSpaces_NotDoubled()
    {
        var sut = new MainWindowViewModel { SourceText = "one   two    three" };

        sut.WordCount.ShouldBe(3);
    }

    [Fact]
    public void WordCount_Newlines_SplitWords()
    {
        var sut = new MainWindowViewModel { SourceText = "line1\nline2\nline3" };

        sut.WordCount.ShouldBe(3);
    }

    [Fact]
    public void WordCount_PunctuationAttachedToWord_CountsAsOneWord()
    {
        // Punctuation glued to text is one "word" (non-whitespace run).
        var sut = new MainWindowViewModel { SourceText = "hello, world! foo." };

        sut.WordCount.ShouldBe(3);
    }

    [Fact]
    public void WordCount_MarkdownSyntax_CountsTokensAsWords()
    {
        // Markdown heading marker (#) and bold markers (**) are non-whitespace
        // runs, so # counts as a word and **bold** counts as one word.
        var sut = new MainWindowViewModel { SourceText = "# heading\n**bold** text" };

        sut.WordCount.ShouldBe(4);
    }

    [Fact]
    public void WordCount_SingleWord_ReturnsOne()
    {
        var sut = new MainWindowViewModel { SourceText = "hello" };

        sut.WordCount.ShouldBe(1);
    }

    // --- CharCount ---

    [Fact]
    public void CharCount_MatchesSourceLength()
    {
        var sut = new MainWindowViewModel { SourceText = "abc" };

        sut.CharCount.ShouldBe(3);
    }

    [Fact]
    public void CharCount_EmptySource_ReturnsZero()
    {
        var sut = new MainWindowViewModel { SourceText = string.Empty };

        sut.CharCount.ShouldBe(0);
    }

    // --- ReadingMinutes ---

    [Fact]
    public void ReadingMinutes_MinimumIsOne()
    {
        var sut = new MainWindowViewModel { SourceText = "word" };

        sut.ReadingMinutes.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ReadingMinutes_250Words_IsOneMinute()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 250));
        var sut = new MainWindowViewModel { SourceText = text };

        sut.ReadingMinutes.ShouldBe(1);
    }

    [Fact]
    public void ReadingMinutes_251Words_IsTwoMinutes()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", 251));
        var sut = new MainWindowViewModel { SourceText = text };

        sut.ReadingMinutes.ShouldBe(2);
    }

    // --- DocumentStats format ---

    [Fact]
    public void DocumentStats_ContainsWordCountCharCountAndReadingTime()
    {
        var sut = new MainWindowViewModel { SourceText = "one two three" };

        sut.DocumentStats.ShouldContain("3 words");
        sut.DocumentStats.ShouldContain("13 chars");
        sut.DocumentStats.ShouldContain("~1 min");
    }

    [Fact]
    public void DocumentStats_IncludesTildeBeforeMinutes()
    {
        // Catches a mutation that removes the "~" prefix from reading time.
        var sut = new MainWindowViewModel { SourceText = "word" };

        sut.DocumentStats.ShouldContain("~");
    }

    [Fact]
    public void DocumentStats_IncludesLastModifiedWhenSet()
    {
        var sut = new MainWindowViewModel { SourceText = "word", LastModifiedText = "modified 12:34" };

        sut.DocumentStats.ShouldContain("modified 12:34");
        sut.DocumentStats.ShouldContain("~");
        sut.DocumentStats.ShouldContain("1 words");
    }

    [Fact]
    public void DocumentStats_OmitsLastModifiedWhenEmpty()
    {
        var sut = new MainWindowViewModel { SourceText = "word", LastModifiedText = string.Empty };

        sut.DocumentStats.ShouldNotContain("modified");
    }

    // --- NewScratch ---

    [Fact]
    public void NewScratch_ClearsSourceText()
    {
        var sut = new MainWindowViewModel { SourceText = "some content" };

        sut.NewScratchCommand.Execute(null);

        sut.SourceText.ShouldBeEmpty();
    }

    [Fact]
    public void NewScratch_SetsScratchBufferFlag()
    {
        var sut = new MainWindowViewModel();

        sut.NewScratchCommand.Execute(null);

        sut.IsScratchBuffer.ShouldBeTrue();
    }

    [Fact]
    public void NewScratch_SetsDocumentTitleToUntitled()
    {
        var sut = new MainWindowViewModel();

        sut.NewScratchCommand.Execute(null);

        sut.DocumentTitle.ShouldBe("Untitled");
    }

    [Fact]
    public void NewScratch_ClearsLastModifiedText()
    {
        var sut = new MainWindowViewModel { LastModifiedText = "modified 2024-01-01" };

        sut.NewScratchCommand.Execute(null);

        sut.LastModifiedText.ShouldBeEmpty();
    }

    [Fact]
    public void NewScratch_MakesWelcomeInvisible()
    {
        // After NewScratch, IsScratchBuffer is true so IsWelcomeVisible should be false
        // even though CurrentFilePath is null.
        var sut = new MainWindowViewModel();

        sut.NewScratchCommand.Execute(null);

        sut.IsWelcomeVisible.ShouldBeFalse();
    }

    // --- IsWelcomeVisible ---

    [Fact]
    public void IsWelcomeVisible_TrueWhenNoFileAndNotScratch()
    {
        var sut = new MainWindowViewModel();

        sut.IsWelcomeVisible.ShouldBeTrue();
    }

    [Fact]
    public void IsWelcomeVisible_FalseWhenFilePathSet()
    {
        var sut = new MainWindowViewModel { CurrentFilePath = "/some/file.md" };

        sut.IsWelcomeVisible.ShouldBeFalse();
    }

    // --- Outline toggle ---

    [Fact]
    public void ToggleOutline_FlipsVisibility()
    {
        var sut = new MainWindowViewModel();
        var before = sut.IsOutlineVisible;

        sut.ToggleOutlineCommand.Execute(null);

        sut.IsOutlineVisible.ShouldBe(!before);
    }

    [Fact]
    public void ToggleOutline_TwiceReturnsToOriginal()
    {
        var sut = new MainWindowViewModel();
        var original = sut.IsOutlineVisible;

        sut.ToggleOutlineCommand.Execute(null);
        sut.ToggleOutlineCommand.Execute(null);

        sut.IsOutlineVisible.ShouldBe(original);
    }

    // --- Outline placement ---

    [Fact]
    public void IsOutlineLeftVisible_TrueOnlyWhenVisibleAndLeft()
    {
        var sut = new MainWindowViewModel { IsOutlineVisible = true, OutlinePlacement = OutlinePlacement.Left };

        sut.IsOutlineLeftVisible.ShouldBeTrue();
        sut.IsOutlineRightVisible.ShouldBeFalse();
    }

    [Fact]
    public void IsOutlineRightVisible_TrueOnlyWhenVisibleAndRight()
    {
        var sut = new MainWindowViewModel { IsOutlineVisible = true, OutlinePlacement = OutlinePlacement.Right };

        sut.IsOutlineRightVisible.ShouldBeTrue();
        sut.IsOutlineLeftVisible.ShouldBeFalse();
    }

    [Fact]
    public void OutlinePlacement_NotVisibleWhenOutlineHidden()
    {
        var sut = new MainWindowViewModel { IsOutlineVisible = false, OutlinePlacement = OutlinePlacement.Left };

        sut.IsOutlineLeftVisible.ShouldBeFalse();
        sut.IsOutlineRightVisible.ShouldBeFalse();
    }

    // --- HasSelection ---

    [Fact]
    public void HasSelection_FalseWhenSelectionStatsEmpty()
    {
        var sut = new MainWindowViewModel { SelectionStats = string.Empty };

        sut.HasSelection.ShouldBeFalse();
    }

    [Fact]
    public void HasSelection_TrueWhenSelectionStatsNonEmpty()
    {
        var sut = new MainWindowViewModel { SelectionStats = "3 chars selected" };

        sut.HasSelection.ShouldBeTrue();
    }

    // --- PreviewInvalidated fires for different renderer-affecting settings ---

    [Fact]
    public void PreviewInvalidated_FiresWhenFontSizeChanges()
    {
        var settingsService = CreateTempSettings();
        var sut = new MainWindowViewModel(settingsService);
        var fired = false;
        sut.PreviewInvalidated += (_, _) => fired = true;

        var settings = sut.Settings.Clone();
        settings.FontSize = 24.0;
        settingsService.Save(settings);

        fired.ShouldBeTrue();
    }

    [Fact]
    public void PreviewInvalidated_FiresWhenThemeChanges()
    {
        var settingsService = CreateTempSettings();
        var sut = new MainWindowViewModel(settingsService);
        var fired = false;
        sut.PreviewInvalidated += (_, _) => fired = true;

        var settings = sut.Settings.Clone();
        settings.Theme = "GitHubLight";
        settingsService.Save(settings);

        fired.ShouldBeTrue();
    }

    [Fact]
    public void PreviewInvalidated_DoesNotFireWhenUnrelatedSettingChanges()
    {
        var settingsService = CreateTempSettings();
        var sut = new MainWindowViewModel(settingsService);
        var fired = false;
        sut.PreviewInvalidated += (_, _) => fired = true;

        // Changing only the language should not invalidate the preview.
        var settings = sut.Settings.Clone();
        settings.Language = "de";
        settingsService.Save(settings);

        fired.ShouldBeFalse();
    }

    // --- PropertyChanged propagation from SourceText ---

    [Fact]
    public void SettingSourceText_RaisesPropertyChangedForWordCount()
    {
        var sut = new MainWindowViewModel();
        var raised = new List<string>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.SourceText = "hello world";

        raised.ShouldContain(nameof(sut.WordCount));
    }

    [Fact]
    public void SettingSourceText_RaisesPropertyChangedForCharCount()
    {
        var sut = new MainWindowViewModel();
        var raised = new List<string>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.SourceText = "hello world";

        raised.ShouldContain(nameof(sut.CharCount));
    }

    [Fact]
    public void SettingSourceText_RaisesPropertyChangedForDocumentStats()
    {
        var sut = new MainWindowViewModel();
        var raised = new List<string>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.SourceText = "hello world";

        raised.ShouldContain(nameof(sut.DocumentStats));
    }

    [Fact]
    public void SettingSourceText_RaisesPropertyChangedForReadingMinutes()
    {
        var sut = new MainWindowViewModel();
        var raised = new List<string>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.SourceText = "hello world";

        raised.ShouldContain(nameof(sut.ReadingMinutes));
    }

    // --- CurrentViewMode PropertyChanged propagation ---

    [Fact]
    public void SettingCurrentViewMode_RaisesPropertyChangedForAllFlags()
    {
        var sut = new MainWindowViewModel();
        var raised = new List<string>();
        ((INotifyPropertyChanged)sut).PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        sut.CurrentViewMode = ViewMode.SplitVertical;

        raised.ShouldContain(nameof(sut.IsSourceOnly));
        raised.ShouldContain(nameof(sut.IsPreviewOnly));
        raised.ShouldContain(nameof(sut.IsSplitVerticalActive));
        raised.ShouldContain(nameof(sut.IsSplitHorizontalActive));
    }

    private static SettingsService CreateTempSettings()
    {
        return new SettingsService(Path.Combine(Path.GetTempPath(), $"markus_test_{Guid.NewGuid():N}"));
    }
}

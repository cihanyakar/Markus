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

    [Fact]
    public void ToggleOutline_PersistsShowOutlineFlag()
    {
        var dir = Directory.CreateTempSubdirectory("markus-test").FullName;
        var service = new SettingsService(dir);
        var sut = new MainWindowViewModel(service);
        var initial = sut.IsOutlineVisible;

        sut.ToggleOutlineCommand.Execute(null);

        sut.IsOutlineVisible.ShouldBe(!initial);
        service.Load().ShowOutline.ShouldBe(!initial);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ShowOutlineSettingChange_UpdatesLiveVisibility()
    {
        var dir = Directory.CreateTempSubdirectory("markus-test").FullName;
        var service = new SettingsService(dir);
        var sut = new MainWindowViewModel(service);
        var initial = sut.IsOutlineVisible;

        var changed = service.Load();
        changed.ShowOutline = !initial;
        service.Save(changed);

        sut.IsOutlineVisible.ShouldBe(!initial);
        Directory.Delete(dir, recursive: true);
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
    public async Task NewScratch_ClearsSourceText()
    {
        var sut = new MainWindowViewModel { SourceText = "some content" };

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.SourceText.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewScratch_SetsScratchBufferFlag()
    {
        var sut = new MainWindowViewModel();

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.IsScratchBuffer.ShouldBeTrue();
    }

    [Fact]
    public async Task NewScratch_SetsDocumentTitleToUntitled()
    {
        var sut = new MainWindowViewModel();

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.DocumentTitle.ShouldBe("Untitled");
    }

    [Fact]
    public async Task NewScratch_ClearsLastModifiedText()
    {
        var sut = new MainWindowViewModel { LastModifiedText = "modified 2024-01-01" };

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.LastModifiedText.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewScratch_MakesWelcomeInvisible()
    {
        // After NewScratch, IsScratchBuffer is true so IsWelcomeVisible should be false
        // even though CurrentFilePath is null.
        var sut = new MainWindowViewModel();

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.IsWelcomeVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveToFileAsync_WritesSourceTextToProvidedPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-save-{Guid.NewGuid():N}.md");
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "# Saved\n\nHello" };

        try
        {
            await sut.SaveToFileAsync(path, TestContext.Current.CancellationToken);

            var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            saved.ShouldBe("# Saved\n\nHello");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveToFileAsync_UpdatesDocumentIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-save-{Guid.NewGuid():N}.md");
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "content" };
        await sut.NewScratchCommand.ExecuteAsync(null);

        try
        {
            await sut.SaveToFileAsync(path, TestContext.Current.CancellationToken);

            sut.CurrentFilePath.ShouldBe(path);
            sut.DocumentTitle.ShouldBe(Path.GetFileName(path));
            sut.IsScratchBuffer.ShouldBeFalse();
            sut.StatusText.ShouldContain("saved");
            sut.LastModifiedText.ShouldNotBeEmpty();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveCommand_WithExistingFileWritesCurrentSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-save-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken);
        var sut = new MainWindowViewModel(CreateTempSettings()) { CurrentFilePath = path, SourceText = "new" };

        try
        {
            await sut.SaveCommand.ExecuteAsync(null);

            var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            saved.ShouldBe("new");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveCommand_WithoutFile_PromptsForSavePathAndWrites()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-saveas-{Guid.NewGuid():N}.md");
        var interaction = new FakeDocumentInteraction { SavePathToReturn = path };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "draft", Interaction = interaction };

        try
        {
            await sut.SaveCommand.ExecuteAsync(null);

            interaction.PickSaveCalls.ShouldBe(1);
            var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            saved.ShouldBe("draft");
            sut.CurrentFilePath.ShouldBe(path);
            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SaveCommand_WithoutFile_CancelledPicker_DoesNotSave()
    {
        var interaction = new FakeDocumentInteraction { SavePathToReturn = null };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "draft", Interaction = interaction };

        await sut.SaveCommand.ExecuteAsync(null);

        interaction.PickSaveCalls.ShouldBe(1);
        sut.CurrentFilePath.ShouldBeNull();
        sut.IsDirty.ShouldBeTrue();
    }

    // --- IsDirty ---

    [Fact]
    public void EditingSourceText_MarksDirty()
    {
        var sut = new MainWindowViewModel(CreateTempSettings());
        sut.IsDirty.ShouldBeFalse();

        sut.SourceText = "user typed this";

        sut.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadFile_LeavesDocumentClean()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-dirty-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "from disk", TestContext.Current.CancellationToken);
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed" };

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);

            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Save_ClearsDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-dirty-{Guid.NewGuid():N}.md");
        var sut = new MainWindowViewModel(CreateTempSettings()) { CurrentFilePath = path, SourceText = "edited" };
        sut.IsDirty.ShouldBeTrue();

        try
        {
            await sut.SaveToFileAsync(path, TestContext.Current.CancellationToken);

            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task NewScratch_StartsClean()
    {
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed" };

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void DisplayTitle_PrefixesBullet_WhenDirty()
    {
        var sut = new MainWindowViewModel(CreateTempSettings()) { DocumentTitle = "readme.md", SourceText = "edit" };

        sut.DisplayTitle.ShouldBe("• readme.md");
    }

    [Fact]
    public void DisplayTitle_NoBullet_WhenClean()
    {
        var sut = new MainWindowViewModel(CreateTempSettings()) { DocumentTitle = "readme.md" };

        sut.DisplayTitle.ShouldBe("readme.md");
    }

    // --- Unsaved-changes guards ---

    [Fact]
    public async Task NewScratch_DirtyAndCancel_KeepsCurrentDocument()
    {
        var interaction = new FakeDocumentInteraction { DiscardChoice = UnsavedChangesChoice.Cancel };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed", Interaction = interaction };

        await sut.NewScratchCommand.ExecuteAsync(null);

        interaction.DiscardCalls.ShouldBe(1);
        sut.SourceText.ShouldBe("typed");
        sut.IsDirty.ShouldBeTrue();
        sut.IsScratchBuffer.ShouldBeFalse();
    }

    [Fact]
    public async Task NewScratch_DirtyAndDiscard_ClearsBuffer()
    {
        var interaction = new FakeDocumentInteraction { DiscardChoice = UnsavedChangesChoice.Discard };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed", Interaction = interaction };

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.SourceText.ShouldBeEmpty();
        sut.IsScratchBuffer.ShouldBeTrue();
        sut.IsDirty.ShouldBeFalse();
        sut.CurrentFilePath.ShouldBeNull();
    }

    [Fact]
    public async Task NewScratch_DirtyAndSave_PersistsThenClears()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-guard-{Guid.NewGuid():N}.md");
        var interaction = new FakeDocumentInteraction
        {
            DiscardChoice = UnsavedChangesChoice.Save,
            SavePathToReturn = path,
        };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "old work", Interaction = interaction };

        try
        {
            await sut.NewScratchCommand.ExecuteAsync(null);

            var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            saved.ShouldBe("old work");
            sut.IsScratchBuffer.ShouldBeTrue();
            sut.SourceText.ShouldBeEmpty();
            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Reload_DirtyAndDeclined_KeepsEdits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-guard-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.SourceText = "edited";
            sut.Interaction = new FakeDocumentInteraction { ReloadConfirm = false };

            await sut.ReloadCommand.ExecuteAsync(null);

            sut.SourceText.ShouldBe("edited");
            sut.IsDirty.ShouldBeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Reload_DirtyAndConfirmed_LoadsDiskVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-guard-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.SourceText = "edited";
            sut.Interaction = new FakeDocumentInteraction { ReloadConfirm = true };

            await sut.ReloadCommand.ExecuteAsync(null);

            sut.SourceText.ShouldBe("orig");
            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenRecent_DirtyAndCancel_DoesNotLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-guard-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "other file", TestContext.Current.CancellationToken);
        var interaction = new FakeDocumentInteraction { DiscardChoice = UnsavedChangesChoice.Cancel };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed", Interaction = interaction };

        try
        {
            await sut.OpenRecentCommand.ExecuteAsync(path);

            sut.SourceText.ShouldBe("typed");
            sut.CurrentFilePath.ShouldBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- External change on disk ---

    [Fact]
    public async Task ExternalChange_DiskMatchesLastSync_DoesNotReloadOrPrompt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-ext-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var interaction = new FakeDocumentInteraction();
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.SourceText = "edited";
            sut.Interaction = interaction;

            // Disk is still "orig" (e.g. our own earlier save); the user typed after.
            await sut.HandleExternalChangeAsync(path, WatcherChangeTypes.Changed);

            sut.SourceText.ShouldBe("edited");
            sut.IsDirty.ShouldBeTrue();
            interaction.ReloadCalls.ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExternalChange_GenuineChangeWhileClean_ReloadsSilently()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-ext-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var interaction = new FakeDocumentInteraction();
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.Interaction = interaction;
            await File.WriteAllTextAsync(path, "external edit", TestContext.Current.CancellationToken);

            await sut.HandleExternalChangeAsync(path, WatcherChangeTypes.Changed);

            sut.SourceText.ShouldBe("external edit");
            sut.IsDirty.ShouldBeFalse();
            interaction.ReloadCalls.ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExternalChange_GenuineChangeWhileDirty_Declined_KeepsEdits()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-ext-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var interaction = new FakeDocumentInteraction { ReloadConfirm = false };
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.SourceText = "my edits";
            sut.Interaction = interaction;
            await File.WriteAllTextAsync(path, "external edit", TestContext.Current.CancellationToken);

            await sut.HandleExternalChangeAsync(path, WatcherChangeTypes.Changed);

            sut.SourceText.ShouldBe("my edits");
            interaction.ReloadCalls.ShouldBe(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExternalChange_GenuineChangeWhileDirty_Confirmed_Reloads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-ext-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var interaction = new FakeDocumentInteraction { ReloadConfirm = true };
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);
            sut.SourceText = "my edits";
            sut.Interaction = interaction;
            await File.WriteAllTextAsync(path, "external edit", TestContext.Current.CancellationToken);

            await sut.HandleExternalChangeAsync(path, WatcherChangeTypes.Changed);

            sut.SourceText.ShouldBe("external edit");
            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExternalChange_Deleted_SetsStatusWithoutClearing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"markus-ext-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, "orig", TestContext.Current.CancellationToken);
        var sut = new MainWindowViewModel(CreateTempSettings());

        try
        {
            await sut.LoadFileAsync(path, TestContext.Current.CancellationToken);

            await sut.HandleExternalChangeAsync(path, WatcherChangeTypes.Deleted);

            sut.StatusText.ShouldContain("deleted");
            sut.SourceText.ShouldBe("orig");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenFile_DirtyAndCancel_DoesNotRequestOpen()
    {
        var interaction = new FakeDocumentInteraction { DiscardChoice = UnsavedChangesChoice.Cancel };
        var sut = new MainWindowViewModel(CreateTempSettings()) { SourceText = "typed", Interaction = interaction };
        var raised = false;
        sut.OpenRequested += (_, _) => raised = true;

        await sut.OpenFileCommand.ExecuteAsync(null);

        raised.ShouldBeFalse();
        interaction.DiscardCalls.ShouldBe(1);
    }

    [Fact]
    public async Task OpenFile_Clean_RequestsOpen()
    {
        var sut = new MainWindowViewModel(CreateTempSettings());
        var raised = false;
        sut.OpenRequested += (_, _) => raised = true;

        await sut.OpenFileCommand.ExecuteAsync(null);

        raised.ShouldBeTrue();
    }

    // --- Save availability ---

    [Fact]
    public void SaveCommand_DisabledOnWelcome()
    {
        var sut = new MainWindowViewModel(CreateTempSettings());

        sut.IsWelcomeVisible.ShouldBeTrue();
        sut.SaveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task SaveCommand_EnabledAfterNewScratch()
    {
        var sut = new MainWindowViewModel(CreateTempSettings());

        await sut.NewScratchCommand.ExecuteAsync(null);

        sut.SaveCommand.CanExecute(null).ShouldBeTrue();
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

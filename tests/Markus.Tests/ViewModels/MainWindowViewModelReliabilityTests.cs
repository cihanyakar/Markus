using Markus.Services;
using Markus.ViewModels;

namespace Markus.Tests.ViewModels;

public sealed class MainWindowViewModelReliabilityTests
{
    // --- Large-file threshold decision ---

    [Theory]
    [InlineData(0L, false)]
    [InlineData(1024L, false)]
    [InlineData((8L * 1024 * 1024) - 1, false)]
    [InlineData(8L * 1024 * 1024, true)]
    [InlineData((8L * 1024 * 1024) + 1, true)]
    [InlineData(64L * 1024 * 1024, true)]
    public void IsFileTooLargeToOpen_UsesThreshold(long lengthBytes, bool expected)
    {
        MainWindowViewModel.IsFileTooLargeToOpen(lengthBytes).ShouldBe(expected);
    }

    // --- Stale-watcher path matching decision ---

    [Fact]
    public void PathMatchesWatched_NullOrEmpty_DoesNotMatch()
    {
        MainWindowViewModel.PathMatchesWatched(null, "/tmp/a.md").ShouldBeFalse();
        MainWindowViewModel.PathMatchesWatched(string.Empty, "/tmp/a.md").ShouldBeFalse();
        MainWindowViewModel.PathMatchesWatched("/tmp/a.md", string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void PathMatchesWatched_SamePath_Matches()
    {
        MainWindowViewModel.PathMatchesWatched("/tmp/a.md", "/tmp/a.md").ShouldBeTrue();
    }

    [Fact]
    public void PathMatchesWatched_DifferentFile_DoesNotMatch()
    {
        MainWindowViewModel.PathMatchesWatched("/tmp/a.md", "/tmp/b.md").ShouldBeFalse();
    }

    [Fact]
    public void PathMatchesWatched_NonCanonicalButEquivalent_Matches()
    {
        // A relative-segment path canonicalizes to the same absolute path.
        MainWindowViewModel.PathMatchesWatched("/tmp/sub/../a.md", "/tmp/a.md").ShouldBeTrue();
    }

    // --- Stale-watcher early-exit behavior ---

    [Fact]
    public async Task HandleExternalChange_PathMismatch_DoesNotReplaceBuffer()
    {
        var (dir, sut) = NewViewModel();
        try
        {
            var currentFile = Path.Combine(dir, "current.md");
            var otherFile = Path.Combine(dir, "other.md");
            var ct = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(currentFile, "current content", ct);
            await File.WriteAllTextAsync(otherFile, "OTHER content", ct);

            await sut.LoadFileAsync(currentFile, ct);
            sut.SourceText.ShouldBe("current content");

            // A stale event for a different file must be ignored, not loaded over
            // the open document.
            await sut.HandleExternalChangeAsync(otherFile, WatcherChangeTypes.Changed);

            sut.SourceText.ShouldBe("current content");
            sut.CurrentFilePath.ShouldBe(currentFile);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task HandleExternalChange_DeleteForOtherFile_DoesNotFlagDirty()
    {
        var (dir, sut) = NewViewModel();
        try
        {
            var currentFile = Path.Combine(dir, "current.md");
            var otherFile = Path.Combine(dir, "other.md");
            var ct = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(currentFile, "current content", ct);

            await sut.LoadFileAsync(currentFile, ct);
            sut.IsDirty.ShouldBeFalse();

            // A delete event for a file we are not showing must not mark the open
            // (clean) document dirty.
            await sut.HandleExternalChangeAsync(otherFile, WatcherChangeTypes.Deleted);

            sut.IsDirty.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task HandleExternalChange_MatchingChange_ReloadsBuffer()
    {
        var (dir, sut) = NewViewModel();
        try
        {
            var file = Path.Combine(dir, "doc.md");
            var ct = TestContext.Current.CancellationToken;
            await File.WriteAllTextAsync(file, "original", ct);
            await sut.LoadFileAsync(file, ct);

            await File.WriteAllTextAsync(file, "edited on disk", ct);
            await sut.HandleExternalChangeAsync(file, WatcherChangeTypes.Changed);

            // The matching-path event still reconciles as before.
            sut.SourceText.ShouldBe("edited on disk");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // --- Crash recovery dump ---

    [Fact]
    public void TryDumpRecovery_CleanBuffer_WritesNothing()
    {
        var (dir, sut) = NewViewModel();
        try
        {
            var recoveryDir = Path.Combine(dir, "recovery");

            sut.TryDumpRecovery(recoveryDir).ShouldBeFalse();
            Directory.Exists(recoveryDir).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryDumpRecovery_DirtyBuffer_WritesRecoveryFile()
    {
        var (dir, sut) = NewViewModel();
        try
        {
            var recoveryDir = Path.Combine(dir, "recovery");
            sut.SourceText = "unsaved work"; // OnSourceTextChanged flags dirty
            sut.IsDirty.ShouldBeTrue();

            sut.TryDumpRecovery(recoveryDir).ShouldBeTrue();

            var files = Directory.GetFiles(recoveryDir, "*.recovered.md");
            files.Length.ShouldBe(1);
            File.ReadAllText(files[0]).ShouldBe("unsaved work");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static (string Dir, MainWindowViewModel Vm) NewViewModel()
    {
        var dir = Directory.CreateTempSubdirectory("markus-reliability").FullName;
        var service = new SettingsService(dir);
        return (dir, new MainWindowViewModel(service));
    }
}

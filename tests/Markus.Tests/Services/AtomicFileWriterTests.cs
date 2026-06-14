using Markus.Services;

namespace Markus.Tests.Services;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string _tempDir;

    public AtomicFileWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"markus-atomic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void WriteAllText_FreshFile_WritesContent()
    {
        var path = Path.Combine(_tempDir, "fresh.json");

        AtomicFileWriter.WriteAllText(path, "{\"hello\":\"world\"}");

        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldBe("{\"hello\":\"world\"}");
    }

    [Fact]
    public void WriteAllText_ExistingFile_OverwritesContent()
    {
        var path = Path.Combine(_tempDir, "existing.json");
        File.WriteAllText(path, "old content");

        AtomicFileWriter.WriteAllText(path, "new content");

        File.ReadAllText(path).ShouldBe("new content");
    }

    [Fact]
    public void WriteAllText_DoesNotLeaveSidecarTempFile()
    {
        var path = Path.Combine(_tempDir, "clean.json");

        AtomicFileWriter.WriteAllText(path, "payload");

        // No `clean.json.tmp` or similar sidecar should survive a successful write.
        var residue = Directory
            .GetFiles(_tempDir)
            .Where(p => !string.Equals(p, path, StringComparison.Ordinal))
            .ToArray();
        residue.ShouldBeEmpty();
    }

    [Fact]
    public void WriteAllText_PreservesPreviousContent_WhenRenameCannotComplete()
    {
        // Atomic semantics: a failure during the rename step must leave the
        // original file intact. Force File.Move to fail by pre-occupying the
        // destination with a subdirectory of the same name (rename onto a
        // non-empty directory is rejected on every platform we ship for).
        var path = Path.Combine(_tempDir, "preserve.json");
        Directory.CreateDirectory(path);
        var realFile = Path.Combine(path, "marker.txt");
        File.WriteAllText(realFile, "directory contents that must survive");

        Should.Throw<Exception>(() => AtomicFileWriter.WriteAllText(path, "new payload"));

        // The destination directory still holds its prior content (the
        // atomic guarantee that a failed write does not corrupt what was
        // there before).
        File.ReadAllText(realFile).ShouldBe("directory contents that must survive");
    }

    [Fact]
    public void WriteAllText_CreatesParentDirectoryIfMissing()
    {
        var path = Path.Combine(_tempDir, "auto", "nested", "deep.json");

        AtomicFileWriter.WriteAllText(path, "content");

        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldBe("content");
    }

    [Fact]
    public void WriteAllText_ConcurrentWriters_BothSucceed_LastWins()
    {
        // Two parallel writers to the same path must not deadlock and must
        // not both throw because of a shared sidecar — the original fixed-
        // sidecar + FileShare.None design made the second writer fail with
        // IOException. With per-call randomized sidecar names, both writes
        // complete and the last rename wins (matching File.WriteAllText
        // semantics, but without the corruption window).
        var path = Path.Combine(_tempDir, "concurrent.json");
        var contents = Enumerable.Range(0, 20).Select(i => $"payload-{i}").ToArray();
        var threw = new List<Exception>();
        Parallel.For(
            0,
            contents.Length,
            i =>
            {
                try
                {
                    AtomicFileWriter.WriteAllText(path, contents[i]);
                }
                catch (Exception ex)
                {
                    lock (threw)
                    {
                        threw.Add(ex);
                    }
                }
            }
        );
        threw.ShouldBeEmpty();
        // Last writer wins, but the file must hold exactly one of the
        // contents (no partial / interleaved bytes).
        var final = File.ReadAllText(path);
        contents.ShouldContain(final);
        // No orphan .tmp sidecars survive a successful run.
        Directory
            .GetFiles(_tempDir)
            .Where(p => p.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty();
    }

    [Fact]
    public void WriteAllText_OrphanedTempIsCleanedUpWhenWriteFails()
    {
        // The previous implementation had no try/finally to clean up the
        // sidecar if File.Move (or a later step) failed after a successful
        // FileStream write; an orphan `<path>.<random>.tmp` would linger.
        // The new contract: any failure between sidecar creation and a
        // successful rename leaves NO orphan in the destination directory.
        // We simulate failure by pre-occupying the destination path with a
        // directory of the same name, which forces File.Move to throw.
        var path = Path.Combine(_tempDir, "blocked.json");
        Directory.CreateDirectory(path); // destination is a directory, so File.Move(... overwrite:true) fails

        Should.Throw<Exception>(() => AtomicFileWriter.WriteAllText(path, "payload"));

        Directory.GetFiles(_tempDir).Where(p => p.Contains(".tmp", StringComparison.OrdinalIgnoreCase)).ShouldBeEmpty();
    }

    [Fact]
    public void WriteAllText_FollowsSymlink_PreservingTheLinkItself()
    {
        // The previous File.WriteAllText path followed symlinks (writing
        // through to the target). AtomicFileWriter's File.Move-over-symlink
        // would have REPLACED the link with a regular file, silently
        // breaking sync-folder / dotfiles-symlink workflows. The fix
        // resolves the link before computing the destination path so the
        // link survives and the linked file gets the new content.
        if (OperatingSystem.IsWindows())
        {
            // Symlink creation on Windows requires admin or developer mode;
            // skip the test without failing the suite when neither is set.
            return;
        }
        var realDir = Path.Combine(_tempDir, "real");
        var linkDir = Path.Combine(_tempDir, "linked");
        Directory.CreateDirectory(realDir);
        Directory.CreateDirectory(linkDir);
        var realPath = Path.Combine(realDir, "settings.json");
        var linkPath = Path.Combine(linkDir, "settings.json");
        File.WriteAllText(realPath, "initial");
        File.CreateSymbolicLink(linkPath, realPath);

        AtomicFileWriter.WriteAllText(linkPath, "updated");

        // Link survives and points at the real file.
        File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint).ShouldBeTrue();
        File.ReadAllText(realPath).ShouldBe("updated");
    }
}

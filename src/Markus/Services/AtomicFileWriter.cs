using System.Runtime.InteropServices;

namespace Markus.Services;

/// <summary>
/// Writes a file's full content atomically: writes the new bytes into a
/// per-call randomized sidecar temp file first, flushes them to disk, then
/// renames over the destination. POSIX rename(2) and Windows MoveFileEx
/// with REPLACE_EXISTING are atomic at the filesystem layer, so a crash
/// mid-write leaves either the old content or the new content on disk,
/// never a truncated mix. On POSIX the parent directory is also fsync'd
/// after rename so the directory metadata is durably persisted (without
/// it, a power loss can lose the rename even though the file data is on
/// disk). Concurrent writers do not collide on the sidecar (random name
/// per call); last writer wins at the rename, matching File.WriteAllText
/// semantics without the corruption window.
/// </summary>
internal static class AtomicFileWriter
{
    public static void WriteAllText(string path, string content)
    {
        // Resolve symlinks at the destination so writes flow to the link
        // target (preserving the link), rather than the rename replacing
        // the link with a regular file. The previous File.WriteAllText path
        // followed symlinks by default; preserving that contract avoids
        // breaking sync-folder / dotfiles symlink workflows.
        var finalPath = ResolveSymlink(path);
        var finalDir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(finalDir))
        {
            Directory.CreateDirectory(finalDir);
        }

        // Per-call random sidecar so concurrent writers to the same logical
        // path do not collide on a fixed `.tmp` name. FileMode.CreateNew
        // refuses to clobber a leftover from another in-flight writer.
        var tempPath = Path.Combine(
            finalDir ?? string.Empty,
            Path.GetFileName(finalPath) + "." + Path.GetRandomFileName() + ".tmp"
        );
        try
        {
            WriteThroughTemp(tempPath, content);
            MoveWithRetry(tempPath, finalPath);
            FsyncDirectoryIfPosix(finalDir);
        }
        finally
        {
            CleanupOrphanTemp(tempPath);
        }
    }

    private static void WriteThroughTemp(string tempPath, string content)
    {
        using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        // Force the file's data pages to durable storage BEFORE the rename.
        // Without this a crash could leave the rename visible while the file
        // content is still in the page cache.
        stream.Flush(flushToDisk: true);
    }

    private static void CleanupOrphanTemp(string tempPath)
    {
        // If File.Move succeeded the sidecar is gone (rename consumed it).
        // If it threw, clean up so a failed write does not leave a stray
        // `.<random>.tmp` next to the destination.
        if (!File.Exists(tempPath))
        {
            return;
        }
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup; suppress so we do not mask the original
            // write exception (if any) on its way out of the caller.
        }
        catch (UnauthorizedAccessException)
        {
            // Same posture.
        }
    }

    private static string ResolveSymlink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch (IOException)
        {
            return path;
        }
        catch (UnauthorizedAccessException)
        {
            return path;
        }
    }

    private static void MoveWithRetry(string source, string destination)
    {
        const int maxAttempts = 5;
        var delayMs = 25;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                System.Threading.Thread.Sleep(delayMs);
                delayMs *= 2;
            }
        }
    }

    private static void FsyncDirectoryIfPosix(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        // open(O_RDONLY) + fsync(fd) + close(fd). Best-effort; any failure
        // is swallowed because we do not want a metadata-flush problem to
        // mask the successful rename.
        var fd = Libc.Open(directory, 0);
        if (fd < 0)
        {
            return;
        }
        try
        {
            _ = Libc.Fsync(fd);
        }
        finally
        {
            _ = Libc.Close(fd);
        }
    }

    private static class Libc
    {
        [DllImport(
            "libc",
            EntryPoint = "open",
            SetLastError = true,
            CharSet = CharSet.Ansi,
            BestFitMapping = false,
            ThrowOnUnmappableChar = true
        )]
        public static extern int Open(string path, int flags);

        [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
        public static extern int Fsync(int fd);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        public static extern int Close(int fd);
    }
}

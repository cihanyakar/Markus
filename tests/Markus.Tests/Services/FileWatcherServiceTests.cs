using Markus.Services;

namespace Markus.Tests.Services;

public sealed class FileWatcherServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileWatcherService _sut;

    public FileWatcherServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "markus-test-fswatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new FileWatcherService();
    }

    public void Dispose()
    {
        _sut.Dispose();
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
    public void Watch_ValidPath_SetsWatchedPath()
    {
        var file = CreateTempFile("test.md");

        _sut.Watch(file);

        _sut.WatchedPath.ShouldBe(file);
    }

    [Fact]
    public void Watch_ReplacesExistingWatch()
    {
        var first = CreateTempFile("first.md");
        var second = CreateTempFile("second.md");

        _sut.Watch(first);
        _sut.Watch(second);

        _sut.WatchedPath.ShouldBe(second);
    }

    [Fact]
    public void Stop_ClearsWatchedPath()
    {
        var file = CreateTempFile("test.md");
        _sut.Watch(file);

        _sut.Stop();

        _sut.WatchedPath.ShouldBeNull();
    }

    [Fact]
    public void Stop_WhenNotWatching_DoesNotThrow()
    {
        // Should be a no-op.
        Should.NotThrow(_sut.Stop);
    }

    [Fact]
    public void Watch_AfterStop_SetsNewPath()
    {
        var first = CreateTempFile("first.md");
        var second = CreateTempFile("second.md");

        _sut.Watch(first);
        _sut.Stop();
        _sut.Watch(second);

        _sut.WatchedPath.ShouldBe(second);
    }

    [Fact]
    public void Watch_EmptyFileName_DoesNotSetWatchedPath()
    {
        // A bare directory path has no file name component.
        _sut.Watch(_tempDir + Path.DirectorySeparatorChar);

        _sut.WatchedPath.ShouldBeNull();
    }

    [Fact]
    public void Watch_AfterDispose_ThrowsObjectDisposedException()
    {
        _sut.Dispose();

        Should.Throw<ObjectDisposedException>(() => _sut.Watch(CreateTempFile("test.md")));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        _sut.Dispose();

        Should.NotThrow(_sut.Dispose);
    }

    [Fact]
    public void Dispose_WhileWatching_ClearsWatchedPath()
    {
        _sut.Watch(CreateTempFile("test.md"));

        _sut.Dispose();

        _sut.WatchedPath.ShouldBeNull();
    }

    [Fact]
    public void FileChangedEvent_HasExpectedSignature()
    {
        FileChangedEventArgs? received = null;
        _sut.FileChanged += (_, args) => received = args;

        // We cannot easily trigger the internal event without filesystem activity,
        // but we can verify the event args type holds the expected data.
        var args = new FileChangedEventArgs("/some/path.md", WatcherChangeTypes.Changed);

        args.Path.ShouldBe("/some/path.md");
        args.Change.ShouldBe(WatcherChangeTypes.Changed);
    }

    [Fact]
    public void WatchedPath_InitiallyNull()
    {
        _sut.WatchedPath.ShouldBeNull();
    }

    private string CreateTempFile(string name)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "test content");
        return path;
    }
}

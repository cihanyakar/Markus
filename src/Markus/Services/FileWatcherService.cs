namespace Markus.Services;

internal sealed class FileWatcherService : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private int _generation;
    private bool _disposed;

    /// <summary>
    /// Raised on the thread-pool when the watched file changes; subscribers
    /// must marshal to the UI thread themselves.
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? FileChanged;

    public string? WatchedPath { get; private set; }

    public void Watch(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Stop();

        var dir = Path.GetDirectoryName(filePath);
        var name = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
        {
            return;
        }

        WatchedPath = filePath;
        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter =
                NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnFsEvent;
    }

    public void Stop()
    {
        if (_watcher is { } w)
        {
            w.EnableRaisingEvents = false;
            w.Changed -= OnFsEvent;
            w.Created -= OnFsEvent;
            w.Renamed -= OnRenamed;
            w.Deleted -= OnFsEvent;
            w.Dispose();
            _watcher = null;
        }

        // Invalidate any in-flight debounce callback: a Timer already queued on
        // the thread pool is not cancelled by Dispose, so a stale notification
        // could otherwise fire for a file we just stopped watching.
        _generation++;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        WatchedPath = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private void OnFsEvent(object? sender, FileSystemEventArgs e)
    {
        ScheduleNotify(e.FullPath, e.ChangeType);
    }

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        WatchedPath = e.FullPath;
        _watcher?.Filter = Path.GetFileName(e.FullPath);
        ScheduleNotify(e.FullPath, WatcherChangeTypes.Renamed);
    }

    private void ScheduleNotify(string path, WatcherChangeTypes change)
    {
        _debounceTimer?.Dispose();
        var generation = ++_generation;
        _debounceTimer = new Timer(
            _ =>
            {
                // Drop the notification if it was superseded by a newer event or
                // by Stop()/Dispose() between scheduling and firing.
                if (_disposed || generation != _generation)
                {
                    return;
                }
                FileChanged?.Invoke(this, new FileChangedEventArgs(path, change));
            },
            null,
            DebounceInterval,
            Timeout.InfiniteTimeSpan
        );
    }
}

internal sealed class FileChangedEventArgs : EventArgs
{
    public FileChangedEventArgs(string path, WatcherChangeTypes change)
    {
        Path = path;
        Change = change;
    }

    public string Path { get; }

    public WatcherChangeTypes Change { get; }
}

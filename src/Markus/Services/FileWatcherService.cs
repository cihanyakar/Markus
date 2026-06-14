namespace Markus.Services;

internal sealed class FileWatcherService : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    // Guards all mutable state below. Watch/Stop/Dispose run on the UI thread
    // while the FileSystemWatcher and debounce-timer callbacks run on pool
    // threads, so every field access is serialized through this lock.
    private readonly Lock _gate = new Lock();

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

        var dir = Path.GetDirectoryName(filePath);
        var name = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name))
        {
            return;
        }

        lock (_gate)
        {
            StopLocked();
            FileSystemWatcher? watcher = null;
            try
            {
#pragma warning disable IDE0017 // Object initialization simplification
                // Two-step construction (rather than an object initializer for
                // EnableRaisingEvents) keeps the partially-constructed watcher
                // disposable in the catch below. Kernel-level registration can
                // fail when EnableRaisingEvents = true even if the constructor
                // and NotifyFilter assignment both succeed; the initializer
                // form would leak the native handle on that path.
                watcher = new FileSystemWatcher(dir, name)
                {
                    NotifyFilter =
                        NotifyFilters.LastWrite
                        | NotifyFilters.Size
                        | NotifyFilters.CreationTime
                        | NotifyFilters.FileName,
                };
#pragma warning restore IDE0017
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
                when (ex
                        is ArgumentException
                            or FileNotFoundException
                            or PathTooLongException
                            or UnauthorizedAccessException
                            or IOException
                )
            {
                // Any path-resolution or kernel-registration failure (missing
                // parent, oversized path, revoked permission, transient
                // inotify or ReadDirectoryChangesW error) leaves the service
                // in its idle state instead of crashing the file-open flow.
                // Reload-on-change is the only feature lost. Dispose any
                // partially-constructed watcher to release the native handle.
                watcher?.Dispose();
                return;
            }

            WatchedPath = filePath;
            _watcher = watcher;
            _watcher.Changed += OnFsEvent;
            _watcher.Created += OnFsEvent;
            _watcher.Renamed += OnRenamed;
            _watcher.Deleted += OnFsEvent;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopLocked();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            StopLocked();
        }
    }

    private void StopLocked()
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

    private void OnFsEvent(object? sender, FileSystemEventArgs e)
    {
        ScheduleNotify(e.FullPath, e.ChangeType);
    }

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        lock (_gate)
        {
            if (_watcher is null)
            {
                return;
            }
            WatchedPath = e.FullPath;
            _watcher.Filter = Path.GetFileName(e.FullPath);
        }
        ScheduleNotify(e.FullPath, WatcherChangeTypes.Renamed);
    }

    private void ScheduleNotify(string path, WatcherChangeTypes change)
    {
        int generation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _debounceTimer?.Dispose();
            generation = ++_generation;
            _debounceTimer = new Timer(
                _ => FireIfCurrent(generation, path, change),
                null,
                DebounceInterval,
                Timeout.InfiniteTimeSpan
            );
        }
    }

    private void FireIfCurrent(int generation, string path, WatcherChangeTypes change)
    {
        // Capture the guard result and the handler under the lock, then raise the
        // event outside it so a subscriber can never deadlock against Watch/Stop
        // on the UI thread and so a concurrent unsubscribe is observed atomically.
        EventHandler<FileChangedEventArgs>? handler;
        lock (_gate)
        {
            if (_disposed || generation != _generation)
            {
                return;
            }
            handler = FileChanged;
        }
        handler?.Invoke(this, new FileChangedEventArgs(path, change));
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

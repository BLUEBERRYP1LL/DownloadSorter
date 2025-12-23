using DownloadSorter.Core.Configuration;

namespace DownloadSorter.Core.Services;

/// <summary>
/// Watches the inbox folder for new files and triggers sorting after settle time.
/// </summary>
public class FileWatcherService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly FileSorterService _sorter;
    private readonly SettleTimeTracker _settleTracker;
    private FileSystemWatcher? _watcher;
    private bool _disposed;
    private bool _isPaused;

    public bool IsRunning => _watcher?.EnableRaisingEvents ?? false;
    public bool IsPaused => _isPaused;
    public int PendingCount => _settleTracker.PendingCount;

    public FileWatcherService(AppSettings settings, FileSorterService sorter)
    {
        _settings = settings;
        _sorter = sorter;
        _settleTracker = new SettleTimeTracker(settings.SettleTimeSeconds);
        _settleTracker.FileSettled += OnFileSettled;
    }

    /// <summary>
    /// Start watching the inbox folder.
    /// </summary>
    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileWatcherService));

        // Ensure inbox exists
        if (!Directory.Exists(_settings.InboxPath))
        {
            Directory.CreateDirectory(_settings.InboxPath);
        }

        // Create watcher
        _watcher = new FileSystemWatcher(_settings.InboxPath)
        {
            NotifyFilter = NotifyFilters.FileName |
                          NotifyFilters.Size |
                          NotifyFilters.LastWrite |
                          NotifyFilters.CreationTime,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnFileEvent;
        _watcher.Changed += OnFileEvent;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        _watcher.EnableRaisingEvents = true;

        // Process any files already in inbox
        ProcessExistingFiles();
    }

    /// <summary>
    /// Stop watching.
    /// </summary>
    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    /// <summary>
    /// Pause sorting (still tracks files, but doesn't sort them).
    /// </summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>
    /// Resume sorting.
    /// </summary>
    public void Resume()
    {
        _isPaused = false;
    }

    private void ProcessExistingFiles()
    {
        if (!Directory.Exists(_settings.InboxPath)) return;

        foreach (var file in Directory.GetFiles(_settings.InboxPath))
        {
            if (!_settings.ShouldIgnore(file))
            {
                _settleTracker.OnFileEvent(file);
            }
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;

        // Skip temp files
        if (_settings.ShouldIgnore(e.FullPath)) return;

        // Track the file
        _settleTracker.OnFileEvent(e.FullPath);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_disposed) return;

        // Stop tracking old path
        _settleTracker.StopTracking(e.OldFullPath);

        // If new name is not a temp file, track it
        if (!_settings.ShouldIgnore(e.FullPath))
        {
            _settleTracker.OnFileEvent(e.FullPath);
        }
    }

    private void OnFileSettled(object? sender, FileSettledEventArgs e)
    {
        if (_disposed || _isPaused) return;

        // File has been stable - sort it
        _sorter.SortFile(e.FilePath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // Watcher failed, try to restart
        try
        {
            Stop();
            Thread.Sleep(1000);
            Start();
        }
        catch
        {
            // Log but don't crash
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _settleTracker.FileSettled -= OnFileSettled;
        _settleTracker.Dispose();
    }
}

using System.Collections.Concurrent;

namespace DownloadSorter.Core.Services;

/// <summary>
/// Tracks files and only signals when they've been unchanged for the settle time period.
/// Prevents sorting partial downloads.
/// </summary>
public class SettleTimeTracker : IDisposable
{
    private readonly ConcurrentDictionary<string, FileState> _pendingFiles = new();
    private readonly Timer _checkTimer;
    private readonly int _settleTimeMs;
    private bool _disposed;

    /// <summary>
    /// Fired when a file has been stable for the settle time.
    /// </summary>
    public event EventHandler<FileSettledEventArgs>? FileSettled;

    public SettleTimeTracker(int settleTimeSeconds)
    {
        _settleTimeMs = settleTimeSeconds * 1000;
        _checkTimer = new Timer(CheckPendingFiles, null, 1000, 1000);
    }

    /// <summary>
    /// Notify tracker of a file event (create, change, rename).
    /// </summary>
    public void OnFileEvent(string filePath)
    {
        if (_disposed) return;

        try
        {
            if (!File.Exists(filePath))
            {
                // File was deleted, remove from tracking
                _pendingFiles.TryRemove(filePath, out _);
                return;
            }

            var fileInfo = new FileInfo(filePath);
            var state = new FileState
            {
                FilePath = filePath,
                LastSeen = DateTime.UtcNow,
                LastSize = fileInfo.Length,
                LastWriteTime = fileInfo.LastWriteTimeUtc
            };

            _pendingFiles.AddOrUpdate(filePath, state, (_, _) => state);
        }
        catch (IOException)
        {
            // File might be locked, will retry on next event
        }
        catch (UnauthorizedAccessException)
        {
            // No access, remove from tracking
            _pendingFiles.TryRemove(filePath, out _);
        }
    }

    /// <summary>
    /// Stop tracking a file (e.g., after it's been sorted).
    /// </summary>
    public void StopTracking(string filePath)
    {
        _pendingFiles.TryRemove(filePath, out _);
    }

    /// <summary>
    /// Get count of files currently being tracked.
    /// </summary>
    public int PendingCount => _pendingFiles.Count;

    /// <summary>
    /// Get list of files currently being tracked.
    /// </summary>
    public IEnumerable<string> GetPendingFiles() => _pendingFiles.Keys.ToList();

    private void CheckPendingFiles(object? state)
    {
        if (_disposed) return;

        var now = DateTime.UtcNow;
        var settledFiles = new List<string>();

        foreach (var (path, fileState) in _pendingFiles)
        {
            try
            {
                if (!File.Exists(path))
                {
                    // File gone, stop tracking
                    _pendingFiles.TryRemove(path, out _);
                    continue;
                }

                var fileInfo = new FileInfo(path);
                var currentSize = fileInfo.Length;
                var currentWriteTime = fileInfo.LastWriteTimeUtc;

                // Check if file has changed since last check
                if (currentSize != fileState.LastSize ||
                    currentWriteTime != fileState.LastWriteTime)
                {
                    // File changed, reset timer
                    var newState = new FileState
                    {
                        FilePath = path,
                        LastSeen = now,
                        LastSize = currentSize,
                        LastWriteTime = currentWriteTime
                    };
                    _pendingFiles.TryUpdate(path, newState, fileState);
                    continue;
                }

                // File unchanged - check if settle time has passed
                var elapsed = (now - fileState.LastSeen).TotalMilliseconds;
                if (elapsed >= _settleTimeMs)
                {
                    settledFiles.Add(path);
                }
            }
            catch (IOException)
            {
                // File is locked, will retry
            }
            catch (UnauthorizedAccessException)
            {
                _pendingFiles.TryRemove(path, out _);
            }
        }

        // Fire events for settled files
        foreach (var path in settledFiles)
        {
            if (_pendingFiles.TryRemove(path, out var finalState))
            {
                try
                {
                    FileSettled?.Invoke(this, new FileSettledEventArgs
                    {
                        FilePath = path,
                        FileSize = finalState.LastSize
                    });
                }
                catch
                {
                    // Don't let handler exceptions break the timer
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _checkTimer.Dispose();
        _pendingFiles.Clear();
    }

    private class FileState
    {
        public required string FilePath { get; set; }
        public DateTime LastSeen { get; set; }
        public long LastSize { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}

public class FileSettledEventArgs : EventArgs
{
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
}

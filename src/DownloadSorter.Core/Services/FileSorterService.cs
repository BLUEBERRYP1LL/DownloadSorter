using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Data;

namespace DownloadSorter.Core.Services;

/// <summary>
/// Main service that categorizes and moves files from inbox to destination folders.
/// </summary>
public class FileSorterService
{
    private readonly AppSettings _settings;
    private readonly Repository _repository;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    /// <summary>
    /// Fired when a file is successfully sorted.
    /// </summary>
    public event EventHandler<FileSortedEventArgs>? FileSorted;

    /// <summary>
    /// Fired when a file sort fails or is skipped.
    /// </summary>
    public event EventHandler<FileSortFailedEventArgs>? FileSortFailed;

    public FileSorterService(AppSettings settings, Repository repository)
    {
        _settings = settings;
        _repository = repository;
    }

    /// <summary>
    /// Sort a single file from inbox to its destination.
    /// </summary>
    public SortResult SortFile(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        Logger.Debug($"Processing file: {fileName}");

        try
        {
            // Validate file exists
            if (!File.Exists(sourcePath))
            {
                Logger.Debug($"File no longer exists: {fileName}");
                return SortResult.Skip("File no longer exists");
            }

            // Check if it's a temp file we should ignore
            if (_settings.ShouldIgnore(sourcePath))
            {
                Logger.Debug($"Ignoring temp file: {fileName}");
                return SortResult.Skip("Temp file extension");
            }

            // Get file info
            var fileInfo = new FileInfo(sourcePath);
            var extension = fileInfo.Extension;
            var fileSize = fileInfo.Length;

            // Determine destination
            var destFolder = _settings.GetDestinationFolder(extension, fileSize);
            var category = Path.GetFileName(destFolder);

            // Ensure destination folder exists
            Directory.CreateDirectory(destFolder);

            // Handle collisions
            var destPath = CollisionResolver.GetUniqueFilePath(destFolder, fileName);
            var finalName = Path.GetFileName(destPath);

            // Move file with retry
            MoveFileWithRetry(sourcePath, destPath);

            // Log to database
            var record = new FileRecord
            {
                OriginalName = fileName,
                FinalName = finalName,
                SourcePath = sourcePath,
                DestPath = destPath,
                Category = category,
                FileSize = fileSize,
                SortedAt = DateTime.UtcNow,
                Status = SortStatus.Success
            };
            _repository.Insert(record);

            // Fire event
            FileSorted?.Invoke(this, new FileSortedEventArgs
            {
                OriginalName = fileName,
                FinalName = finalName,
                SourcePath = sourcePath,
                DestPath = destPath,
                Category = category,
                FileSize = fileSize
            });

            Logger.Info($"Sorted: {fileName} -> {category}");
            return SortResult.Ok(destPath, category);
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            Logger.Warn($"File locked, skipping: {fileName}");
            LogFailure(sourcePath, fileName, "File is locked", SortStatus.Skipped);
            return SortResult.Skip("File is locked");
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Error($"Access denied: {fileName}", ex);
            LogFailure(sourcePath, fileName, ex.Message, SortStatus.Failed);
            return SortResult.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to sort: {fileName}", ex);
            LogFailure(sourcePath, fileName, ex.Message, SortStatus.Failed);
            return SortResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Sort all eligible files from the default inbox.
    /// </summary>
    public List<SortResult> SortAllPending()
    {
        return SortFromFolder(_settings.InboxPath);
    }

    /// <summary>
    /// Sort all eligible files from all configured watch folders.
    /// </summary>
    public List<SortResult> SortFromAllWatchFolders()
    {
        var results = new List<SortResult>();

        foreach (var folder in _settings.AllWatchFolders)
        {
            results.AddRange(SortFromFolder(folder));
        }

        return results;
    }

    /// <summary>
    /// Sort all eligible files from a specific folder.
    /// </summary>
    public List<SortResult> SortFromFolder(string folderPath)
    {
        var results = new List<SortResult>();

        if (!Directory.Exists(folderPath))
        {
            return results;
        }

        var files = Directory.GetFiles(folderPath);

        foreach (var file in files)
        {
            // Skip temp files
            if (_settings.ShouldIgnore(file))
            {
                continue;
            }

            var result = SortFile(file);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Get count of files in inbox that could be sorted.
    /// </summary>
    public int GetPendingCount()
    {
        if (!Directory.Exists(_settings.InboxPath))
        {
            return 0;
        }

        return Directory.GetFiles(_settings.InboxPath)
            .Count(f => !_settings.ShouldIgnore(f));
    }

    private void MoveFileWithRetry(string source, string dest)
    {
        Exception? lastException = null;

        for (int i = 0; i < MaxRetries; i++)
        {
            try
            {
                File.Move(source, dest);
                return;
            }
            catch (IOException ex) when (IsFileLocked(ex) && i < MaxRetries - 1)
            {
                lastException = ex;
                Thread.Sleep(RetryDelayMs);
            }
        }

        throw lastException ?? new IOException("Failed to move file after retries");
    }

    private void LogFailure(string sourcePath, string fileName, string error, SortStatus status)
    {
        try
        {
            var record = new FileRecord
            {
                OriginalName = fileName,
                FinalName = fileName,
                SourcePath = sourcePath,
                DestPath = sourcePath,
                Category = "ERROR",
                FileSize = 0,
                SortedAt = DateTime.UtcNow,
                Status = status,
                ErrorMessage = error
            };
            _repository.Insert(record);
        }
        catch
        {
            // Don't fail on logging failure
        }

        FileSortFailed?.Invoke(this, new FileSortFailedEventArgs
        {
            FileName = fileName,
            SourcePath = sourcePath,
            Error = error,
            Status = status
        });
    }

    private static bool IsFileLocked(IOException ex)
    {
        var errorCode = ex.HResult & 0xFFFF;
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
    }
}

public class SortResult
{
    public bool Success { get; private set; }
    public bool Skipped { get; private set; }
    public string? DestPath { get; private set; }
    public string? Category { get; private set; }
    public string? Error { get; private set; }

    public static SortResult Ok(string destPath, string category) => new()
    {
        Success = true,
        DestPath = destPath,
        Category = category
    };

    public static SortResult Skip(string reason) => new()
    {
        Skipped = true,
        Error = reason
    };

    public static SortResult Fail(string error) => new()
    {
        Error = error
    };
}

public class FileSortedEventArgs : EventArgs
{
    public required string OriginalName { get; set; }
    public required string FinalName { get; set; }
    public required string SourcePath { get; set; }
    public required string DestPath { get; set; }
    public required string Category { get; set; }
    public long FileSize { get; set; }
}

public class FileSortFailedEventArgs : EventArgs
{
    public required string FileName { get; set; }
    public required string SourcePath { get; set; }
    public required string Error { get; set; }
    public SortStatus Status { get; set; }
}

namespace DownloadSorter.Core.Services;

/// <summary>
/// Simple file logger for DownloadSorter.
/// Logs to %LOCALAPPDATA%\DownloadSorter\logs\
/// </summary>
public static class Logger
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadSorter", "logs");

    private static readonly object Lock = new();
    private static string? _currentLogFile;
    private static DateTime _currentLogDate;

    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Log a debug message.
    /// </summary>
    public static void Debug(string message) => Log(LogLevel.Debug, message);

    /// <summary>
    /// Log an info message.
    /// </summary>
    public static void Info(string message) => Log(LogLevel.Info, message);

    /// <summary>
    /// Log a warning message.
    /// </summary>
    public static void Warn(string message) => Log(LogLevel.Warn, message);

    /// <summary>
    /// Log an error message.
    /// </summary>
    public static void Error(string message) => Log(LogLevel.Error, message);

    /// <summary>
    /// Log an error with exception details.
    /// </summary>
    public static void Error(string message, Exception ex)
    {
        Log(LogLevel.Error, $"{message}: {ex.GetType().Name}: {ex.Message}");
        if (MinLevel == LogLevel.Debug)
        {
            Log(LogLevel.Debug, ex.StackTrace ?? "No stack trace");
        }
    }

    /// <summary>
    /// Log a message at the specified level.
    /// </summary>
    public static void Log(LogLevel level, string message)
    {
        if (level < MinLevel)
            return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper().PadRight(5);
            var line = $"[{timestamp}] [{levelStr}] {message}";

            lock (Lock)
            {
                EnsureLogFile();
                if (_currentLogFile != null)
                {
                    File.AppendAllText(_currentLogFile, line + Environment.NewLine);
                }
            }
        }
        catch
        {
            // Never crash on logging failure
        }
    }

    private static void EnsureLogFile()
    {
        var today = DateTime.Today;

        if (_currentLogFile != null && _currentLogDate == today)
            return;

        try
        {
            Directory.CreateDirectory(LogDirectory);

            _currentLogDate = today;
            _currentLogFile = Path.Combine(LogDirectory, $"sorter-{today:yyyy-MM-dd}.log");

            // Clean up old log files (keep 7 days)
            CleanOldLogs();
        }
        catch
        {
            _currentLogFile = null;
        }
    }

    private static void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.Today.AddDays(-7);
            var files = Directory.GetFiles(LogDirectory, "sorter-*.log");

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.Length > 7)
                {
                    var dateStr = fileName[7..]; // After "sorter-"
                    if (DateTime.TryParse(dateStr, out var fileDate) && fileDate < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    /// <summary>
    /// Get the current log file path.
    /// </summary>
    public static string? GetCurrentLogPath()
    {
        lock (Lock)
        {
            EnsureLogFile();
            return _currentLogFile;
        }
    }
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

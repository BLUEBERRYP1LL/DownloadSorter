using System.Text.Json;

namespace DownloadSorter.Core.Configuration;

public class AppSettings
{
    private static readonly string DefaultConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DownloadSorter");

    public static string DefaultConfigPath => Path.Combine(DefaultConfigDir, "appsettings.json");
    public static string DefaultDatabasePath => Path.Combine(DefaultConfigDir, "sorter.db");

    /// <summary>
    /// Root path where sorted folders live (user configures on first run).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Additional folders to watch for files to sort.
    /// These are external folders (like browser download folders) that get sorted into RootPath categories.
    /// </summary>
    public List<string> WatchFolders { get; set; } = [];

    /// <summary>
    /// Path to the default inbox folder (RootPath/00_INBOX).
    /// </summary>
    public string InboxPath => Path.Combine(RootPath, "00_INBOX");

    /// <summary>
    /// All folders to watch: InboxPath + any additional WatchFolders.
    /// </summary>
    public IEnumerable<string> AllWatchFolders
    {
        get
        {
            if (!string.IsNullOrEmpty(RootPath))
                yield return InboxPath;

            foreach (var folder in WatchFolders)
            {
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    yield return folder;
            }
        }
    }

    /// <summary>
    /// Seconds a file must be unchanged before sorting.
    /// </summary>
    public int SettleTimeSeconds { get; set; } = 180;

    /// <summary>
    /// Path to SQLite database.
    /// </summary>
    public string DatabasePath { get; set; } = DefaultDatabasePath;

    /// <summary>
    /// Extensions to ignore (partial downloads).
    /// </summary>
    public string[] IgnoreExtensions { get; set; } =
        [".crdownload", ".part", ".tmp", ".download", ".partial"];

    /// <summary>
    /// Files larger than this (bytes) go to Big_Files folder.
    /// Default: 1GB
    /// </summary>
    public long BigFileThreshold { get; set; } = 1024L * 1024L * 1024L;

    /// <summary>
    /// Enable big file routing.
    /// </summary>
    public bool EnableBigFileRouting { get; set; } = true;

    /// <summary>
    /// Show balloon notifications.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Category routing rules.
    /// </summary>
    public List<CategoryRule> Categories { get; set; } = GetDefaultCategories();

    /// <summary>
    /// Folder names for each category.
    /// </summary>
    public static class Folders
    {
        public const string Inbox = "00_INBOX";
        public const string Pinned = "00_PINNED";
        public const string Documents = "10_Documents";
        public const string Executables = "20_Executables";
        public const string Archives = "30_Archives";
        public const string Media = "40_Media";
        public const string Code = "50_Code";
        public const string ISOs = "60_ISOs";
        public const string BigFiles = "80_Big_Files";
        public const string Unsorted = "_UNSORTED";

        public static readonly string[] All =
        [
            Inbox, Pinned, Documents, Executables, Archives,
            Media, Code, ISOs, BigFiles, Unsorted
        ];
    }

    public static List<CategoryRule> GetDefaultCategories() =>
    [
        new CategoryRule
        {
            Category = Folders.Documents,
            Extensions = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                         ".txt", ".rtf", ".odt", ".ods", ".odp", ".csv", ".epub", ".mobi"]
        },
        new CategoryRule
        {
            Category = Folders.Executables,
            Extensions = [".exe", ".msi", ".msix", ".appx", ".bat", ".cmd"]
        },
        new CategoryRule
        {
            Category = Folders.Archives,
            Extensions = [".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz"]
        },
        new CategoryRule
        {
            Category = Folders.Media,
            Extensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico",
                         ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
                         ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".wma"]
        },
        new CategoryRule
        {
            Category = Folders.Code,
            Extensions = [".json", ".xml", ".yml", ".yaml", ".toml", ".ini", ".conf", ".config",
                         ".py", ".js", ".ts", ".cs", ".java", ".cpp", ".c", ".h", ".go", ".rs",
                         ".ps1", ".sh", ".bash", ".sql", ".html", ".css", ".scss", ".less"]
        },
        new CategoryRule
        {
            Category = Folders.ISOs,
            Extensions = [".iso", ".img", ".dmg", ".vhd", ".vhdx", ".vmdk"]
        }
    ];

    public string GetDestinationFolder(string extension, long fileSize)
    {
        // Big file override
        if (EnableBigFileRouting && fileSize >= BigFileThreshold)
        {
            return Path.Combine(RootPath, Folders.BigFiles);
        }

        // Find matching category
        foreach (var rule in Categories)
        {
            if (rule.Matches(extension))
            {
                var basePath = Path.Combine(RootPath, rule.Category);
                return rule.Subfolder != null
                    ? Path.Combine(basePath, rule.Subfolder)
                    : basePath;
            }
        }

        // Default to unsorted
        return Path.Combine(RootPath, Folders.Unsorted);
    }

    public bool ShouldIgnore(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return IgnoreExtensions.Any(e =>
            e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    public void CreateFolderStructure()
    {
        foreach (var folder in Folders.All)
        {
            var path = Path.Combine(RootPath, folder);
            Directory.CreateDirectory(path);
        }
    }

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultConfigPath;

        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(path);

            // Check for empty or whitespace-only file
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings == null)
            {
                return new AppSettings();
            }

            // Validate and fix loaded settings
            settings.Validate();
            return settings;
        }
        catch (JsonException)
        {
            // Corrupted JSON - return defaults
            // Could optionally backup the corrupted file here
            return new AppSettings();
        }
        catch (IOException)
        {
            // File access error - return defaults
            return new AppSettings();
        }
    }

    /// <summary>
    /// Validates and repairs settings after loading.
    /// </summary>
    public void Validate()
    {
        // Ensure Categories is not null
        Categories ??= GetDefaultCategories();

        // Ensure WatchFolders is not null
        WatchFolders ??= [];

        // Ensure IgnoreExtensions is not null
        IgnoreExtensions ??= [".crdownload", ".part", ".tmp", ".download", ".partial"];

        // Remove invalid watch folders
        WatchFolders.RemoveAll(f => string.IsNullOrWhiteSpace(f));

        // Ensure reasonable values
        if (SettleTimeSeconds < 0)
            SettleTimeSeconds = 180;

        if (BigFileThreshold < 0)
            BigFileThreshold = 1024L * 1024L * 1024L;
    }

    public void Save(string? path = null)
    {
        path ??= DefaultConfigPath;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

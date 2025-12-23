using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Data;
using DownloadSorter.Core.Services;

namespace DownloadSorter.Tests;

/// <summary>
/// Integration tests that test complete workflows end-to-end.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _rootPath;
    private readonly string _configPath;
    private readonly AppSettings _settings;

    public IntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SorterIntegration_{Guid.NewGuid():N}");
        _rootPath = Path.Combine(_testDir, "Sorted");
        _configPath = Path.Combine(_testDir, "config.json");

        Directory.CreateDirectory(_testDir);

        _settings = new AppSettings
        {
            RootPath = _rootPath,
            DatabasePath = Path.Combine(_testDir, "test.db")
        };
        _settings.CreateFolderStructure();
        _settings.Save(_configPath);
    }

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Thread.Sleep(100);

        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void FullWorkflow_DropFileInInbox_GetsSorted()
    {
        // Arrange - drop a file in inbox
        var inboxFile = Path.Combine(_settings.InboxPath, "report.pdf");
        File.WriteAllText(inboxFile, "PDF content");

        // Act - sort
        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        var results = sorter.SortAllPending();

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("10_Documents", results[0].Category);
        Assert.False(File.Exists(inboxFile)); // Moved from inbox
        Assert.True(File.Exists(results[0].DestPath)); // Exists in destination
    }

    [Fact]
    public void FullWorkflow_MultipleFilesFromMultipleFolders()
    {
        // Arrange - create external watch folder
        var externalDownloads = Path.Combine(_testDir, "BrowserDownloads");
        Directory.CreateDirectory(externalDownloads);
        _settings.WatchFolders.Add(externalDownloads);

        // Drop files in both locations
        File.WriteAllText(Path.Combine(_settings.InboxPath, "doc.pdf"), "");
        File.WriteAllText(Path.Combine(externalDownloads, "setup.exe"), "");
        File.WriteAllText(Path.Combine(externalDownloads, "data.zip"), "");

        // Act
        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        var results = sorter.SortFromAllWatchFolders();

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));

        // Verify categories
        var categories = results.Select(r => r.Category).ToList();
        Assert.Contains("10_Documents", categories);
        Assert.Contains("20_Executables", categories);
        Assert.Contains("30_Archives", categories);
    }

    [Fact]
    public void FullWorkflow_DuplicateFilesGetNumbered()
    {
        // Arrange - create multiple files with same name
        for (int i = 0; i < 3; i++)
        {
            var file = Path.Combine(_settings.InboxPath, "report.pdf");
            File.WriteAllText(file, $"Content {i}");

            using var repo = new Repository(_settings.DatabasePath);
            var sorter = new FileSorterService(_settings, repo);
            sorter.SortAllPending();
        }

        // Assert - check destination folder
        var docsFolder = Path.Combine(_rootPath, "10_Documents");
        var files = Directory.GetFiles(docsFolder);

        Assert.Equal(3, files.Length);
        Assert.Contains(files, f => f.EndsWith("report.pdf"));
        Assert.Contains(files, f => f.EndsWith("report (2).pdf"));
        Assert.Contains(files, f => f.EndsWith("report (3).pdf"));
    }

    [Fact]
    public void FullWorkflow_TempFilesAreSkipped()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_settings.InboxPath, "downloading.crdownload"), "");
        File.WriteAllText(Path.Combine(_settings.InboxPath, "partial.part"), "");
        File.WriteAllText(Path.Combine(_settings.InboxPath, "complete.pdf"), "");

        // Act
        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        var results = sorter.SortAllPending();

        // Assert - only the PDF should be sorted
        Assert.Single(results);
        Assert.Equal("10_Documents", results[0].Category);

        // Temp files should still be in inbox
        Assert.True(File.Exists(Path.Combine(_settings.InboxPath, "downloading.crdownload")));
        Assert.True(File.Exists(Path.Combine(_settings.InboxPath, "partial.part")));
    }

    [Fact]
    public void FullWorkflow_BigFilesRouting()
    {
        // Arrange
        _settings.BigFileThreshold = 100; // 100 bytes for testing
        _settings.EnableBigFileRouting = true;

        var smallFile = Path.Combine(_settings.InboxPath, "small.pdf");
        var bigFile = Path.Combine(_settings.InboxPath, "big.pdf");

        File.WriteAllText(smallFile, "Small"); // < 100 bytes
        File.WriteAllText(bigFile, new string('X', 200)); // > 100 bytes

        // Act
        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        var results = sorter.SortAllPending();

        // Assert
        Assert.Equal(2, results.Count);

        var smallResult = results.First(r => r.DestPath!.Contains("small"));
        var bigResult = results.First(r => r.DestPath!.Contains("big"));

        Assert.Equal("10_Documents", smallResult.Category);
        Assert.Equal("80_Big_Files", bigResult.Category);
    }

    [Fact]
    public void FullWorkflow_HistoryIsRecorded()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_settings.InboxPath, "tracked.pdf"), "");

        // Act
        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        sorter.SortAllPending();

        // Assert - check database
        var history = repo.GetRecent(10);
        Assert.Single(history);
        Assert.Equal("tracked.pdf", history[0].OriginalName);
        Assert.Equal(SortStatus.Success, history[0].Status);
    }

    [Fact]
    public void FullWorkflow_SearchFindsFiles()
    {
        // Arrange - sort several files
        File.WriteAllText(Path.Combine(_settings.InboxPath, "invoice-2024-001.pdf"), "");
        File.WriteAllText(Path.Combine(_settings.InboxPath, "invoice-2024-002.pdf"), "");
        File.WriteAllText(Path.Combine(_settings.InboxPath, "report.pdf"), "");

        using var repo = new Repository(_settings.DatabasePath);
        var sorter = new FileSorterService(_settings, repo);
        sorter.SortAllPending();

        // Act - search
        var results = repo.Search("invoice");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("invoice", r.OriginalName));
    }

    [Fact]
    public void FullWorkflow_SettingsPersistence()
    {
        // Arrange
        var settings = new AppSettings
        {
            RootPath = _rootPath,
            SettleTimeSeconds = 120,
            BigFileThreshold = 500_000_000
        };
        settings.WatchFolders.Add(@"C:\Test\Downloads");

        // Act - save and reload
        settings.Save(_configPath);
        var loaded = AppSettings.Load(_configPath);

        // Assert
        Assert.Equal(_rootPath, loaded.RootPath);
        Assert.Equal(120, loaded.SettleTimeSeconds);
        Assert.Equal(500_000_000, loaded.BigFileThreshold);
        Assert.Single(loaded.WatchFolders);
    }

    [Fact]
    public void FullWorkflow_FolderStructureCreation()
    {
        // Arrange
        var newRoot = Path.Combine(_testDir, "NewSorted");
        var settings = new AppSettings { RootPath = newRoot };

        // Act
        settings.CreateFolderStructure();

        // Assert - all folders should exist
        Assert.True(Directory.Exists(Path.Combine(newRoot, "00_INBOX")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "00_PINNED")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "10_Documents")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "20_Executables")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "30_Archives")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "40_Media")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "50_Code")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "60_ISOs")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "80_Big_Files")));
        Assert.True(Directory.Exists(Path.Combine(newRoot, "_UNSORTED")));
    }
}

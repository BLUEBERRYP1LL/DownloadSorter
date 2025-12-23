using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Data;
using DownloadSorter.Core.Services;

namespace DownloadSorter.Tests;

public class FileSorterServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _rootPath;
    private readonly string _inboxPath;
    private readonly AppSettings _settings;
    private readonly Repository _repository;

    public FileSorterServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SorterTests_{Guid.NewGuid():N}");
        _rootPath = Path.Combine(_testDir, "Sorted");
        _inboxPath = Path.Combine(_rootPath, "00_INBOX");

        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_rootPath);
        Directory.CreateDirectory(_inboxPath);

        _settings = new AppSettings
        {
            RootPath = _rootPath,
            DatabasePath = Path.Combine(_testDir, "test.db")
        };
        _settings.CreateFolderStructure();

        _repository = new Repository(_settings.DatabasePath);
    }

    public void Dispose()
    {
        _repository.Dispose();

        // Force SQLite to release file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Small delay to ensure file handles are released
        Thread.Sleep(100);

        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup failures in tests - temp folder will be cleaned up eventually
        }
    }

    [Fact]
    public void SortFile_PdfFile_MovesToDocuments()
    {
        var sourceFile = Path.Combine(_inboxPath, "test.pdf");
        File.WriteAllText(sourceFile, "PDF content");

        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(sourceFile);

        Assert.True(result.Success);
        Assert.Equal("10_Documents", result.Category);
        Assert.False(File.Exists(sourceFile));
        Assert.True(File.Exists(result.DestPath));
    }

    [Fact]
    public void SortFile_ExeFile_MovesToExecutables()
    {
        var sourceFile = Path.Combine(_inboxPath, "setup.exe");
        File.WriteAllText(sourceFile, "EXE content");

        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(sourceFile);

        Assert.True(result.Success);
        Assert.Equal("20_Executables", result.Category);
    }

    [Fact]
    public void SortFile_ZipFile_MovesToArchives()
    {
        var sourceFile = Path.Combine(_inboxPath, "data.zip");
        File.WriteAllText(sourceFile, "ZIP content");

        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(sourceFile);

        Assert.True(result.Success);
        Assert.Equal("30_Archives", result.Category);
    }

    [Fact]
    public void SortFile_TempFile_IsSkipped()
    {
        var sourceFile = Path.Combine(_inboxPath, "download.crdownload");
        File.WriteAllText(sourceFile, "Partial content");

        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(sourceFile);

        Assert.True(result.Skipped);
        Assert.True(File.Exists(sourceFile)); // File should remain
    }

    [Fact]
    public void SortFile_NonexistentFile_IsSkipped()
    {
        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(Path.Combine(_inboxPath, "nonexistent.pdf"));

        Assert.True(result.Skipped);
    }

    [Fact]
    public void SortFile_UnknownExtension_GoesToUnsorted()
    {
        var sourceFile = Path.Combine(_inboxPath, "mystery.xyz123");
        File.WriteAllText(sourceFile, "Unknown content");

        var sorter = new FileSorterService(_settings, _repository);
        var result = sorter.SortFile(sourceFile);

        Assert.True(result.Success);
        Assert.Equal("_UNSORTED", result.Category);
    }

    [Fact]
    public void SortFile_DuplicateName_GetsNumbered()
    {
        // Create first file
        var sourceFile1 = Path.Combine(_inboxPath, "report.pdf");
        File.WriteAllText(sourceFile1, "First");

        var sorter = new FileSorterService(_settings, _repository);
        var result1 = sorter.SortFile(sourceFile1);

        // Create second file with same name
        var sourceFile2 = Path.Combine(_inboxPath, "report.pdf");
        File.WriteAllText(sourceFile2, "Second");
        var result2 = sorter.SortFile(sourceFile2);

        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Contains("report.pdf", result1.DestPath);
        Assert.Contains("report (2).pdf", result2.DestPath);
    }

    [Fact]
    public void SortFile_FiresFileSortedEvent()
    {
        var sourceFile = Path.Combine(_inboxPath, "test.pdf");
        File.WriteAllText(sourceFile, "Content");

        var sorter = new FileSorterService(_settings, _repository);
        FileSortedEventArgs? eventArgs = null;
        sorter.FileSorted += (sender, args) => eventArgs = args;

        sorter.SortFile(sourceFile);

        Assert.NotNull(eventArgs);
        Assert.Equal("test.pdf", eventArgs.OriginalName);
        Assert.Equal("10_Documents", eventArgs.Category);
    }

    [Fact]
    public void SortAllPending_SortsMultipleFiles()
    {
        File.WriteAllText(Path.Combine(_inboxPath, "doc.pdf"), "");
        File.WriteAllText(Path.Combine(_inboxPath, "app.exe"), "");
        File.WriteAllText(Path.Combine(_inboxPath, "data.zip"), "");

        var sorter = new FileSorterService(_settings, _repository);
        var results = sorter.SortAllPending();

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public void SortFromFolder_SortsFromSpecificFolder()
    {
        var customFolder = Path.Combine(_testDir, "CustomDownloads");
        Directory.CreateDirectory(customFolder);
        File.WriteAllText(Path.Combine(customFolder, "file.pdf"), "");

        var sorter = new FileSorterService(_settings, _repository);
        var results = sorter.SortFromFolder(customFolder);

        Assert.Single(results);
        Assert.True(results[0].Success);
    }

    [Fact]
    public void SortFromFolder_NonexistentFolder_ReturnsEmpty()
    {
        var sorter = new FileSorterService(_settings, _repository);
        var results = sorter.SortFromFolder(Path.Combine(_testDir, "NonExistent"));

        Assert.Empty(results);
    }

    [Fact]
    public void GetPendingCount_ReturnsCorrectCount()
    {
        File.WriteAllText(Path.Combine(_inboxPath, "a.pdf"), "");
        File.WriteAllText(Path.Combine(_inboxPath, "b.pdf"), "");
        File.WriteAllText(Path.Combine(_inboxPath, "c.crdownload"), ""); // Should be ignored

        var sorter = new FileSorterService(_settings, _repository);
        var count = sorter.GetPendingCount();

        Assert.Equal(2, count);
    }

    [Fact]
    public void SortFile_RecordsToDatabase()
    {
        var sourceFile = Path.Combine(_inboxPath, "tracked.pdf");
        File.WriteAllText(sourceFile, "Content");

        var sorter = new FileSorterService(_settings, _repository);
        sorter.SortFile(sourceFile);

        var records = _repository.GetRecent(10);
        Assert.Single(records);
        Assert.Equal("tracked.pdf", records[0].OriginalName);
        Assert.Equal(SortStatus.Success, records[0].Status);
    }
}

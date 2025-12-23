using DownloadSorter.Core.Configuration;

namespace DownloadSorter.Tests;

public class AppSettingsTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _configPath;

    public AppSettingsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SorterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _configPath = Path.Combine(_testDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaultSettings()
    {
        var settings = AppSettings.Load(Path.Combine(_testDir, "nonexistent.json"));

        Assert.Empty(settings.RootPath);
        Assert.Empty(settings.WatchFolders);
        Assert.Equal(180, settings.SettleTimeSeconds);
    }

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var original = new AppSettings
        {
            RootPath = @"C:\Test\Sorted",
            SettleTimeSeconds = 60,
            BigFileThreshold = 500_000_000
        };
        original.WatchFolders.Add(@"C:\Downloads");

        original.Save(_configPath);
        var loaded = AppSettings.Load(_configPath);

        Assert.Equal(original.RootPath, loaded.RootPath);
        Assert.Equal(original.SettleTimeSeconds, loaded.SettleTimeSeconds);
        Assert.Equal(original.BigFileThreshold, loaded.BigFileThreshold);
        Assert.Single(loaded.WatchFolders);
        Assert.Equal(@"C:\Downloads", loaded.WatchFolders[0]);
    }

    [Fact]
    public void GetDestinationFolder_DocumentExtension_ReturnsDocumentsFolder()
    {
        var settings = new AppSettings { RootPath = @"C:\Sorted" };

        var result = settings.GetDestinationFolder(".pdf", 1000);

        Assert.Equal(@"C:\Sorted\10_Documents", result);
    }

    [Fact]
    public void GetDestinationFolder_BigFile_ReturnsBigFilesFolder()
    {
        var settings = new AppSettings
        {
            RootPath = @"C:\Sorted",
            BigFileThreshold = 1_000_000,
            EnableBigFileRouting = true
        };

        var result = settings.GetDestinationFolder(".pdf", 5_000_000);

        Assert.Equal(@"C:\Sorted\80_Big_Files", result);
    }

    [Fact]
    public void GetDestinationFolder_BigFileRoutingDisabled_ReturnsNormalCategory()
    {
        var settings = new AppSettings
        {
            RootPath = @"C:\Sorted",
            BigFileThreshold = 1_000_000,
            EnableBigFileRouting = false
        };

        var result = settings.GetDestinationFolder(".pdf", 5_000_000);

        Assert.Equal(@"C:\Sorted\10_Documents", result);
    }

    [Fact]
    public void GetDestinationFolder_UnknownExtension_ReturnsUnsorted()
    {
        var settings = new AppSettings { RootPath = @"C:\Sorted" };

        var result = settings.GetDestinationFolder(".xyz123", 1000);

        Assert.Equal(@"C:\Sorted\_UNSORTED", result);
    }

    [Fact]
    public void ShouldIgnore_TempExtension_ReturnsTrue()
    {
        var settings = new AppSettings();

        Assert.True(settings.ShouldIgnore("file.crdownload"));
        Assert.True(settings.ShouldIgnore("file.part"));
        Assert.True(settings.ShouldIgnore("file.tmp"));
    }

    [Fact]
    public void ShouldIgnore_NormalExtension_ReturnsFalse()
    {
        var settings = new AppSettings();

        Assert.False(settings.ShouldIgnore("file.pdf"));
        Assert.False(settings.ShouldIgnore("file.exe"));
        Assert.False(settings.ShouldIgnore("file.zip"));
    }

    [Fact]
    public void AllWatchFolders_IncludesInboxAndWatchFolders()
    {
        var watchDir = Path.Combine(_testDir, "watch");
        Directory.CreateDirectory(watchDir);

        var settings = new AppSettings { RootPath = _testDir };
        settings.WatchFolders.Add(watchDir);

        var allFolders = settings.AllWatchFolders.ToList();

        Assert.Equal(2, allFolders.Count);
        Assert.Contains(settings.InboxPath, allFolders);
        Assert.Contains(watchDir, allFolders);
    }

    [Fact]
    public void AllWatchFolders_ExcludesNonexistentWatchFolders()
    {
        var settings = new AppSettings { RootPath = _testDir };
        settings.WatchFolders.Add(@"C:\NonExistent\Folder");

        var allFolders = settings.AllWatchFolders.ToList();

        Assert.Single(allFolders);
        Assert.Equal(settings.InboxPath, allFolders[0]);
    }

    [Theory]
    [InlineData(".pdf", "10_Documents")]
    [InlineData(".docx", "10_Documents")]
    [InlineData(".exe", "20_Executables")]
    [InlineData(".msi", "20_Executables")]
    [InlineData(".zip", "30_Archives")]
    [InlineData(".7z", "30_Archives")]
    [InlineData(".mp4", "40_Media")]
    [InlineData(".jpg", "40_Media")]
    [InlineData(".mp3", "40_Media")]
    [InlineData(".py", "50_Code")]
    [InlineData(".json", "50_Code")]
    [InlineData(".iso", "60_ISOs")]
    public void GetDestinationFolder_CorrectCategoryMapping(string extension, string expectedCategory)
    {
        var settings = new AppSettings { RootPath = @"C:\Sorted" };

        var result = settings.GetDestinationFolder(extension, 1000);

        Assert.Equal($@"C:\Sorted\{expectedCategory}", result);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        File.WriteAllText(_configPath, "{ this is not valid json }}}");

        var settings = AppSettings.Load(_configPath);

        Assert.Empty(settings.RootPath);
        Assert.NotNull(settings.Categories);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(_configPath, "");

        var settings = AppSettings.Load(_configPath);

        Assert.Empty(settings.RootPath);
    }

    [Fact]
    public void Validate_NegativeSettleTime_FixesToDefault()
    {
        var settings = new AppSettings { SettleTimeSeconds = -5 };

        settings.Validate();

        Assert.Equal(180, settings.SettleTimeSeconds);
    }

    [Fact]
    public void Validate_NegativeBigFileThreshold_FixesToDefault()
    {
        var settings = new AppSettings { BigFileThreshold = -1000 };

        settings.Validate();

        Assert.True(settings.BigFileThreshold > 0);
    }

    [Fact]
    public void Validate_EmptyWatchFolders_AreRemoved()
    {
        var settings = new AppSettings();
        settings.WatchFolders.Add("");
        settings.WatchFolders.Add("  ");
        settings.WatchFolders.Add(@"C:\Valid");

        settings.Validate();

        Assert.Single(settings.WatchFolders);
        Assert.Equal(@"C:\Valid", settings.WatchFolders[0]);
    }
}

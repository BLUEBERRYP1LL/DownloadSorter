using DownloadSorter.Core.Services;

namespace DownloadSorter.Tests;

public class CollisionResolverTests : IDisposable
{
    private readonly string _testDir;

    public CollisionResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"SorterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void GetUniqueFilePath_NoCollision_ReturnsOriginalPath()
    {
        var result = CollisionResolver.GetUniqueFilePath(_testDir, "test.pdf");

        Assert.Equal(Path.Combine(_testDir, "test.pdf"), result);
    }

    [Fact]
    public void GetUniqueFilePath_WithCollision_ReturnsNumberedPath()
    {
        // Create existing file
        File.WriteAllText(Path.Combine(_testDir, "test.pdf"), "");

        var result = CollisionResolver.GetUniqueFilePath(_testDir, "test.pdf");

        Assert.Equal(Path.Combine(_testDir, "test (2).pdf"), result);
    }

    [Fact]
    public void GetUniqueFilePath_MultipleCollisions_IncrementsCounter()
    {
        // Create existing files
        File.WriteAllText(Path.Combine(_testDir, "test.pdf"), "");
        File.WriteAllText(Path.Combine(_testDir, "test (2).pdf"), "");
        File.WriteAllText(Path.Combine(_testDir, "test (3).pdf"), "");

        var result = CollisionResolver.GetUniqueFilePath(_testDir, "test.pdf");

        Assert.Equal(Path.Combine(_testDir, "test (4).pdf"), result);
    }

    [Fact]
    public void GetUniqueFilePath_AlreadyNumbered_ContinuesSequence()
    {
        // Create existing file with number
        File.WriteAllText(Path.Combine(_testDir, "test (5).pdf"), "");

        var result = CollisionResolver.GetUniqueFilePath(_testDir, "test (5).pdf");

        Assert.Equal(Path.Combine(_testDir, "test (6).pdf"), result);
    }

    [Fact]
    public void GetUniqueFilePath_NoExtension_Works()
    {
        File.WriteAllText(Path.Combine(_testDir, "README"), "");

        var result = CollisionResolver.GetUniqueFilePath(_testDir, "README");

        Assert.Equal(Path.Combine(_testDir, "README (2)"), result);
    }

    [Fact]
    public void GetUniqueFileName_ReturnsJustFileName()
    {
        File.WriteAllText(Path.Combine(_testDir, "doc.txt"), "");

        var result = CollisionResolver.GetUniqueFileName(_testDir, "doc.txt");

        Assert.Equal("doc (2).txt", result);
    }
}

namespace DownloadSorter.Core.Data;

/// <summary>
/// Represents a file that was sorted.
/// </summary>
public class FileRecord
{
    public long Id { get; set; }
    public required string OriginalName { get; set; }
    public required string FinalName { get; set; }
    public required string SourcePath { get; set; }
    public required string DestPath { get; set; }
    public required string Category { get; set; }
    public long FileSize { get; set; }
    public DateTime SortedAt { get; set; }
    public string? FileHash { get; set; }
    public SortStatus Status { get; set; } = SortStatus.Success;
    public string? ErrorMessage { get; set; }
}

public enum SortStatus
{
    Success,
    Skipped,
    Failed
}

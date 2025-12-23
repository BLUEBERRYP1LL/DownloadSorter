namespace DownloadSorter.Core.Configuration;

/// <summary>
/// Maps file extensions to a destination category folder.
/// </summary>
public class CategoryRule
{
    public required string Category { get; set; }
    public required string[] Extensions { get; set; }

    /// <summary>
    /// Optional subfolder within the category (e.g., "PDFs" within "Documents").
    /// </summary>
    public string? Subfolder { get; set; }

    public bool Matches(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return Extensions.Any(e => e.TrimStart('.').Equals(ext, StringComparison.OrdinalIgnoreCase));
    }
}

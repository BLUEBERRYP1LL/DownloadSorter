namespace DownloadSorter.Core.Services;

/// <summary>
/// Handles filename collisions with Windows-style numbering: file (2).pdf, file (3).pdf, etc.
/// </summary>
public static class CollisionResolver
{
    /// <summary>
    /// Returns a unique file path in the destination folder.
    /// If the file already exists, appends (2), (3), etc.
    /// </summary>
    public static string GetUniqueFilePath(string destFolder, string fileName)
    {
        var destPath = Path.Combine(destFolder, fileName);

        if (!File.Exists(destPath))
        {
            return destPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 2;

        // Check if name already ends with (N) pattern
        var existingMatch = System.Text.RegularExpressions.Regex.Match(
            nameWithoutExt, @"^(.+)\s\((\d+)\)$");

        if (existingMatch.Success)
        {
            nameWithoutExt = existingMatch.Groups[1].Value;
            counter = int.Parse(existingMatch.Groups[2].Value) + 1;
        }

        while (File.Exists(destPath))
        {
            var newName = $"{nameWithoutExt} ({counter}){ext}";
            destPath = Path.Combine(destFolder, newName);
            counter++;

            // Safety limit to prevent infinite loops
            if (counter > 10000)
            {
                throw new InvalidOperationException(
                    $"Too many files with name '{fileName}' in '{destFolder}'");
            }
        }

        return destPath;
    }

    /// <summary>
    /// Gets just the filename portion of a unique path.
    /// </summary>
    public static string GetUniqueFileName(string destFolder, string fileName)
    {
        var fullPath = GetUniqueFilePath(destFolder, fileName);
        return Path.GetFileName(fullPath);
    }
}

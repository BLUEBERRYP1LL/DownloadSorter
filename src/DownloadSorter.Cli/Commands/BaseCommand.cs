using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Data;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

/// <summary>
/// Base class for commands that need settings and repository.
/// </summary>
public abstract class BaseCommand<TSettings> : Command<TSettings>
    where TSettings : CommandSettings
{
    protected AppSettings LoadSettings()
    {
        var settings = AppSettings.Load();

        if (string.IsNullOrEmpty(settings.RootPath))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Not configured. Run [blue]sorter init[/] first.");
        }

        return settings;
    }

    protected Repository CreateRepository(AppSettings settings)
    {
        return new Repository(settings.DatabasePath);
    }

    protected static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    protected static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;

        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";

        return dt.ToLocalTime().ToString("MMM dd");
    }
}

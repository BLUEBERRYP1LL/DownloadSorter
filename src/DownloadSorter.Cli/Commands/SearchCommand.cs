using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace DownloadSorter.Cli.Commands;

public class SearchCommand : BaseCommand<SearchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<query>")]
        public required string Query { get; set; }

        [CommandOption("-n|--count <COUNT>")]
        public int Count { get; set; } = 20;

        [CommandOption("-o|--open")]
        public bool OpenFolder { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        using var repo = CreateRepository(appSettings);
        var records = repo.Search(settings.Query, settings.Count);

        if (records.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No files found matching:[/] {settings.Query}");
            return 0;
        }

        AnsiConsole.Write(new Rule($"[bold blue]Search: {settings.Query}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("#");
        table.AddColumn("File");
        table.AddColumn("Size");
        table.AddColumn("Location");
        table.AddColumn("When");

        var index = 1;
        foreach (var record in records)
        {
            var fileName = record.FinalName;
            if (fileName.Length > 35)
            {
                fileName = fileName[..32] + "...";
            }

            var folder = Path.GetFileName(Path.GetDirectoryName(record.DestPath)) ?? "";

            table.AddRow(
                $"[dim]{index}[/]",
                fileName,
                $"[dim]{FormatSize(record.FileSize)}[/]",
                $"[blue]{folder}[/]",
                $"[dim]{FormatTimeAgo(record.SortedAt)}[/]"
            );
            index++;
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (settings.OpenFolder && records.Count > 0)
        {
            // Open first result's folder
            var firstResult = records[0];
            var folder = Path.GetDirectoryName(firstResult.DestPath);

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                AnsiConsole.MarkupLine($"[dim]Opening folder:[/] {folder}");
                OpenInExplorer(folder);
            }
        }
        else if (records.Count > 0)
        {
            AnsiConsole.MarkupLine("[dim]Tip: Use -o to open the folder containing the first result.[/]");
        }

        return 0;
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            // On Windows via WSL, use explorer.exe
            var windowsPath = path;

            // Convert WSL path to Windows path if needed
            if (path.StartsWith("/mnt/"))
            {
                var parts = path[5..].Split('/', 2);
                if (parts.Length == 2)
                {
                    windowsPath = $"{parts[0].ToUpper()}:\\{parts[1].Replace('/', '\\')}";
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = windowsPath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if can't open
        }
    }
}

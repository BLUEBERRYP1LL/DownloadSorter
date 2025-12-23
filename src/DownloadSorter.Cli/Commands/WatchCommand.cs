using DownloadSorter.Core.Configuration;
using DownloadSorter.Cli.Styling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class WatchCommand : BaseCommand<WatchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACTION]")]
        public string? Action { get; set; }

        [CommandArgument(1, "[PATH]")]
        public string? Path { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        return settings.Action?.ToLowerInvariant() switch
        {
            "add" => AddFolder(appSettings, settings.Path),
            "remove" or "rm" => RemoveFolder(appSettings, settings.Path),
            _ => ListFolders(appSettings)
        };
    }

    private int ListFolders(AppSettings appSettings)
    {
        AnsiConsole.Write(new Rule("[bold blue]Watch Folders[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]#[/]").Width(3).Centered());
        table.AddColumn(new TableColumn("[bold]Folder[/]"));
        table.AddColumn(new TableColumn("[bold]Status[/]").Width(10).Centered());
        table.AddColumn(new TableColumn("[bold]Files[/]").Width(8).RightAligned());

        var idx = 1;

        // Default inbox
        var inboxExists = Directory.Exists(appSettings.InboxPath);
        var inboxFiles = inboxExists ? Directory.GetFiles(appSettings.InboxPath).Length : 0;
        table.AddRow(
            "[dim]0[/]",
            $"[blue]IN[/] {Markup.Escape(appSettings.InboxPath)} [dim](default)[/]",
            inboxExists ? "[green]+[/]" : "[red]x[/]",
            inboxFiles > 0 ? $"[yellow]{inboxFiles}[/]" : "[dim]0[/]"
        );

        // Additional watch folders
        foreach (var folder in appSettings.WatchFolders)
        {
            var exists = Directory.Exists(folder);
            var fileCount = exists ? Directory.GetFiles(folder).Length : 0;

            table.AddRow(
                $"[dim]{idx}[/]",
                $"[blue]>[/] {Markup.Escape(folder)}",
                exists ? "[green]+[/]" : "[red]missing[/]",
                fileCount > 0 ? $"[yellow]{fileCount}[/]" : "[dim]0[/]"
            );
            idx++;
        }

        AnsiConsole.Write(table);

        if (appSettings.WatchFolders.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]No additional watch folders configured.[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Commands:[/]");
        AnsiConsole.MarkupLine("  [blue]sorter watch add <path>[/]     Add a folder to watch");
        AnsiConsole.MarkupLine("  [blue]sorter watch remove <#>[/]     Remove folder by number");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Example:[/]");
        AnsiConsole.MarkupLine("  [blue]sorter watch add \"C:\\Users\\Me\\Downloads\"[/]");

        return 0;
    }

    private int AddFolder(AppSettings appSettings, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            // Interactive mode
            path = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter folder path to watch:")
                    .Validate(p =>
                    {
                        if (string.IsNullOrWhiteSpace(p))
                            return ValidationResult.Error("Path cannot be empty");
                        if (!Directory.Exists(p))
                            return ValidationResult.Error($"Folder not found: {p}");
                        return ValidationResult.Success();
                    })
            );
        }

        // Normalize path
        path = Path.GetFullPath(path);

        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Folder not found:[/] {path}");
            return 1;
        }

        // Check for duplicates
        if (path.Equals(appSettings.InboxPath, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]This is already the default inbox folder.[/]");
            return 0;
        }

        if (appSettings.WatchFolders.Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.MarkupLine("[yellow]This folder is already being watched.[/]");
            return 0;
        }

        // Block RootPath itself (would cause infinite loops)
        if (path.Equals(appSettings.RootPath, StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[red]Cannot watch the root sorted folder itself.[/]");
            return 1;
        }

        // Block system directories
        var systemPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
        };

        foreach (var sysPath in systemPaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            if (path.Equals(sysPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(sysPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[red]Cannot watch system directories.[/]");
                return 1;
            }
        }

        // Block drive roots
        if (Path.GetPathRoot(path)?.Equals(path, StringComparison.OrdinalIgnoreCase) == true)
        {
            AnsiConsole.MarkupLine("[red]Cannot watch an entire drive. Choose a subfolder.[/]");
            return 1;
        }

        // Check if it's one of the category folders (would cause recursion)
        var categoryFolders = AppSettings.Folders.All
            .Select(f => Path.Combine(appSettings.RootPath, f))
            .ToList();

        foreach (var catFolder in categoryFolders)
        {
            // Block if path IS a category folder or is INSIDE one
            if (path.Equals(catFolder, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(catFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[red]Cannot watch a category folder or its subfolders.[/]");
                return 1;
            }
        }

        appSettings.WatchFolders.Add(path);
        appSettings.Save();

        var fileCount = Directory.GetFiles(path).Length;
        AnsiConsole.MarkupLine($"[green]+[/] Added: {Markup.Escape(path)}");
        if (fileCount > 0)
        {
            AnsiConsole.MarkupLine($"  [yellow]{fileCount}[/] files found. Run [blue]sorter sort[/] to process them.");
        }

        return 0;
    }

    private int RemoveFolder(AppSettings appSettings, string? indexOrPath)
    {
        if (string.IsNullOrEmpty(indexOrPath))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] sorter watch remove <number>");
            return 1;
        }

        // Try as index first
        if (int.TryParse(indexOrPath, out var index))
        {
            if (index == 0)
            {
                AnsiConsole.MarkupLine("[red]Cannot remove the default inbox folder.[/]");
                return 1;
            }

            index--; // Convert to 0-based
            if (index < 0 || index >= appSettings.WatchFolders.Count)
            {
                AnsiConsole.MarkupLine($"[red]Invalid index.[/] Must be 1-{appSettings.WatchFolders.Count}");
                return 1;
            }

            var folder = appSettings.WatchFolders[index];
            appSettings.WatchFolders.RemoveAt(index);
            appSettings.Save();

            AnsiConsole.MarkupLine($"[green]+[/] Removed: {Markup.Escape(folder)}");
            return 0;
        }

        // Try as path
        var path = Path.GetFullPath(indexOrPath);
        var matchIndex = appSettings.WatchFolders.FindIndex(f =>
            f.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (matchIndex < 0)
        {
            AnsiConsole.MarkupLine($"[red]Folder not found in watch list:[/] {path}");
            return 1;
        }

        appSettings.WatchFolders.RemoveAt(matchIndex);
        appSettings.Save();

        AnsiConsole.MarkupLine($"[green]+[/] Removed: {Markup.Escape(path)}");
        return 0;
    }
}

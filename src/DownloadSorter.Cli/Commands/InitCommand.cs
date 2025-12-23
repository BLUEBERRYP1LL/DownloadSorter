using DownloadSorter.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class InitCommand : Command<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        public string? Path { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[bold blue]DownloadSorter Setup[/]\n");

        var appSettings = AppSettings.Load();

        // Determine root path
        string rootPath;

        if (!string.IsNullOrEmpty(settings.Path))
        {
            rootPath = Path.GetFullPath(settings.Path);
        }
        else if (!string.IsNullOrEmpty(appSettings.RootPath))
        {
            if (!AnsiConsole.Confirm($"Already configured at [blue]{appSettings.RootPath}[/]. Reconfigure?", false))
            {
                return 0;
            }
            rootPath = PromptForPath();
        }
        else
        {
            rootPath = PromptForPath();
        }

        // Validate path
        if (!Directory.Exists(Path.GetDirectoryName(rootPath) ?? rootPath))
        {
            AnsiConsole.MarkupLine($"[red]Parent directory doesn't exist:[/] {rootPath}");
            return 1;
        }

        // Show what will be created
        AnsiConsole.MarkupLine($"\n[bold]Will create folder structure at:[/] [blue]{rootPath}[/]\n");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Folder");
        table.AddColumn("Purpose");

        table.AddRow("[yellow]00_INBOX[/]", "New downloads land here");
        table.AddRow("00_PINNED", "Protected files (never auto-sorted)");
        table.AddRow("10_Documents", "PDFs, Office docs, text files");
        table.AddRow("20_Executables", "EXE, MSI installers");
        table.AddRow("30_Archives", "ZIP, RAR, 7z archives");
        table.AddRow("40_Media", "Images, videos, audio");
        table.AddRow("50_Code", "Source code, config files");
        table.AddRow("60_ISOs", "Disk images");
        table.AddRow("80_Big_Files", $"Files over {FormatSize(appSettings.BigFileThreshold)}");
        table.AddRow("_UNSORTED", "Unknown file types");

        AnsiConsole.Write(table);

        if (!AnsiConsole.Confirm("\nCreate these folders?", true))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        // Create folders
        appSettings.RootPath = rootPath;

        AnsiConsole.Status()
            .Start("Creating folders...", ctx =>
            {
                appSettings.CreateFolderStructure();
                Thread.Sleep(500); // Brief pause so user sees it working
            });

        // Save config
        appSettings.Save();

        AnsiConsole.MarkupLine("\n[green]✓[/] Folder structure created");
        AnsiConsole.MarkupLine($"[green]✓[/] Config saved to [dim]{AppSettings.DefaultConfigPath}[/]");
        AnsiConsole.MarkupLine($"\n[bold]Inbox path:[/] [blue]{appSettings.InboxPath}[/]");
        AnsiConsole.MarkupLine("\nSet your browser's download folder to the INBOX path above.");
        AnsiConsole.MarkupLine("Then run [blue]sorter status[/] to check everything is working.\n");

        return 0;
    }

    private static string PromptForPath()
    {
        var downloadsPath = GetDefaultDownloadsPath();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Where should sorted folders live?")
                .AddChoices(
                    $"Inside Downloads ({downloadsPath})",
                    "Custom path"
                ));

        if (choice.StartsWith("Inside"))
        {
            return downloadsPath;
        }

        return AnsiConsole.Prompt(
            new TextPrompt<string>("Enter full path:")
                .DefaultValue(downloadsPath)
                .Validate(path =>
                {
                    var parent = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                    {
                        return ValidationResult.Error("Parent directory must exist");
                    }
                    return ValidationResult.Success();
                }));
    }

    private static string GetDefaultDownloadsPath()
    {
        // Try to get Windows Downloads folder
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(userProfile, "Downloads");

        if (Directory.Exists(downloads))
        {
            return downloads;
        }

        // Fallback
        return userProfile;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.#} GB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.#} MB";
        return $"{bytes / 1024.0:0.#} KB";
    }
}

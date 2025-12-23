using DownloadSorter.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace DownloadSorter.Cli.Commands;

public class ImportCommand : BaseCommand<ImportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Path to scan for files (defaults to Downloads folder)")]
        public string? SourcePath { get; set; }

        [CommandOption("-n|--dry-run")]
        [Description("Show what would be imported without moving files")]
        public bool DryRun { get; set; }

        [CommandOption("-r|--recursive")]
        [Description("Scan subfolders recursively")]
        public bool Recursive { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        // Determine source path
        var sourcePath = settings.SourcePath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            // Default to user's Downloads folder
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            sourcePath = Path.Combine(userProfile, "Downloads");
        }

        if (!Directory.Exists(sourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Path doesn't exist:[/] {sourcePath}");
            return 1;
        }

        AnsiConsole.MarkupLine($"Scanning [blue]{sourcePath}[/]...\n");

        // Get folders to exclude (our managed folders)
        var excludedFolders = GetExcludedFolders(appSettings.RootPath);

        // Find files to import
        var searchOption = settings.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(sourcePath, "*", searchOption))
            {
                // Skip files in excluded folders
                var fileDir = Path.GetDirectoryName(file) ?? "";
                if (excludedFolders.Any(ex => fileDir.StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Skip temp/ignored files
                if (appSettings.ShouldIgnore(file))
                    continue;

                files.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Some folders couldn't be accessed.");
        }

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓[/] No files to import.");
            return 0;
        }

        AnsiConsole.MarkupLine($"Found [blue]{files.Count}[/] files to import.\n");

        if (settings.DryRun)
        {
            ShowDryRun(files, appSettings);
            return 0;
        }

        // Confirm before importing
        if (!AnsiConsole.Confirm($"Import {files.Count} files into the sorter system?"))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        // Import files
        return ImportFiles(files, appSettings);
    }

    private void ShowDryRun(List<string> files, Core.Configuration.AppSettings appSettings)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("File");
        table.AddColumn("Size");
        table.AddColumn("Would Move To");

        foreach (var file in files.Take(50)) // Limit display
        {
            var info = new FileInfo(file);
            var dest = appSettings.GetDestinationFolder(info.Extension, info.Length);
            var category = Path.GetFileName(dest);

            table.AddRow(
                $"[white]{TruncateName(info.Name, 40)}[/]",
                FormatSize(info.Length),
                $"[blue]{category}[/]"
            );
        }

        AnsiConsole.Write(table);

        if (files.Count > 50)
        {
            AnsiConsole.MarkupLine($"\n[dim]...and {files.Count - 50} more files[/]");
        }

        AnsiConsole.MarkupLine("\n[yellow]Dry run - no files moved.[/]");
    }

    private int ImportFiles(List<string> files, Core.Configuration.AppSettings appSettings)
    {
        using var repo = CreateRepository(appSettings);
        var sorter = new FileSorterService(appSettings, repo);

        var success = 0;
        var failed = 0;
        var skipped = 0;

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .Start(ctx =>
            {
                var task = ctx.AddTask($"Importing {files.Count} files", maxValue: files.Count);

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    task.Description = $"[dim]{TruncateName(fileName, 30)}[/]";

                    try
                    {
                        var result = sorter.SortFile(file);

                        if (result.Success)
                        {
                            success++;
                        }
                        else if (result.Skipped)
                        {
                            skipped++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch
                    {
                        failed++;
                    }

                    task.Increment(1);
                }

                task.Description = "Done";
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Imported:[/] {success}  [yellow]Skipped:[/] {skipped}  [red]Failed:[/] {failed}");

        return failed > 0 ? 1 : 0;
    }

    private static HashSet<string> GetExcludedFolders(string rootPath)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Exclude all subfolders of the root path (our managed folders)
        if (Directory.Exists(rootPath))
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                excluded.Add(dir);
            }
        }

        return excluded;
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name[..(maxLength - 3)] + "...";
    }
}

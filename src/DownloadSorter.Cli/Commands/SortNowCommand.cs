using System.ComponentModel;
using DownloadSorter.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class SortNowCommand : BaseCommand<SortNowCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--dry-run")]
        public bool DryRun { get; set; }

        [CommandOption("-l|--loop")]
        [Description("Continuously monitor and sort files")]
        public bool Loop { get; set; }

        [CommandOption("-i|--interval <SECONDS>")]
        [Description("Seconds between sort cycles (default: 30)")]
        public int Interval { get; set; } = 30;

        [CommandOption("-f|--from <PATH>")]
        [Description("Sort from specific folder instead of configured watch folders")]
        public string? FromPath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        // Determine source folders
        var sourceFolders = GetSourceFolders(appSettings, settings);
        if (sourceFolders.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No valid source folders found.[/]");
            return 1;
        }

        if (settings.Loop)
        {
            return RunLoopMode(appSettings, settings, sourceFolders);
        }

        return RunOnce(appSettings, settings, sourceFolders);
    }

    private List<string> GetSourceFolders(Core.Configuration.AppSettings appSettings, Settings settings)
    {
        // If --from specified, use only that folder
        if (!string.IsNullOrEmpty(settings.FromPath))
        {
            try
            {
                var path = Path.GetFullPath(settings.FromPath);
                if (Directory.Exists(path))
                {
                    return [path];
                }
                AnsiConsole.MarkupLine($"[red]Folder not found:[/] {path}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Invalid path:[/] {ex.Message}");
            }
            return [];
        }

        // Otherwise use all configured watch folders
        return appSettings.AllWatchFolders.ToList();
    }

    private int RunLoopMode(Core.Configuration.AppSettings appSettings, Settings settings, List<string> sourceFolders)
    {
        var cts = new CancellationTokenSource();
        var totalSorted = 0;
        var totalFailed = 0;
        var startTime = DateTime.Now;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        AnsiConsole.Clear();
        RenderLoopHeader(sourceFolders, settings.Interval);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var (sorted, failed) = SortFromFolders(appSettings, sourceFolders, showProgress: false);
                totalSorted += sorted;
                totalFailed += failed;

                RenderLoopStatus(totalSorted, totalFailed, startTime, settings.Interval, sourceFolders.Count);

                try
                {
                    Task.Delay(settings.Interval * 1000, cts.Token).Wait(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Loop stopped.[/]");
        AnsiConsole.MarkupLine($"Session: [green]{totalSorted}[/] sorted, [red]{totalFailed}[/] failed");

        return totalFailed > 0 ? 1 : 0;
    }

    private void RenderLoopHeader(List<string> sourceFolders, int interval)
    {
        var watchList = string.Join("\n", sourceFolders.Select(f => $"  [blue]>[/] {f}"));

        var header = new Panel(
            new Markup($"[bold]Watching {sourceFolders.Count} folder(s):[/]\n{watchList}\n\n" +
                       $"[bold]Interval:[/] {interval}s\n" +
                       $"[dim]Press Ctrl+C to stop[/]"))
            .Header("[bold blue]DownloadSorter Loop Mode[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);

        AnsiConsole.Write(header);
        AnsiConsole.WriteLine();
    }

    private void RenderLoopStatus(int sorted, int failed, DateTime startTime, int interval, int folderCount)
    {
        var elapsed = DateTime.Now - startTime;
        var elapsedStr = elapsed.TotalHours >= 1
            ? $"{elapsed:hh\\:mm\\:ss}"
            : $"{elapsed:mm\\:ss}";

        // Move cursor to status line (after header)
        var headerLines = 5 + folderCount;
        AnsiConsole.Cursor.SetPosition(0, headerLines + 2);

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("")
            .AddColumn("");

        table.AddRow("[bold]Status[/]", $"[green]*[/] Running");
        table.AddRow("Uptime", $"[blue]{elapsedStr}[/]");
        table.AddRow("Sorted", $"[green]{sorted}[/]");
        table.AddRow("Failed", failed > 0 ? $"[red]{failed}[/]" : "[dim]0[/]");
        table.AddRow("Last check", $"[dim]{DateTime.Now:HH:mm:ss}[/]");
        table.AddRow("Next check", $"[dim]{DateTime.Now.AddSeconds(interval):HH:mm:ss}[/]");

        AnsiConsole.Write(table);
    }

    private int RunOnce(Core.Configuration.AppSettings appSettings, Settings settings, List<string> sourceFolders)
    {
        // Get all files to sort
        var allFiles = new List<(string folder, string file)>();
        foreach (var folder in sourceFolders)
        {
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder)
                    .Where(f => !appSettings.ShouldIgnore(f));
                foreach (var file in files)
                {
                    allFiles.Add((folder, file));
                }
            }
        }

        if (allFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]+[/] All folders empty. Nothing to sort.");
            return 0;
        }

        // Show source breakdown
        if (sourceFolders.Count > 1)
        {
            AnsiConsole.MarkupLine($"Found [blue]{allFiles.Count}[/] files across {sourceFolders.Count} folders:\n");
            foreach (var folder in sourceFolders)
            {
                var count = allFiles.Count(f => f.folder == folder);
                if (count > 0)
                {
                    var shortPath = folder.Length > 50 ? "..." + folder[^47..] : folder;
                    AnsiConsole.MarkupLine($"  [blue]>[/] {Markup.Escape(shortPath)}: [blue]{count}[/] files");
                }
            }
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine($"Found [blue]{allFiles.Count}[/] files to sort.\n");
        }

        if (settings.DryRun)
        {
            return ShowDryRun(appSettings, allFiles.Select(f => f.file).ToList());
        }

        var (success, failed) = SortFromFolders(appSettings, sourceFolders, showProgress: true);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]+ Sorted:[/] {success}  [red]x Failed:[/] {failed}");

        return failed > 0 ? 1 : 0;
    }

    private (int sorted, int failed) SortFromFolders(Core.Configuration.AppSettings appSettings, List<string> folders, bool showProgress)
    {
        using var repo = CreateRepository(appSettings);
        var sorter = new FileSorterService(appSettings, repo);

        var success = 0;
        var failed = 0;

        if (showProgress)
        {
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .Start("Sorting files...", ctx =>
                {
                    foreach (var folder in folders)
                    {
                        ProcessResults(sorter.SortFromFolder(folder), ref success, ref failed, showProgress);
                    }
                });
        }
        else
        {
            foreach (var folder in folders)
            {
                ProcessResults(sorter.SortFromFolder(folder), ref success, ref failed, showProgress);
            }
        }

        return (success, failed);
    }

    private void ProcessResults(IEnumerable<SortResult> results, ref int success, ref int failed, bool showProgress)
    {
        foreach (var result in results)
        {
            if (result.Success)
            {
                success++;
                if (showProgress)
                {
                    var fileName = Path.GetFileName(result.DestPath) ?? "unknown";
                    var icon = GetCategoryIcon(result.Category ?? "_UNSORTED");
                    AnsiConsole.MarkupLine($"[green]+[/] {icon} {Markup.Escape(fileName)} -> [blue]{result.Category}[/]");
                }
            }
            else if (!result.Skipped)
            {
                failed++;
                if (showProgress)
                {
                    AnsiConsole.MarkupLine($"[red]x[/] {Markup.Escape(result.Error ?? "Unknown error")}");
                }
            }
        }
    }

    private int ShowDryRun(Core.Configuration.AppSettings appSettings, List<string> files)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn(new TableColumn("[bold]File[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Size[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Destination[/]"));

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var dest = appSettings.GetDestinationFolder(info.Extension, info.Length);
            var category = Path.GetFileName(dest);
            var icon = GetCategoryIcon(category);

            table.AddRow(
                $"[white]{Markup.Escape(info.Name)}[/]",
                $"[dim]{FormatSize(info.Length)}[/]",
                $"{icon} [blue]{category}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Dry run - no files moved.[/]");
        return 0;
    }

    private static string GetCategoryIcon(string category) => category switch
    {
        "10_Documents" => "[blue]DOC[/]",
        "20_Executables" => "[red]EXE[/]",
        "30_Archives" => "[green]ZIP[/]",
        "40_Media" => "[fuchsia]MED[/]",
        "50_Code" => "[yellow]COD[/]",
        "60_ISOs" => "[purple]ISO[/]",
        "80_Big_Files" => "[orange1]BIG[/]",
        "_UNSORTED" => "[grey]???[/]",
        _ => "[blue]>[/]"
    };
}

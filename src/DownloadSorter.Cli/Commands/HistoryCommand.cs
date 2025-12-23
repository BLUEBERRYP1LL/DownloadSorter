using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class HistoryCommand : BaseCommand<HistoryCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-n|--count <COUNT>")]
        public int Count { get; set; } = 20;

        [CommandOption("-c|--category <CATEGORY>")]
        public string? Category { get; set; }

        [CommandOption("--today")]
        public bool Today { get; set; }
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

        var records = settings.Today
            ? repo.GetByDateRange(DateTime.Today, DateTime.Today.AddDays(1), settings.Count)
            : settings.Category != null
                ? repo.GetByCategory(settings.Category, settings.Count)
                : repo.GetRecent(settings.Count);

        if (records.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No history found.[/]");
            return 0;
        }

        // Build title
        var title = settings.Today
            ? "Today's Sorted Files"
            : settings.Category != null
                ? $"Files in {settings.Category}"
                : "Recently Sorted Files";

        AnsiConsole.Write(new Rule($"[bold blue]{title}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.Rounded).Expand();
        table.AddColumn("Time");
        table.AddColumn("File");
        table.AddColumn("Size");
        table.AddColumn("Category");
        table.AddColumn("Status");

        foreach (var record in records)
        {
            var statusMarkup = record.Status switch
            {
                Core.Data.SortStatus.Success => "[green]✓[/]",
                Core.Data.SortStatus.Skipped => "[yellow]⊘[/]",
                Core.Data.SortStatus.Failed => "[red]✗[/]",
                _ => "?"
            };

            var nameDisplay = record.OriginalName == record.FinalName
                ? record.FinalName
                : $"{record.OriginalName} → {record.FinalName}";

            // Truncate long names
            if (nameDisplay.Length > 40)
            {
                nameDisplay = nameDisplay[..37] + "...";
            }

            var categoryDisplay = record.Category
                .Replace("_", " ")
                .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

            table.AddRow(
                $"[dim]{FormatTimeAgo(record.SortedAt)}[/]",
                nameDisplay,
                $"[dim]{FormatSize(record.FileSize)}[/]",
                $"[blue]{categoryDisplay}[/]",
                statusMarkup
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Showing {records.Count} records. Use -n to show more.[/]");

        return 0;
    }
}

using DownloadSorter.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class StatusCommand : BaseCommand<StatusCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        // Header
        AnsiConsole.Write(new Rule("[bold blue]DownloadSorter Status[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Configuration
        var configTable = new Table().Border(TableBorder.Rounded).Expand();
        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");

        configTable.AddRow("Root Path", $"[blue]{appSettings.RootPath}[/]");
        configTable.AddRow("Inbox", $"[blue]{appSettings.InboxPath}[/]");
        configTable.AddRow("Settle Time", $"{appSettings.SettleTimeSeconds} seconds");
        configTable.AddRow("Big File Threshold", FormatSize(appSettings.BigFileThreshold));
        configTable.AddRow("Database", $"[dim]{appSettings.DatabasePath}[/]");

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        // Inbox status
        var inboxExists = Directory.Exists(appSettings.InboxPath);
        var pendingCount = 0;
        var pendingSize = 0L;

        if (inboxExists)
        {
            var files = Directory.GetFiles(appSettings.InboxPath)
                .Where(f => !appSettings.ShouldIgnore(f))
                .ToList();

            pendingCount = files.Count;
            pendingSize = files.Sum(f => new FileInfo(f).Length);
        }

        var statusColor = pendingCount == 0 ? "green" : (pendingCount < 10 ? "yellow" : "red");

        AnsiConsole.MarkupLine($"[bold]Inbox Status:[/] [{statusColor}]{pendingCount} files[/] ({FormatSize(pendingSize)})");

        if (!inboxExists)
        {
            AnsiConsole.MarkupLine("[red]! Inbox folder doesn't exist![/]");
        }

        // Today's stats
        AnsiConsole.WriteLine();

        using var repo = CreateRepository(appSettings);
        var stats = repo.GetTodayStats();

        AnsiConsole.Write(new Rule("[bold]Today's Activity[/]").LeftJustified());

        var statsTable = new Table().Border(TableBorder.Simple);
        statsTable.AddColumn("Metric");
        statsTable.AddColumn("Count");

        statsTable.AddRow("Files Sorted", $"[green]{stats.Success}[/]");
        statsTable.AddRow("Skipped", $"[yellow]{stats.Skipped}[/]");
        statsTable.AddRow("Failed", stats.Failed > 0 ? $"[red]{stats.Failed}[/]" : "0");
        statsTable.AddRow("Biggest File", FormatSize(stats.BiggestFileSize));

        AnsiConsole.Write(statsTable);

        // Category breakdown
        var categories = repo.GetCategoryCounts(DateTime.Today);

        if (categories.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold]Categories Today[/]").LeftJustified());

            var chart = new BarChart()
                .Width(60)
                .Label("[bold]Files by category[/]");

            var colors = new[] { Color.Blue, Color.Green, Color.Yellow, Color.Fuchsia, Color.Aqua };
            var i = 0;

            foreach (var (category, count) in categories.OrderByDescending(c => c.Value).Take(6))
            {
                var displayName = category.Replace("_", " ").TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                chart.AddItem(displayName, count, colors[i % colors.Length]);
                i++;
            }

            AnsiConsole.Write(chart);
        }

        AnsiConsole.WriteLine();
        return 0;
    }
}

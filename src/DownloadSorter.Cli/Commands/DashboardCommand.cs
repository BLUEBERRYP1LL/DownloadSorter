using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Data;
using DownloadSorter.Core.Services;
using DownloadSorter.Cli.Interactive;
using DownloadSorter.Cli.Styling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class DashboardCommand : BaseCommand<DashboardCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-r|--refresh <SECONDS>")]
        public int RefreshSeconds { get; set; } = 3;
    }

    private static readonly string[] TabNames = ["Overview", "Activity", "Categories"];

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        var cts = new CancellationTokenSource();
        var state = new InteractiveState(TabNames.Length);
        var keyboard = new KeyboardHandler();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        keyboard.KeyPressed += key =>
        {
            lock (state)
            {
                state.HandleKey(key);
            }
        };

        AnsiConsole.Clear();
        Console.CursorVisible = false;

        try
        {
            keyboard.Start(cts.Token);
            RunDashboardLoop(appSettings, settings, state, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }
        finally
        {
            keyboard.Stop();
            Console.CursorVisible = true;
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[dim]Dashboard closed.[/]");
        return 0;
    }

    private void RunDashboardLoop(AppSettings appSettings, Settings settings, InteractiveState state, CancellationToken ct)
    {
        var lastRender = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            var snapshot = state.GetSnapshot();

            if (snapshot.ShouldExit)
                break;

            // Handle sort trigger
            if (snapshot.TriggerSort)
            {
                TriggerSort(appSettings);
            }

            // Render if needed
            var shouldRender = snapshot.NeedsRefresh ||
                               (DateTime.Now - lastRender).TotalSeconds >= settings.RefreshSeconds;

            if (shouldRender)
            {
                RenderDashboard(appSettings, snapshot);
                lastRender = DateTime.Now;
                state.ClearRefresh();
            }

            try
            {
                Task.Delay(100, ct).Wait(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void TriggerSort(AppSettings appSettings)
    {
        using var repo = CreateRepository(appSettings);
        var sorter = new FileSorterService(appSettings, repo);
        sorter.SortAllPending();
    }

    private void RenderDashboard(AppSettings appSettings, InteractiveStateSnapshot snapshot)
    {
        AnsiConsole.Cursor.SetPosition(0, 0);

        using var repo = CreateRepository(appSettings);
        var stats = repo.GetTodayStats();
        var categories = repo.GetCategoryCounts(DateTime.Today);
        var recent = repo.GetRecent(15);
        var pending = GetPendingFiles(appSettings);

        // Header
        RenderHeader();

        // Tab bar
        RenderTabBar(snapshot.ActiveTab);

        // Content area based on active tab
        switch (snapshot.ActiveTab)
        {
            case 0:
                RenderOverviewTab(appSettings, stats, pending, categories);
                break;
            case 1:
                RenderActivityTab(recent, snapshot.SelectedIndex);
                break;
            case 2:
                RenderCategoriesTab(appSettings, categories);
                break;
        }

        // Help overlay or status bar
        if (snapshot.ShowHelp)
        {
            RenderHelpOverlay();
        }
        else
        {
            RenderStatusBar();
        }
    }

    private static void RenderHeader()
    {
        var title = new Rule("[bold blue]📁 DownloadSorter Dashboard[/]")
            .LeftJustified()
            .RuleStyle(Style.Parse("blue"));
        AnsiConsole.Write(title);
    }

    private static void RenderTabBar(int activeTab)
    {
        var tabBar = Theme.TabBar(activeTab, TabNames);
        AnsiConsole.MarkupLine($"\n  {tabBar}\n");
    }

    private void RenderOverviewTab(AppSettings appSettings, DailyStats stats,
        List<(string Name, long Size)> pending, Dictionary<string, int> categories)
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Top").SplitColumns(
                    new Layout("Stats").Size(30),
                    new Layout("Inbox")
                ),
                new Layout("Bottom")
            );

        // Stats panel
        var statsTable = new Table().Border(TableBorder.None).HideHeaders();
        statsTable.AddColumn("");
        statsTable.AddColumn(new TableColumn("").RightAligned());

        statsTable.AddRow("[bold]📊 Today's Stats[/]", "");
        statsTable.AddRow("", "");
        statsTable.AddRow($"  {Theme.IconSuccess} Sorted", $"[green]{stats.Success}[/]");
        statsTable.AddRow($"  {Theme.IconWarning} Skipped", $"[yellow]{stats.Skipped}[/]");
        statsTable.AddRow($"  {Theme.IconFailed} Failed", stats.Failed > 0 ? $"[red]{stats.Failed}[/]" : "[dim]0[/]");
        statsTable.AddRow("", "");
        statsTable.AddRow("  📀 Biggest", FormatSize(stats.BiggestFileSize));
        statsTable.AddRow("", "");
        statsTable.AddRow("[bold]⚙️ Settings[/]", "");
        statsTable.AddRow("", "");
        statsTable.AddRow("  Settle Time", $"[dim]{appSettings.SettleTimeSeconds}s[/]");
        statsTable.AddRow("  Big File", $"[dim]>{FormatSize(appSettings.BigFileThreshold)}[/]");

        layout["Stats"].Update(new Panel(statsTable).Border(BoxBorder.Rounded).BorderColor(Color.Blue));

        // Inbox panel
        var inboxContent = new Table().Border(TableBorder.None).HideHeaders();
        inboxContent.AddColumn("");
        inboxContent.AddColumn(new TableColumn("").RightAligned());

        if (pending.Count > 0)
        {
            inboxContent.AddRow($"[yellow bold]📥 {pending.Count} files waiting[/]", "");
            inboxContent.AddRow("", "");
            foreach (var (name, size) in pending.Take(8))
            {
                var displayName = name.Length > 35 ? name[..32] + "..." : name;
                inboxContent.AddRow($"  {Markup.Escape(displayName)}", $"[dim]{FormatSize(size)}[/]");
            }
            if (pending.Count > 8)
            {
                inboxContent.AddRow($"  [dim]... and {pending.Count - 8} more[/]", "");
            }
        }
        else
        {
            inboxContent.AddRow($"[green]{Theme.IconSuccess} Inbox empty[/]", "");
            inboxContent.AddRow("[dim]All files sorted![/]", "");
        }

        layout["Inbox"].Update(new Panel(inboxContent)
            .Header("[blue]Inbox[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(pending.Count > 0 ? Color.Yellow : Color.Green));

        // Categories summary
        var catTable = new Table().Border(TableBorder.None).HideHeaders();
        catTable.AddColumn("");
        catTable.AddColumn(new TableColumn("").Width(8).RightAligned());
        catTable.AddColumn(new TableColumn("").Width(20));

        var maxCount = categories.Count > 0 ? categories.Values.Max() : 1;
        foreach (var (cat, count) in categories.OrderByDescending(c => c.Value).Take(6))
        {
            var icon = Theme.GetCategoryIcon(cat);
            var name = Theme.FormatCategoryName(cat);
            var color = Theme.GetCategoryColor(cat);
            var barLen = (int)((double)count / maxCount * 15);
            var bar = new string('█', barLen);

            catTable.AddRow($"{icon} [{color}]{name}[/]", $"[{color}]{count}[/]", $"[{color}]{bar}[/]");
        }

        if (categories.Count == 0)
        {
            catTable.AddRow("[dim]No activity today[/]", "", "");
        }

        layout["Bottom"].Update(new Panel(catTable)
            .Header("[blue]Categories Today[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand());

        AnsiConsole.Write(layout);
        ClearRemaining();
    }

    private void RenderActivityTab(List<FileRecord> recent, int selectedIndex)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Time[/]").Width(12));
        table.AddColumn(new TableColumn("[bold]File[/]"));
        table.AddColumn(new TableColumn("[bold]Size[/]").Width(10).RightAligned());
        table.AddColumn(new TableColumn("[bold]Category[/]").Width(15));
        table.AddColumn(new TableColumn("[bold]Status[/]").Width(6).Centered());

        if (recent.Count == 0)
        {
            AnsiConsole.MarkupLine("\n  [dim]No recent activity[/]\n");
            ClearRemaining();
            return;
        }

        var idx = 0;
        foreach (var record in recent)
        {
            var isSelected = idx == selectedIndex;
            var prefix = isSelected ? "[on blue]" : "";
            var suffix = isSelected ? "[/]" : "";

            var statusIcon = record.Status switch
            {
                SortStatus.Success => Theme.IconSuccess,
                SortStatus.Skipped => Theme.IconWarning,
                SortStatus.Failed => Theme.IconFailed,
                _ => "?"
            };

            var icon = Theme.GetCategoryIcon(record.Category);
            var catName = Theme.FormatCategoryName(record.Category);
            var color = Theme.GetCategoryColor(record.Category);

            var name = record.FinalName.Length > 40
                ? record.FinalName[..37] + "..."
                : record.FinalName;

            table.AddRow(
                $"{prefix}[dim]{FormatTimeAgo(record.SortedAt)}[/]{suffix}",
                $"{prefix}{Markup.Escape(name)}{suffix}",
                $"{prefix}[dim]{FormatSize(record.FileSize)}[/]{suffix}",
                $"{prefix}{icon} [{color}]{catName}[/]{suffix}",
                $"{prefix}{statusIcon}{suffix}"
            );
            idx++;
        }

        AnsiConsole.Write(table);
        ClearRemaining();
    }

    private void RenderCategoriesTab(AppSettings appSettings, Dictionary<string, int> todayCounts)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Category[/]"));
        table.AddColumn(new TableColumn("[bold]Today[/]").Width(8).RightAligned());
        table.AddColumn(new TableColumn("[bold]Extensions[/]"));

        foreach (var rule in appSettings.Categories)
        {
            var icon = Theme.GetCategoryIcon(rule.Category);
            var name = Theme.FormatCategoryName(rule.Category);
            var color = Theme.GetCategoryColor(rule.Category);
            var count = todayCounts.GetValueOrDefault(rule.Category, 0);
            var countStr = count > 0 ? $"[{color}]{count}[/]" : "[dim]0[/]";

            var exts = string.Join(", ", rule.Extensions.Take(8));
            if (rule.Extensions.Length > 8)
            {
                exts += $" +{rule.Extensions.Length - 8} more";
            }

            table.AddRow(
                $"{icon} [{color}]{name}[/]",
                countStr,
                $"[dim]{exts}[/]"
            );
        }

        // Add special folders
        table.AddRow("📀 [orange1]Big Files[/]",
            todayCounts.GetValueOrDefault("80_Big_Files", 0) > 0
                ? $"[orange1]{todayCounts["80_Big_Files"]}[/]"
                : "[dim]0[/]",
            $"[dim]Files > {FormatSize(appSettings.BigFileThreshold)}[/]");

        table.AddRow("❓ [grey]Unsorted[/]",
            todayCounts.GetValueOrDefault("_UNSORTED", 0) > 0
                ? $"[grey]{todayCounts["_UNSORTED"]}[/]"
                : "[dim]0[/]",
            "[dim]Unknown extensions[/]");

        AnsiConsole.Write(table);
        ClearRemaining();
    }

    private static void RenderHelpOverlay()
    {
        AnsiConsole.WriteLine();
        var helpPanel = new Panel(
            "[bold]Keyboard Shortcuts[/]\n\n" +
            "  [blue]1/2/3[/]     Switch tabs\n" +
            "  [blue]Tab[/]       Next tab\n" +
            "  [blue]↑/↓[/]       Navigate (Activity tab)\n" +
            "  [blue]s[/]         Sort now\n" +
            "  [blue]r[/]         Refresh\n" +
            "  [blue]?/h[/]       Toggle help\n" +
            "  [blue]q/Esc[/]     Quit\n")
            .Header("[bold blue]Help[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);

        AnsiConsole.Write(helpPanel);
    }

    private static void RenderStatusBar()
    {
        AnsiConsole.WriteLine();
        var hotkeys = Theme.HotkeyBar(
            ("1-3", "tabs"),
            ("s", "sort"),
            ("r", "refresh"),
            ("?", "help"),
            ("q", "quit")
        );
        AnsiConsole.MarkupLine($"  {hotkeys}");
        AnsiConsole.MarkupLine($"\n  [dim]Last updated: {DateTime.Now:HH:mm:ss}[/]");
    }

    private static void ClearRemaining()
    {
        // Clear any remaining lines from previous render
        for (var i = 0; i < 5; i++)
        {
            AnsiConsole.WriteLine(new string(' ', Console.WindowWidth - 1));
        }
    }

    private static List<(string Name, long Size)> GetPendingFiles(AppSettings settings)
    {
        var allFiles = new List<FileInfo>();

        // Gather files from all watch folders
        foreach (var folder in settings.AllWatchFolders)
        {
            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder)
                    .Where(f => !settings.ShouldIgnore(f))
                    .Select(f => new FileInfo(f));
                allFiles.AddRange(files);
            }
        }

        // Sort by most recent and take top 15
        return allFiles
            .OrderByDescending(f => f.LastWriteTime)
            .Take(15)
            .Select(f => (f.Name, f.Length))
            .ToList();
    }
}

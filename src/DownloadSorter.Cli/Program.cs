using DownloadSorter.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("sorter");
    config.SetApplicationVersion("1.0.0");

    // Core commands
    config.AddCommand<InitCommand>("init")
        .WithDescription("Initialize folder structure (first-time setup)");

    config.AddCommand<StatusCommand>("status")
        .WithDescription("Show current sorter status and stats");

    config.AddCommand<SortNowCommand>("sort")
        .WithDescription("Sort files now. Use --loop for continuous monitoring");

    // Interactive commands
    config.AddCommand<DashboardCommand>("dashboard")
        .WithAlias("dash")
        .WithDescription("Live dashboard with tabs, stats and keyboard navigation");

    config.AddCommand<BrowseCommand>("browse")
        .WithAlias("files")
        .WithDescription("Interactive file browser with move/pin actions");

    // History and search
    config.AddCommand<HistoryCommand>("history")
        .WithDescription("Show recently sorted files");

    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search file history by name");

    // Configuration
    config.AddCommand<RulesCommand>("rules")
        .WithDescription("View and edit category routing rules");

    config.AddCommand<ConfigCommand>("config")
        .WithDescription("View and edit settings");

    config.AddCommand<WatchCommand>("watch")
        .WithDescription("Manage folders to watch for sorting");

    // Import
    config.AddCommand<ImportCommand>("import")
        .WithDescription("Import existing files from a folder");
});

return app.Run(args);

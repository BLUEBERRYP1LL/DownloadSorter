using DownloadSorter.Core.Configuration;
using DownloadSorter.Cli.Styling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class ConfigCommand : BaseCommand<ConfigCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACTION]")]
        public string? Action { get; set; }

        [CommandArgument(1, "[KEY]")]
        public string? Key { get; set; }

        [CommandArgument(2, "[VALUE]")]
        public string? Value { get; set; }
    }

    private static readonly Dictionary<string, (string description, string example)> ConfigKeys = new()
    {
        ["SettleTimeSeconds"] = ("Seconds file must be unchanged before sorting", "180"),
        ["BigFileThreshold"] = ("Files larger than this (bytes) go to Big_Files", "1073741824"),
        ["EnableBigFileRouting"] = ("Enable big file routing", "true"),
        ["IgnoreExtensions"] = ("Extensions to ignore (comma-separated)", ".crdownload,.part,.tmp"),
        ["ShowNotifications"] = ("Show balloon notifications (tray only)", "true")
    };

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
            "set" => SetConfig(appSettings, settings.Key, settings.Value),
            "get" => GetConfig(appSettings, settings.Key),
            "reset" => ResetConfig(appSettings),
            _ => ShowConfig(appSettings)
        };
    }

    private int ShowConfig(AppSettings appSettings)
    {
        AnsiConsole.Write(new Rule("[bold blue]⚙️ Configuration[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]Setting[/]"));
        table.AddColumn(new TableColumn("[bold]Value[/]"));
        table.AddColumn(new TableColumn("[bold]Description[/]"));

        // Paths
        table.AddRow(
            "[blue]RootPath[/]",
            $"[white]{Markup.Escape(appSettings.RootPath)}[/]",
            "[dim]Base folder for sorted files[/]"
        );

        table.AddRow(
            "[blue]InboxPath[/]",
            $"[white]{Markup.Escape(appSettings.InboxPath)}[/]",
            "[dim]Watch folder (read-only)[/]"
        );

        table.AddRow(
            "[blue]DatabasePath[/]",
            $"[dim]{Markup.Escape(appSettings.DatabasePath)}[/]",
            "[dim]SQLite database location[/]"
        );

        table.AddEmptyRow();

        // Editable settings
        table.AddRow(
            "[cyan]SettleTimeSeconds[/]",
            $"[green]{appSettings.SettleTimeSeconds}[/]",
            "[dim]Seconds file must be unchanged[/]"
        );

        table.AddRow(
            "[cyan]BigFileThreshold[/]",
            $"[green]{FormatSize(appSettings.BigFileThreshold)}[/] [dim]({appSettings.BigFileThreshold})[/]",
            "[dim]Big file size threshold[/]"
        );

        table.AddRow(
            "[cyan]EnableBigFileRouting[/]",
            appSettings.EnableBigFileRouting ? "[green]true[/]" : "[red]false[/]",
            "[dim]Route large files to Big_Files[/]"
        );

        table.AddRow(
            "[cyan]IgnoreExtensions[/]",
            $"[white]{string.Join(", ", appSettings.IgnoreExtensions)}[/]",
            "[dim]Extensions to skip[/]"
        );

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Commands:[/]");
        AnsiConsole.MarkupLine("  [blue]sorter config set <key> <value>[/]  Update a setting");
        AnsiConsole.MarkupLine("  [blue]sorter config get <key>[/]          Show a specific setting");
        AnsiConsole.MarkupLine("  [blue]sorter config reset[/]              Reset to defaults");

        return 0;
    }

    private int GetConfig(AppSettings appSettings, string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] sorter config get <key>");
            return 1;
        }

        var value = GetSettingValue(appSettings, key);
        if (value == null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown setting:[/] {key}");
            AnsiConsole.MarkupLine("[dim]Available:[/] " + string.Join(", ", ConfigKeys.Keys));
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]{key}[/] = [white]{value}[/]");

        if (ConfigKeys.TryGetValue(key, out var info))
        {
            AnsiConsole.MarkupLine($"[dim]{info.description}[/]");
        }

        return 0;
    }

    private int SetConfig(AppSettings appSettings, string? key, string? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] sorter config set <key> <value>");
            return 1;
        }

        if (string.IsNullOrEmpty(value))
        {
            AnsiConsole.MarkupLine($"[red]Missing value for {key}[/]");
            if (ConfigKeys.TryGetValue(key, out var info))
            {
                AnsiConsole.MarkupLine($"[dim]Example: sorter config set {key} {info.example}[/]");
            }
            return 1;
        }

        var oldValue = GetSettingValue(appSettings, key);
        if (oldValue == null)
        {
            AnsiConsole.MarkupLine($"[red]Unknown setting:[/] {key}");
            AnsiConsole.MarkupLine("[dim]Available:[/] " + string.Join(", ", ConfigKeys.Keys));
            return 1;
        }

        try
        {
            switch (key.ToLowerInvariant())
            {
                case "settletimeseconds":
                    appSettings.SettleTimeSeconds = int.Parse(value);
                    break;
                case "bigfilethreshold":
                    appSettings.BigFileThreshold = ParseSize(value);
                    break;
                case "enablebigfilerouting":
                    appSettings.EnableBigFileRouting = bool.Parse(value);
                    break;
                case "ignoreextensions":
                    appSettings.IgnoreExtensions = value
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Select(e => e.StartsWith('.') ? e : "." + e)
                        .ToArray();
                    break;
                case "shownotifications":
                    appSettings.ShowNotifications = bool.Parse(value);
                    break;
                default:
                    AnsiConsole.MarkupLine($"[red]Cannot modify:[/] {key}");
                    return 1;
            }

            appSettings.Save();

            var newValue = GetSettingValue(appSettings, key);
            AnsiConsole.MarkupLine($"[green]✓[/] {key}: {oldValue} → [green]{newValue}[/]");
            return 0;
        }
        catch (FormatException)
        {
            AnsiConsole.MarkupLine($"[red]Invalid value:[/] {value}");
            if (ConfigKeys.TryGetValue(key, out var info))
            {
                AnsiConsole.MarkupLine($"[dim]Example: {info.example}[/]");
            }
            return 1;
        }
    }

    private int ResetConfig(AppSettings appSettings)
    {
        AnsiConsole.MarkupLine("[yellow]This will reset settings to defaults.[/]");
        AnsiConsole.MarkupLine("[dim]RootPath and Categories will be preserved.[/]");

        if (!AnsiConsole.Confirm("Reset settings?", false))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return 0;
        }

        // Preserve root path and categories
        var rootPath = appSettings.RootPath;
        var categories = appSettings.Categories;
        var dbPath = appSettings.DatabasePath;

        // Create fresh settings
        var defaults = new AppSettings
        {
            RootPath = rootPath,
            Categories = categories,
            DatabasePath = dbPath
        };

        defaults.Save();

        AnsiConsole.MarkupLine("[green]✓[/] Settings reset to defaults.");
        return 0;
    }

    private static string? GetSettingValue(AppSettings settings, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "rootpath" => settings.RootPath,
            "inboxpath" => settings.InboxPath,
            "databasepath" => settings.DatabasePath,
            "settletimeseconds" => settings.SettleTimeSeconds.ToString(),
            "bigfilethreshold" => settings.BigFileThreshold.ToString(),
            "enablebigfilerouting" => settings.EnableBigFileRouting.ToString().ToLower(),
            "ignoreextensions" => string.Join(",", settings.IgnoreExtensions),
            "shownotifications" => settings.ShowNotifications.ToString().ToLower(),
            _ => null
        };
    }

    private static long ParseSize(string value)
    {
        value = value.Trim().ToUpperInvariant();

        // Handle suffixes
        if (value.EndsWith("GB") || value.EndsWith("G"))
        {
            var num = double.Parse(value.TrimEnd('G', 'B'));
            return (long)(num * 1024 * 1024 * 1024);
        }
        if (value.EndsWith("MB") || value.EndsWith("M"))
        {
            var num = double.Parse(value.TrimEnd('M', 'B'));
            return (long)(num * 1024 * 1024);
        }
        if (value.EndsWith("KB") || value.EndsWith("K"))
        {
            var num = double.Parse(value.TrimEnd('K', 'B'));
            return (long)(num * 1024);
        }

        return long.Parse(value);
    }
}

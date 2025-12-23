using DownloadSorter.Core.Configuration;
using DownloadSorter.Cli.Styling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class RulesCommand : BaseCommand<RulesCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACTION]")]
        public string? Action { get; set; }

        [CommandArgument(1, "[INDEX]")]
        public int? Index { get; set; }
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
            "add" => AddRule(appSettings),
            "edit" => EditRule(appSettings, settings.Index),
            "delete" or "remove" => DeleteRule(appSettings, settings.Index),
            "test" => TestRule(appSettings),
            _ => ListRules(appSettings)
        };
    }

    private int ListRules(AppSettings appSettings)
    {
        AnsiConsole.Write(new Rule("[bold blue]📋 Category Rules[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .Expand();

        table.AddColumn(new TableColumn("[bold]#[/]").Width(3).Centered());
        table.AddColumn(new TableColumn("[bold]Category[/]"));
        table.AddColumn(new TableColumn("[bold]Extensions[/]"));
        table.AddColumn(new TableColumn("[bold]Subfolder[/]"));

        var idx = 1;
        foreach (var rule in appSettings.Categories)
        {
            var icon = Theme.GetCategoryIcon(rule.Category);
            var name = Theme.FormatCategoryName(rule.Category);
            var color = Theme.GetCategoryColor(rule.Category);

            var exts = string.Join(", ", rule.Extensions.Take(10));
            if (rule.Extensions.Length > 10)
            {
                exts += $" [dim]+{rule.Extensions.Length - 10} more[/]";
            }

            table.AddRow(
                $"[dim]{idx}[/]",
                $"{icon} [{color}]{name}[/]",
                exts,
                rule.Subfolder ?? "[dim]-[/]"
            );
            idx++;
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Commands:[/]");
        AnsiConsole.MarkupLine("  [blue]sorter rules add[/]        Add a new rule");
        AnsiConsole.MarkupLine("  [blue]sorter rules edit N[/]     Edit rule #N");
        AnsiConsole.MarkupLine("  [blue]sorter rules delete N[/]   Delete rule #N");
        AnsiConsole.MarkupLine("  [blue]sorter rules test[/]       Test filename routing");

        return 0;
    }

    private int AddRule(AppSettings appSettings)
    {
        AnsiConsole.Write(new Rule("[bold blue]➕ Add Category Rule[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Select category
        var categoryChoices = AppSettings.Folders.All
            .Where(f => f != AppSettings.Folders.Inbox && f != AppSettings.Folders.Pinned)
            .Select(f => $"{Theme.GetCategoryIcon(f)} {Theme.FormatCategoryName(f)} ({f})")
            .ToList();

        var categoryChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select target category:")
                .AddChoices(categoryChoices)
        );

        // Extract folder name from choice
        var folderStart = categoryChoice.LastIndexOf('(') + 1;
        var folderEnd = categoryChoice.LastIndexOf(')');
        var folder = categoryChoice[folderStart..folderEnd];

        // Get extensions
        var extensionsInput = AnsiConsole.Prompt(
            new TextPrompt<string>("Extensions (comma-separated, e.g., .pdf,.doc,.txt):")
                .Validate(input =>
                {
                    var exts = ParseExtensions(input);
                    return exts.Count > 0
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter at least one extension");
                })
        );

        var extensions = ParseExtensions(extensionsInput);

        // Optional subfolder
        var subfolder = AnsiConsole.Prompt(
            new TextPrompt<string>("Subfolder (optional, press Enter to skip):")
                .AllowEmpty()
        );

        // Create rule
        var rule = new CategoryRule
        {
            Category = folder,
            Extensions = extensions.ToArray(),
            Subfolder = string.IsNullOrWhiteSpace(subfolder) ? null : subfolder
        };

        // Check for conflicts
        var conflicts = FindConflicts(appSettings, extensions, null);
        if (conflicts.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Warning:[/] These extensions already exist in other rules:");
            foreach (var (ext, cat) in conflicts)
            {
                AnsiConsole.MarkupLine($"  {ext} → {Theme.FormatCategoryName(cat)}");
            }
            AnsiConsole.WriteLine();

            if (!AnsiConsole.Confirm("Add anyway?", false))
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return 0;
            }
        }

        // Add and save
        appSettings.Categories.Add(rule);
        appSettings.Save();

        AnsiConsole.MarkupLine($"\n[green]✓[/] Rule added: {extensions.Count} extensions → [blue]{folder}[/]");
        return 0;
    }

    private int EditRule(AppSettings appSettings, int? index)
    {
        if (index == null)
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] sorter rules edit <index>");
            return 1;
        }

        var idx = index.Value - 1;
        if (idx < 0 || idx >= appSettings.Categories.Count)
        {
            AnsiConsole.MarkupLine($"[red]Invalid rule index.[/] Must be 1-{appSettings.Categories.Count}");
            return 1;
        }

        var rule = appSettings.Categories[idx];

        AnsiConsole.Write(new Rule($"[bold blue]✏️ Edit Rule #{index}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[bold]Category:[/] {Theme.GetCategoryIcon(rule.Category)} {Theme.FormatCategoryName(rule.Category)}");
        AnsiConsole.MarkupLine($"[bold]Extensions:[/] {string.Join(", ", rule.Extensions)}");
        AnsiConsole.MarkupLine($"[bold]Subfolder:[/] {rule.Subfolder ?? "(none)"}");
        AnsiConsole.WriteLine();

        // Edit extensions
        var currentExts = string.Join(", ", rule.Extensions);
        var newExtsInput = AnsiConsole.Prompt(
            new TextPrompt<string>($"Extensions [{currentExts}]:")
                .AllowEmpty()
        );

        if (!string.IsNullOrWhiteSpace(newExtsInput))
        {
            rule.Extensions = ParseExtensions(newExtsInput).ToArray();
        }

        // Edit subfolder
        var newSubfolder = AnsiConsole.Prompt(
            new TextPrompt<string>($"Subfolder [{rule.Subfolder ?? "(none)"}]:")
                .AllowEmpty()
        );

        if (!string.IsNullOrWhiteSpace(newSubfolder))
        {
            rule.Subfolder = newSubfolder == "-" ? null : newSubfolder;
        }

        appSettings.Save();
        AnsiConsole.MarkupLine($"\n[green]✓[/] Rule updated.");
        return 0;
    }

    private int DeleteRule(AppSettings appSettings, int? index)
    {
        if (index == null)
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] sorter rules delete <index>");
            return 1;
        }

        var idx = index.Value - 1;
        if (idx < 0 || idx >= appSettings.Categories.Count)
        {
            AnsiConsole.MarkupLine($"[red]Invalid rule index.[/] Must be 1-{appSettings.Categories.Count}");
            return 1;
        }

        var rule = appSettings.Categories[idx];

        AnsiConsole.MarkupLine($"Delete rule for [blue]{Theme.FormatCategoryName(rule.Category)}[/]?");
        AnsiConsole.MarkupLine($"Extensions: {string.Join(", ", rule.Extensions.Take(5))}{(rule.Extensions.Length > 5 ? "..." : "")}");

        if (!AnsiConsole.Confirm("Delete this rule?", false))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return 0;
        }

        appSettings.Categories.RemoveAt(idx);
        appSettings.Save();

        AnsiConsole.MarkupLine($"\n[green]✓[/] Rule deleted.");
        return 0;
    }

    private int TestRule(AppSettings appSettings)
    {
        AnsiConsole.Write(new Rule("[bold blue]🧪 Test File Routing[/]").LeftJustified());
        AnsiConsole.WriteLine();

        while (true)
        {
            var filename = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter filename (or 'q' to quit):")
            );

            if (filename.ToLowerInvariant() == "q")
                break;

            var ext = Path.GetExtension(filename);
            if (string.IsNullOrEmpty(ext))
            {
                AnsiConsole.MarkupLine("[yellow]No extension found.[/] Would go to → [grey]_UNSORTED[/]");
                continue;
            }

            // Simulate file size
            var size = 100L * 1024; // 100KB default

            var dest = appSettings.GetDestinationFolder(ext, size);
            var category = Path.GetFileName(dest);
            var icon = Theme.GetCategoryIcon(category);
            var color = Theme.GetCategoryColor(category);

            AnsiConsole.MarkupLine($"Extension [blue]{ext}[/] → {icon} [{color}]{Theme.FormatCategoryName(category)}[/]");

            // Check if big file routing would apply
            if (appSettings.EnableBigFileRouting)
            {
                AnsiConsole.MarkupLine($"[dim]  (Files > {FormatSize(appSettings.BigFileThreshold)} go to Big_Files)[/]");
            }

            AnsiConsole.WriteLine();
        }

        return 0;
    }

    private static List<string> ParseExtensions(string input)
    {
        return input
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private static List<(string ext, string category)> FindConflicts(
        AppSettings settings, List<string> extensions, CategoryRule? exclude)
    {
        var conflicts = new List<(string, string)>();

        foreach (var ext in extensions)
        {
            foreach (var rule in settings.Categories)
            {
                if (rule == exclude) continue;

                if (rule.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    conflicts.Add((ext, rule.Category));
                }
            }
        }

        return conflicts;
    }
}

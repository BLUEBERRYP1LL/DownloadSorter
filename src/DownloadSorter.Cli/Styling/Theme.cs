using Spectre.Console;

namespace DownloadSorter.Cli.Styling;

/// <summary>
/// Centralized styling for the CLI - colors, icons, and visual elements.
/// </summary>
public static class Theme
{
    // Category colors
    public static Style Documents => new(Color.Blue);
    public static Style Executables => new(Color.Red);
    public static Style Archives => new(Color.Green);
    public static Style Media => new(Color.Fuchsia);
    public static Style Code => new(Color.Yellow);
    public static Style ISOs => new(Color.Purple);
    public static Style BigFiles => new(Color.Orange1);
    public static Style Unsorted => new(Color.Grey);

    // Status colors
    public static Style Success => new(Color.Green);
    public static Style Warning => new(Color.Yellow);
    public static Style Error => new(Color.Red);
    public static Style Muted => new(Color.Grey);
    public static Style Accent => new(Color.Blue);
    public static Style Highlight => new(Color.Cyan1);

    // Status icons
    public const string IconSuccess = "[green]✓[/]";
    public const string IconFailed = "[red]✗[/]";
    public const string IconWarning = "[yellow]⚠[/]";
    public const string IconPending = "[yellow]●[/]";
    public const string IconRunning = "[green]●[/]";
    public const string IconStopped = "[red]●[/]";
    public const string IconInfo = "[blue]ℹ[/]";

    // Category icons
    public static string GetCategoryIcon(string category) => category switch
    {
        "10_Documents" => "📄",
        "20_Executables" => "⚙️",
        "30_Archives" => "📦",
        "40_Media" => "🎬",
        "50_Code" => "💻",
        "60_ISOs" => "💿",
        "80_Big_Files" => "📀",
        "00_INBOX" => "📥",
        "00_PINNED" => "📌",
        "_UNSORTED" => "❓",
        _ => "📁"
    };

    // Category display color
    public static Style GetCategoryStyle(string category) => category switch
    {
        "10_Documents" => Documents,
        "20_Executables" => Executables,
        "30_Archives" => Archives,
        "40_Media" => Media,
        "50_Code" => Code,
        "60_ISOs" => ISOs,
        "80_Big_Files" => BigFiles,
        "_UNSORTED" => Unsorted,
        _ => Accent
    };

    // Category display color as markup string
    public static string GetCategoryColor(string category) => category switch
    {
        "10_Documents" => "blue",
        "20_Executables" => "red",
        "30_Archives" => "green",
        "40_Media" => "fuchsia",
        "50_Code" => "yellow",
        "60_ISOs" => "purple",
        "80_Big_Files" => "orange1",
        "_UNSORTED" => "grey",
        _ => "blue"
    };

    /// <summary>
    /// Format a category name for display (removes numeric prefix and underscores).
    /// </summary>
    public static string FormatCategoryName(string category)
    {
        return category
            .Replace("_", " ")
            .TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ');
    }

    /// <summary>
    /// Create a styled category label with icon.
    /// </summary>
    public static string CategoryLabel(string category)
    {
        var icon = GetCategoryIcon(category);
        var color = GetCategoryColor(category);
        var name = FormatCategoryName(category);
        return $"{icon} [{color}]{name}[/]";
    }

    // Common panels
    public static Panel CreateHeaderPanel(string title, string content)
    {
        return new Panel(new Markup(content))
            .Header($"[bold blue]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
    }

    // Tab bar helpers
    public static string TabBar(int activeTab, params string[] tabs)
    {
        var parts = new List<string>();
        for (var i = 0; i < tabs.Length; i++)
        {
            var num = i + 1;
            if (i == activeTab)
            {
                parts.Add($"[bold white on blue] {num} {tabs[i]} [/]");
            }
            else
            {
                parts.Add($"[dim] {num} {tabs[i]} [/]");
            }
        }
        return string.Join(" ", parts);
    }

    // Hotkey bar for status line
    public static string HotkeyBar(params (string key, string desc)[] hotkeys)
    {
        var parts = hotkeys.Select(h => $"[blue]{h.key}[/]:{h.desc}");
        return string.Join("  ", parts);
    }

    // Progress spinners
    public static Spinner LoadingSpinner => Spinner.Known.Dots;
    public static Spinner ProcessingSpinner => Spinner.Known.Arc;
}

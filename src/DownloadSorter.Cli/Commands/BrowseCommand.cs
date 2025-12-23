using System.Diagnostics;
using DownloadSorter.Core.Configuration;
using DownloadSorter.Core.Services;
using DownloadSorter.Cli.Interactive;
using DownloadSorter.Cli.Styling;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DownloadSorter.Cli.Commands;

public class BrowseCommand : BaseCommand<BrowseCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[CATEGORY]")]
        public string? Category { get; set; }
    }

    private enum FocusPanel { Categories, Files }

    public override int Execute(CommandContext context, Settings settings)
    {
        var appSettings = LoadSettings();

        if (string.IsNullOrEmpty(appSettings.RootPath))
        {
            AnsiConsole.MarkupLine("[red]Not configured.[/] Run [blue]sorter init[/] first.");
            return 1;
        }

        var cts = new CancellationTokenSource();
        var keyboard = new KeyboardHandler();
        var state = new BrowserState(appSettings, settings.Category);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        keyboard.KeyInfoPressed += info => HandleKey(info, state, appSettings);

        AnsiConsole.Clear();
        Console.CursorVisible = false;

        try
        {
            keyboard.Start(cts.Token);
            RunBrowserLoop(appSettings, state, cts.Token);
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
        AnsiConsole.MarkupLine("[dim]Browser closed.[/]");
        return 0;
    }

    private void HandleKey(ConsoleKeyInfo info, BrowserState state, AppSettings appSettings)
    {
        lock (state)
        {
            switch (info.Key)
            {
                // Navigation
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    state.MoveUp();
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    state.MoveDown();
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.H:
                    state.FocusCategories();
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.L:
                    state.FocusFiles();
                    break;
                case ConsoleKey.Tab:
                    state.ToggleFocus();
                    break;

                // Actions
                case ConsoleKey.Enter:
                    OpenSelectedFile(state);
                    break;
                case ConsoleKey.P:
                    PinSelectedFile(state, appSettings);
                    break;
                case ConsoleKey.M:
                    MoveSelectedFile(state, appSettings);
                    break;
                case ConsoleKey.R:
                    state.Refresh();
                    break;
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    state.ShouldExit = true;
                    break;
            }
        }
    }

    private void RunBrowserLoop(AppSettings appSettings, BrowserState state, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            lock (state)
            {
                if (state.ShouldExit)
                    break;

                if (state.NeedsRefresh)
                {
                    RenderBrowser(state);
                    state.NeedsRefresh = false;
                }
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

    private void RenderBrowser(BrowserState state)
    {
        AnsiConsole.Cursor.SetPosition(0, 0);

        // Header
        var title = new Rule("[bold blue]📁 File Browser[/]")
            .LeftJustified()
            .RuleStyle(Style.Parse("blue"));
        AnsiConsole.Write(title);
        AnsiConsole.WriteLine();

        // Two-column layout
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Categories").Size(25),
                new Layout("Files")
            );

        // Categories panel
        var catTable = new Table().Border(TableBorder.None).HideHeaders();
        catTable.AddColumn("");

        for (var i = 0; i < state.Categories.Count; i++)
        {
            var cat = state.Categories[i];
            var isSelected = i == state.CategoryIndex;
            var icon = Theme.GetCategoryIcon(cat);
            var name = Theme.FormatCategoryName(cat);
            var color = Theme.GetCategoryColor(cat);

            if (isSelected && state.Focus == FocusPanel.Categories)
            {
                catTable.AddRow($"[white on blue] {icon} {name} [/]");
            }
            else if (isSelected)
            {
                catTable.AddRow($"[bold]{icon} [{color}]{name}[/][/]");
            }
            else
            {
                catTable.AddRow($"  {icon} [{color}]{name}[/]");
            }
        }

        var catBorder = state.Focus == FocusPanel.Categories ? Color.Cyan1 : Color.Blue;
        layout["Categories"].Update(new Panel(catTable)
            .Header("[blue]Categories[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(catBorder));

        // Files panel
        var filesTable = new Table().Border(TableBorder.None).HideHeaders();
        filesTable.AddColumn("");
        filesTable.AddColumn(new TableColumn("").RightAligned());

        var categoryName = Theme.FormatCategoryName(state.CurrentCategory);
        var categoryPath = state.CurrentCategoryPath;
        var files = state.Files;

        if (files.Count == 0)
        {
            filesTable.AddRow("[dim]No files in this category[/]", "");
        }
        else
        {
            for (var i = 0; i < Math.Min(files.Count, 20); i++)
            {
                var file = files[i];
                var isSelected = i == state.FileIndex;
                var name = file.Name.Length > 45 ? file.Name[..42] + "..." : file.Name;

                if (isSelected && state.Focus == FocusPanel.Files)
                {
                    filesTable.AddRow(
                        $"[white on blue] {Markup.Escape(name)} [/]",
                        $"[white on blue] {FormatSize(file.Length)} [/]"
                    );
                }
                else if (isSelected)
                {
                    filesTable.AddRow(
                        $"[bold]▸ {Markup.Escape(name)}[/]",
                        $"[dim]{FormatSize(file.Length)}[/]"
                    );
                }
                else
                {
                    filesTable.AddRow(
                        $"  {Markup.Escape(name)}",
                        $"[dim]{FormatSize(file.Length)}[/]"
                    );
                }
            }

            if (files.Count > 20)
            {
                filesTable.AddRow($"  [dim]... and {files.Count - 20} more[/]", "");
            }
        }

        var filesBorder = state.Focus == FocusPanel.Files ? Color.Cyan1 : Color.Blue;
        layout["Files"].Update(new Panel(filesTable)
            .Header($"[blue]{Theme.GetCategoryIcon(state.CurrentCategory)} {categoryName}[/] [dim]({files.Count} files)[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(filesBorder));

        AnsiConsole.Write(layout);

        // Status bar
        AnsiConsole.WriteLine();
        var hotkeys = Theme.HotkeyBar(
            ("↑↓", "nav"),
            ("←→", "focus"),
            ("Enter", "open"),
            ("p", "pin"),
            ("m", "move"),
            ("q", "quit")
        );
        AnsiConsole.MarkupLine($"  {hotkeys}");

        // Show error if present
        if (!string.IsNullOrEmpty(state.ErrorMessage))
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape(state.ErrorMessage)}[/]");
            state.ClearError();
        }
        else
        {
            AnsiConsole.WriteLine();
        }

        // Clear remaining lines
        for (var i = 0; i < 2; i++)
        {
            AnsiConsole.WriteLine(new string(' ', Console.WindowWidth - 1));
        }
    }

    private void OpenSelectedFile(BrowserState state)
    {
        var file = state.SelectedFile;
        if (file == null) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = file.FullName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            state.SetError($"Cannot open file: {ex.Message}");
        }
    }

    private void PinSelectedFile(BrowserState state, AppSettings appSettings)
    {
        var file = state.SelectedFile;
        if (file == null) return;

        var pinnedPath = Path.Combine(appSettings.RootPath, AppSettings.Folders.Pinned);
        Directory.CreateDirectory(pinnedPath);

        var destPath = CollisionResolver.GetUniqueFilePath(pinnedPath, file.Name);

        try
        {
            File.Move(file.FullName, destPath);
            state.Refresh();
        }
        catch (Exception ex)
        {
            state.SetError($"Cannot pin file: {ex.Message}");
        }
    }

    private void MoveSelectedFile(BrowserState state, AppSettings appSettings)
    {
        var file = state.SelectedFile;
        if (file == null) return;

        // Show category picker
        Console.CursorVisible = true;
        AnsiConsole.Clear();

        var categories = state.Categories
            .Where(c => c != state.CurrentCategory)
            .ToList();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Move [blue]{file.Name}[/] to:")
                .AddChoices(categories.Select(c => $"{Theme.GetCategoryIcon(c)} {Theme.FormatCategoryName(c)}"))
        );

        // Parse choice back to category
        var targetCat = categories.FirstOrDefault(c =>
            choice.Contains(Theme.FormatCategoryName(c)));

        if (targetCat != null)
        {
            var destFolder = Path.Combine(appSettings.RootPath, targetCat);
            Directory.CreateDirectory(destFolder);

            var destPath = CollisionResolver.GetUniqueFilePath(destFolder, file.Name);

            try
            {
                File.Move(file.FullName, destPath);
            }
            catch
            {
                AnsiConsole.MarkupLine("[red]Failed to move file.[/]");
                Thread.Sleep(1000);
            }
        }

        Console.CursorVisible = false;
        AnsiConsole.Clear();
        state.Refresh();
    }

    private class BrowserState
    {
        public List<string> Categories { get; }
        public int CategoryIndex { get; private set; }
        public int FileIndex { get; private set; }
        public FocusPanel Focus { get; private set; } = FocusPanel.Categories;
        public bool NeedsRefresh { get; set; } = true;
        public bool ShouldExit { get; set; }

        private readonly AppSettings _appSettings;
        private List<FileInfo>? _files;

        public BrowserState(AppSettings appSettings, string? initialCategory)
        {
            _appSettings = appSettings;

            // Build category list from actual folders
            Categories = AppSettings.Folders.All
                .Where(f => f != AppSettings.Folders.Inbox)
                .Where(f => Directory.Exists(Path.Combine(appSettings.RootPath, f)))
                .ToList();

            // Add inbox at the start
            Categories.Insert(0, AppSettings.Folders.Inbox);

            // Set initial category
            if (!string.IsNullOrEmpty(initialCategory))
            {
                var idx = Categories.FindIndex(c =>
                    c.Equals(initialCategory, StringComparison.OrdinalIgnoreCase) ||
                    Theme.FormatCategoryName(c).Equals(initialCategory, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    CategoryIndex = idx;
            }
        }

        public string CurrentCategory => Categories.Count > 0 ? Categories[CategoryIndex] : "";

        public string CurrentCategoryPath =>
            Path.Combine(_appSettings.RootPath, CurrentCategory);

        public List<FileInfo> Files
        {
            get
            {
                if (_files == null)
                    LoadFiles();
                return _files!;
            }
        }

        public FileInfo? SelectedFile =>
            Files.Count > 0 && FileIndex < Files.Count ? Files[FileIndex] : null;

        public void MoveUp()
        {
            if (Focus == FocusPanel.Categories)
            {
                if (CategoryIndex > 0)
                {
                    CategoryIndex--;
                    _files = null;
                    FileIndex = 0;
                    NeedsRefresh = true;
                }
            }
            else
            {
                if (FileIndex > 0)
                {
                    FileIndex--;
                    NeedsRefresh = true;
                }
            }
        }

        public void MoveDown()
        {
            if (Focus == FocusPanel.Categories)
            {
                if (CategoryIndex < Categories.Count - 1)
                {
                    CategoryIndex++;
                    _files = null;
                    FileIndex = 0;
                    NeedsRefresh = true;
                }
            }
            else
            {
                if (FileIndex < Files.Count - 1)
                {
                    FileIndex++;
                    NeedsRefresh = true;
                }
            }
        }

        public void FocusCategories()
        {
            if (Focus != FocusPanel.Categories)
            {
                Focus = FocusPanel.Categories;
                NeedsRefresh = true;
            }
        }

        public void FocusFiles()
        {
            if (Focus != FocusPanel.Files && Files.Count > 0)
            {
                Focus = FocusPanel.Files;
                NeedsRefresh = true;
            }
        }

        public void ToggleFocus()
        {
            if (Focus == FocusPanel.Categories && Files.Count > 0)
                Focus = FocusPanel.Files;
            else
                Focus = FocusPanel.Categories;
            NeedsRefresh = true;
        }

        public string? ErrorMessage { get; private set; }

        public void SetError(string message)
        {
            ErrorMessage = message;
            NeedsRefresh = true;
        }

        public void ClearError()
        {
            ErrorMessage = null;
        }

        public void Refresh()
        {
            _files = null;
            NeedsRefresh = true;
        }

        private void LoadFiles()
        {
            var path = CurrentCategoryPath;
            if (!Directory.Exists(path))
            {
                _files = [];
                return;
            }

            _files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
        }
    }
}

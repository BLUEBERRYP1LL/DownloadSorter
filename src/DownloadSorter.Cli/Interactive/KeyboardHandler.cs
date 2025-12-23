namespace DownloadSorter.Cli.Interactive;

/// <summary>
/// Handles keyboard input in a background thread for interactive CLI modes.
/// </summary>
public class KeyboardHandler : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public event Action<ConsoleKey>? KeyPressed;
    public event Action<ConsoleKeyInfo>? KeyInfoPressed;

    public void Start(CancellationToken externalCt = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var token = _cts.Token;

        _listenerTask = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);
                        KeyInfoPressed?.Invoke(keyInfo);
                        KeyPressed?.Invoke(keyInfo.Key);
                    }
                    Thread.Sleep(50);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors reading keys
                }
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listenerTask?.Wait(500);
        }
        catch
        {
            // Ignore
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// State machine for multi-panel interactive interfaces.
/// </summary>
public class InteractiveState
{
    private readonly object _lock = new();

    public int ActiveTab { get; private set; }
    public int TabCount { get; }
    public int SelectedIndex { get; private set; }
    public int MaxIndex { get; set; }
    public bool NeedsRefresh { get; private set; }
    public bool ShouldExit { get; private set; }
    public bool TriggerSort { get; private set; }
    public bool ShowHelp { get; private set; }

    public InteractiveState(int tabCount = 3)
    {
        TabCount = tabCount;
        ActiveTab = 0;
        SelectedIndex = 0;
        MaxIndex = 0;
    }

    public void HandleKey(ConsoleKey key)
    {
        lock (_lock)
        {
            TriggerSort = false;
            ShowHelp = false;

            switch (key)
            {
                // Tab switching
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    SetTab(0);
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    SetTab(1);
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    SetTab(2);
                    break;
                case ConsoleKey.Tab:
                    SetTab((ActiveTab + 1) % TabCount);
                    break;

                // Navigation
                case ConsoleKey.UpArrow:
                case ConsoleKey.K:
                    if (SelectedIndex > 0)
                    {
                        SelectedIndex--;
                        NeedsRefresh = true;
                    }
                    break;
                case ConsoleKey.DownArrow:
                case ConsoleKey.J:
                    if (SelectedIndex < MaxIndex - 1)
                    {
                        SelectedIndex++;
                        NeedsRefresh = true;
                    }
                    break;

                // Actions
                case ConsoleKey.R:
                    NeedsRefresh = true;
                    break;
                case ConsoleKey.S:
                    TriggerSort = true;
                    NeedsRefresh = true;
                    break;
                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    ShouldExit = true;
                    break;
                case ConsoleKey.Oem2: // ? key
                case ConsoleKey.H:
                    ShowHelp = !ShowHelp;
                    NeedsRefresh = true;
                    break;
            }
        }
    }

    private void SetTab(int tab)
    {
        if (tab >= 0 && tab < TabCount && tab != ActiveTab)
        {
            ActiveTab = tab;
            SelectedIndex = 0;
            NeedsRefresh = true;
        }
    }

    public void ClearRefresh()
    {
        lock (_lock)
        {
            NeedsRefresh = false;
        }
    }

    public InteractiveStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new InteractiveStateSnapshot(
                ActiveTab, TabCount, SelectedIndex, MaxIndex,
                NeedsRefresh, ShouldExit, TriggerSort, ShowHelp);
        }
    }
}

public record InteractiveStateSnapshot(
    int ActiveTab,
    int TabCount,
    int SelectedIndex,
    int MaxIndex,
    bool NeedsRefresh,
    bool ShouldExit,
    bool TriggerSort,
    bool ShowHelp);

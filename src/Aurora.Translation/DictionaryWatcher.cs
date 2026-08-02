namespace Aurora.Translation;

internal sealed class DictionaryWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly Action _onChanged;
    private CancellationTokenSource? _reloadCts;
    private readonly object _reloadGate = new();

    public DictionaryWatcher(string path, Action onChanged)
    {
        _onChanged = onChanged;
        var dir = Path.GetDirectoryName(path);
        var file = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file) || !Directory.Exists(dir))
            return;

        _watcher = new FileSystemWatcher(dir, file)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        CancellationToken token;
        lock (_reloadGate)
        {
            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
            _reloadCts = new CancellationTokenSource();
            token = _reloadCts.Token;
        }
        _ = Task.Delay(300, token).ContinueWith(
            _ => _onChanged(),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    public void Dispose()
    {
        lock (_reloadGate)
        {
            _reloadCts?.Cancel();
            _reloadCts?.Dispose();
            _reloadCts = null;
        }
        _watcher?.Dispose();
    }
}

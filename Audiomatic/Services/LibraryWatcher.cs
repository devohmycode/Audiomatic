using System.Collections.Concurrent;

namespace Audiomatic.Services;

public sealed class LibraryWatcher : IDisposable
{
    private readonly Dictionary<long, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, WatcherChangeType> _pendingChanges = new();
    private Timer? _debounceTimer;
    private readonly object _lock = new();
    private bool _disposed;
    private const int DebounceMs = 600;

    public event Action? LibraryChanged;

    public void Start()
    {
        var folders = LibraryManager.GetFolders();
        foreach (var folder in folders.Where(f => f.Enabled))
            WatchFolder(folder.Id, folder.Path);
    }

    public void Restart()
    {
        StopAll();
        Start();
    }

    public void WatchFolder(long folderId, string path)
    {
        lock (_lock)
        {
            if (_disposed || _watchers.ContainsKey(folderId)) return;
            if (!Directory.Exists(path)) return;

            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            foreach (var ext in LibraryManager.AudioExtensions)
                watcher.Filters.Add($"*{ext}");

            watcher.Created += (_, e) => QueueChange(e.FullPath, WatcherChangeType.Created);
            watcher.Deleted += (_, e) => QueueChange(e.FullPath, WatcherChangeType.Deleted);
            watcher.Changed += (_, e) => QueueChange(e.FullPath, WatcherChangeType.Changed);
            watcher.Renamed += OnRenamed;
            watcher.Error += (_, _) => { /* Buffer overflow — next manual scan will catch up */ };

            watcher.EnableRaisingEvents = true;
            _watchers[folderId] = watcher;
        }
    }

    public void UnwatchFolder(long folderId)
    {
        lock (_lock)
        {
            if (_watchers.Remove(folderId, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }

    private void StopAll()
    {
        lock (_lock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        QueueChange(e.OldFullPath, WatcherChangeType.Deleted);

        var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (LibraryManager.AudioExtensions.Contains(ext))
            QueueChange(e.FullPath, WatcherChangeType.Created);
    }

    private void QueueChange(string path, WatcherChangeType type)
    {
        if (_disposed) return;

        _pendingChanges.AddOrUpdate(path, type, (_, existing) =>
            type == WatcherChangeType.Deleted ? WatcherChangeType.Deleted : type);

        ResetDebounceTimer();
    }

    private void ResetDebounceTimer()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(ProcessChanges, null, DebounceMs, Timeout.Infinite);
        }
    }

    private async void ProcessChanges(object? state)
    {
        if (_disposed) return;

        var changes = new Dictionary<string, WatcherChangeType>();
        foreach (var key in _pendingChanges.Keys.ToList())
        {
            if (_pendingChanges.TryRemove(key, out var type))
                changes[key] = type;
        }

        if (changes.Count == 0) return;

        bool anyChange = false;

        foreach (var (path, type) in changes)
        {
            try
            {
                if (type == WatcherChangeType.Deleted)
                {
                    if (LibraryManager.RemoveTrackByPath(path))
                        anyChange = true;
                }
                else
                {
                    if (!await WaitForFileReady(path, 3000))
                        continue;

                    if (await LibraryManager.AddOrUpdateFileAsync(path))
                        anyChange = true;
                }
            }
            catch
            {
                // Individual file failures shouldn't stop processing
            }
        }

        if (anyChange)
            LibraryChanged?.Invoke();
    }

    private static async Task<bool> WaitForFileReady(string path, int timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (!File.Exists(path)) return false;
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(200);
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;

            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }

    private enum WatcherChangeType { Created, Changed, Deleted }
}

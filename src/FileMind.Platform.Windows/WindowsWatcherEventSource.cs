using System.Threading.Channels;
using FileMind.Application.Ports;
using FileMind.Domain.Errors;
using FileMind.Application.Watching;

namespace FileMind.Platform.Windows;

/// <summary>
/// System.IO.FileSystemWatcher per watched root. Watcher failures (overflow,
/// disposed, unreadable root) surface as Error-kind events — never swallowed.
/// </summary>
public sealed class WindowsWatcherEventSource : IWatcherEventSource, IDisposable
{
    private readonly Channel<WatchEvent> _events =
        Channel.CreateUnbounded<WatchEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, FileSystemWatcher> _watchers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public async IAsyncEnumerable<WatchEvent> EventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (await _events.Reader.WaitToReadAsync(ct))
            while (_events.Reader.TryRead(out var ev))
                yield return ev;
    }

    public Task<Result<bool>> StartWatchingAsync(string path, CancellationToken ct = default)
    {
        try
        {
            lock (_gate)
            {
                if (_watchers.ContainsKey(path)) return Task.FromResult(Result<bool>.Ok(true));
                if (!Directory.Exists(path))
                    return Task.FromResult(Result<bool>.Fail(
                        FileMindError.NotFound($"watched folder '{path}' does not exist")));

                var w = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                    InternalBufferSize = 64 * 1024
                };
                w.Created += (_, e) => Emit(path, WatchEventKind.Created, e.FullPath);
                w.Changed += (_, e) => Emit(path, WatchEventKind.Changed, e.FullPath);
                w.Renamed += (_, e) => Emit(path, WatchEventKind.Renamed, e.FullPath);
                w.Deleted += (_, e) => Emit(path, WatchEventKind.Deleted, e.FullPath);
                w.Error += (_, _) => Emit(path, WatchEventKind.Error, path);
                _watchers[path] = w;
            }
            return Task.FromResult(Result<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<bool>.Fail(FileMindError.Io(ex.Message)));
        }
    }

    public Task<Result<bool>> StopWatchingAsync(string path, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_watchers.Remove(path, out var w)) return Task.FromResult(Result<bool>.Ok(false));
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (var w in _watchers.Values)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _watchers.Clear();
        }
    }

    private void Emit(string root, string kind, string path)
    {
        var ev = new WatchEvent(path, kind, DateTimeOffset.UtcNow);
        // drop silently only when the reader is gone; overflow is reported via
        // the OS-level Error event FileSystemWatcher raises on buffer loss
        _events.Writer.TryWrite(ev);
    }

    public void Dispose() => StopAll();
}

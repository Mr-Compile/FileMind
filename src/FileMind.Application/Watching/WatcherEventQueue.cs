using FileMind.Application.Ports;

namespace FileMind.Application.Watching;

/// <summary>
/// Bounded FIFO for watcher events. Dedupes by (path,kind) keeping the newest
/// timestamp; overflow flips <see cref="Overflowed"/> so callers rescan the
/// root instead of trusting the stream.
/// </summary>
public sealed class WatcherEventQueue(int capacity = 4096)
{
    private readonly LinkedList<QueuedWatchEvent> _items = new();
    private readonly Dictionary<(string Path, string Kind), LinkedListNode<QueuedWatchEvent>> _index = new();

    public int Count => _items.Count;
    public bool Overflowed { get; private set; }

    public bool TryEnqueue(WatchEvent ev, DateTimeOffset now, int attempts = 0)
    {
        var key = (ev.Path, ev.ChangeKind);
        if (_index.TryGetValue(key, out var existing))
        {
            _items.Remove(existing);
            _index.Remove(key);
        }

        if (_items.Count >= capacity)
        {
            var oldest = _items.First!;
            _items.RemoveFirst();
            _index.Remove((oldest.Value.Event.Path, oldest.Value.Event.ChangeKind));
            Overflowed = true;
        }

        var node = _items.AddLast(new QueuedWatchEvent(ev, now, attempts));
        _index[key] = node;
        return true;
    }

    public QueuedWatchEvent? Peek() => _items.First?.Value;

    public QueuedWatchEvent? Dequeue()
    {
        var first = _items.First;
        if (first is null) return null;
        _items.RemoveFirst();
        _index.Remove((first.Value.Event.Path, first.Value.Event.ChangeKind));
        return first.Value;
    }

    public void ClearOverflow() => Overflowed = false;
}

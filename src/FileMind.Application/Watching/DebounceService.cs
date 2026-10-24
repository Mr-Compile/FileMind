using FileMind.Application.Ports;

namespace FileMind.Application.Watching;

/// <summary>
/// Per-path debounce window. Pure/stateful (clock-driven) — an event becomes
/// due once no newer event for the same path arrived within the window.
/// </summary>
public sealed class DebounceService(IClock clock, TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromMilliseconds(750);
    private readonly Dictionary<string, WatchEvent> _pending = new(StringComparer.OrdinalIgnoreCase);

    public int PendingCount => _pending.Count;

    public void Add(WatchEvent ev) => _pending[ev.Path] = ev;

    /// <summary>Paths whose newest event is older than the debounce window.</summary>
    public IReadOnlyList<WatchEvent> CollectDue()
    {
        var due = _pending.Values
            .Where(e => clock.UtcNow - e.At >= _window)
            .ToList();
        foreach (var e in due) _pending.Remove(e.Path);
        return due;
    }
}

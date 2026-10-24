using FileMind.Application.Ports;
using System.Threading.Channels;

namespace FileMind.Platform.Windows;

/// <summary>
/// Removable-drive attach/detach stream via DriveInfo polling (no WMI —
/// deterministic, works under least privilege). Kinds: Attached / Removed.
/// </summary>
public sealed class WindowsRemovableStorageEvents(TimeSpan? pollInterval = null)
    : IRemovableStorageEvents
{
    private readonly TimeSpan _poll = pollInterval ?? TimeSpan.FromSeconds(2);
    private readonly Channel<DriveEvent> _events =
        Channel.CreateUnbounded<DriveEvent>(new UnboundedChannelOptions { SingleReader = true });
    private HashSet<string> _seen = CurrentRemovable();

    public static HashSet<string> CurrentRemovable() =>
        DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable)
            .Select(d => d.RootDirectory.FullName.TrimEnd('\\'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async IAsyncEnumerable<DriveEvent> EventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var now = CurrentRemovable();
            foreach (var attached in now.Except(_seen))
                _events.Writer.TryWrite(new DriveEvent(attached, "Attached", DateTimeOffset.UtcNow));
            foreach (var removed in _seen.Except(now))
                _events.Writer.TryWrite(new DriveEvent(removed, "Removed", DateTimeOffset.UtcNow));
            _seen = now;
            try { await Task.Delay(_poll, ct); }
            catch (OperationCanceledException) { yield break; }
            while (_events.Reader.TryRead(out var ev)) yield return ev;
        }
    }
}

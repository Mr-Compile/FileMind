using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Watching;

public enum StabilityVerdict
{
    Stable,
    StillChanging,
    IncompleteDownload,
    Missing,
    Unreadable
}

public sealed record StabilityResult(StabilityVerdict Verdict, string Detail, FileStat? Stat);

/// <summary>
/// Re-stats a path before acting: watcher events are hints only. A file is
/// stable when two consecutive polls show identical size+mtime and a read
/// probe succeeds. Known in-progress transfer extensions never pass.
/// </summary>
public sealed class FileStabilityChecker(
    IFileSystemAdapter fs,
    int pollCount = 2,
    TimeSpan? pollInterval = null)
{
    private readonly TimeSpan _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);

    public static readonly IReadOnlySet<string> IncompleteExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "crdownload", "part", "partial", "download", "tmp", "temp", "opdownload", "inprogress" };

    public static bool IsIncompleteExtension(string extension) =>
        IncompleteExtensions.Contains(extension.TrimStart('.'));

    public async Task<StabilityResult> CheckAsync(string path, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(path).TrimStart('.');
        if (IsIncompleteExtension(ext))
            return new StabilityResult(StabilityVerdict.IncompleteDownload,
                $"extension '.{ext}' marks an in-progress transfer", null);

        FileStat? prev = null;
        for (var i = 0; i < pollCount; i++)
        {
            var stat = await fs.StatAsync(path, ct);
            if (!stat.IsSuccess)
                return new StabilityResult(
                    stat.Error!.Code == ErrorCode.SourceNotFound
                        ? StabilityVerdict.Missing
                        : StabilityVerdict.Unreadable,
                    stat.Error.Message, null);

            if (stat.Value!.IsDirectory)
                return new StabilityResult(StabilityVerdict.Unreadable, "path is a directory", null);

            if (prev is not null &&
                (prev.SizeBytes != stat.Value.SizeBytes || prev.ModifiedAt != stat.Value.ModifiedAt))
                return new StabilityResult(StabilityVerdict.StillChanging,
                    $"size/mtime changed between polls ({prev.SizeBytes}->{stat.Value.SizeBytes} bytes)", stat.Value);

            prev = stat.Value;
            if (i + 1 < pollCount) await Task.Delay(_pollInterval, ct);
        }

        // write-lock probe: a file being written by another process fails to open
        var probe = await fs.OpenReadAsync(path, ct);
        if (!probe.IsSuccess)
            return new StabilityResult(StabilityVerdict.StillChanging,
                $"read probe failed: {probe.Error!.Message}", prev);
        await probe.Value!.DisposeAsync();

        return new StabilityResult(StabilityVerdict.Stable, "stable", prev);
    }
}

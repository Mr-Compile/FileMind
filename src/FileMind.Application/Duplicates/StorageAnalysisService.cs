using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Duplicates;

/// <summary>
/// Storage analysis over the file index: breakdown by FileKind and age band,
/// largest/oldest lists, duplicate-reclaimable total. Pure read-only analysis.
/// </summary>
public sealed class StorageAnalysisService(IFileIndexRepository index)
{
    private static readonly (string Label, Func<int, bool> Match)[] AgeBands =
    [
        ("0-30d", d => d <= 30),
        ("31-90d", d => d is > 30 and <= 90),
        ("91-365d", d => d is > 90 and <= 365),
        (">365d", d => d > 365),
    ];

    public async Task<Result<StorageReport>> AnalyzeAsync(
        string? pathPrefix = null, CancellationToken ct = default)
    {
        var all = await index.GetAllAsync(ct);
        if (!all.IsSuccess) return Result<StorageReport>.Fail(all.Error!);
        var now = DateTimeOffset.UtcNow;

        var files = all.Value!
            .Where(c => pathPrefix is null ||
                c.SourceUri.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var byKind = files
            .GroupBy(c => FileKindMap.FromExtension(c.Extension))
            .Select(g => new StorageBucket(g.Key.ToString(), g.Sum(x => x.SizeBytes), g.Count()))
            .OrderByDescending(b => b.Bytes).ToList();

        var byAge = AgeBands.Select(b => new StorageBucket(
            b.Label,
            files.Where(f => b.Match((int)(now - f.ModifiedAt).TotalDays)).Sum(f => f.SizeBytes),
            files.Count(f => b.Match((int)(now - f.ModifiedAt).TotalDays)))).ToList();

        StorageFileEntry Entry(FileCandidate c) => new(
            c.SourceUri, c.SizeBytes, c.ModifiedAt, (int)(now - c.ModifiedAt).TotalDays);

        var largest = files.OrderByDescending(f => f.SizeBytes).Take(10).Select(Entry).ToList();
        var oldest = files.OrderByDescending(f => (now - f.ModifiedAt).TotalDays).Take(10).Select(Entry).ToList();

        var dupeBytes = files
            .Where(f => f.ContentHash is not null)
            .GroupBy(f => f.ContentHash!)
            .Where(g => g.Count() >= 2)
            .Sum(g => g.Sum(f => f.SizeBytes) - g.Max(f => f.SizeBytes));

        return Result<StorageReport>.Ok(new StorageReport(
            files.Sum(f => f.SizeBytes), files.Count, byKind, byAge, largest, oldest, dupeBytes));
    }
}

namespace FileMind.Domain.Contracts;

/// <summary>A file inside a duplicate group, with the metadata needed for undo recipes.</summary>
public sealed record DuplicateFile(string Path, long SizeBytes, DateTimeOffset ModifiedAt);

/// <summary>A set of files sharing identical content (full-hash confirmed).</summary>
public sealed record DuplicateGroup(
    string ContentHash,
    long SizeBytes,
    /// <summary>Bytes reclaimable if all but one copy were removed. Advisory only.</summary>
    long ReclaimableBytes,
    IReadOnlyList<DuplicateFile> Files,
    /// <summary>False when only size+partial-hash matched (e.g. over hash cap) — review before trusting.</summary>
    bool Confirmed);

/// <summary>
/// A pair that looks similar without being an exact dupe — name overlap,
/// size proximity, same kind. Always review; never auto-acts.
/// </summary>
public sealed record SimilarityCandidate(
    string PathA,
    string PathB,
    double Score,
    IReadOnlyList<string> Reasons);

public sealed record StorageBucket(string Label, long Bytes, long Count);

public sealed record StorageFileEntry(
    string Path, long SizeBytes, DateTimeOffset ModifiedAt, int AgeDays);

public sealed record StorageReport(
    long TotalBytes,
    long FileCount,
    IReadOnlyList<StorageBucket> ByKind,
    IReadOnlyList<StorageBucket> ByAgeBand,
    IReadOnlyList<StorageFileEntry> Largest,
    IReadOnlyList<StorageFileEntry> Oldest,
    long DuplicateReclaimableBytes);

public sealed record DuplicatesReport(
    IReadOnlyList<DuplicateGroup> Groups,
    IReadOnlyList<SimilarityCandidate> Similar,
    IReadOnlyList<string> Warnings)
{
    public long TotalReclaimable => Groups.Sum(g => g.ReclaimableBytes);
}

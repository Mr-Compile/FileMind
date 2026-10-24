using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Indexing;

public sealed record IndexResult(int Indexed, int Hashed, IReadOnlyList<string> Warnings);

public sealed class FileIndexingService(
    IFileIndexRepository index,
    IHashService? hash = null,
    long hashSizeCapBytes = 64L * 1024 * 1024,
    ISearchIndex? searchIndex = null)
{
    public const long DefaultHashSizeCapBytes = 64L * 1024 * 1024;

    public async Task<Result<IndexResult>> IndexAsync(
        IReadOnlyList<FileCandidate> candidates, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var indexed = 0;
        var hashed = 0;

        foreach (var raw in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var candidate = raw;
            var fingerprint = FileIdentity.ComputeFingerprint(candidate.SizeBytes, candidate.ModifiedAt);

            var stableId = Guid.NewGuid().ToString("N");
            var matches = await index.FindByFingerprintAsync(fingerprint, ct);
            if (matches.IsSuccess && matches.Value!.Count > 0)
            {
                stableId = matches.Value!
                    .FirstOrDefault(m => string.Equals(m.PathHint, candidate.SourceUri,
                        StringComparison.OrdinalIgnoreCase))?.StableId
                    ?? matches.Value![0].StableId;
            }
            else if (!matches.IsSuccess)
            {
                warnings.Add($"{candidate.DisplayName}: stable-id lookup failed ({matches.Error!.Code}); new id assigned");
            }

            if (hash is not null && candidate.SizeBytes <= hashSizeCapBytes)
            {
                var h = await hash.HashAsync(candidate.SourceUri, full: false, ct);
                if (h.IsSuccess)
                {
                    candidate = candidate with
                    {
                        ContentHash = h.Value,
                        HashState = h.Value.StartsWith("sha256:", StringComparison.Ordinal)
                            ? HashState.Full
                            : HashState.Partial
                    };
                    hashed++;
                }
                else warnings.Add($"{candidate.DisplayName}: hash skipped ({h.Error!.Message})");
            }

            var identity = new FileIdentity
            {
                StableId = stableId,
                PathHint = candidate.SourceUri,
                Fingerprint = fingerprint
            };
            var upsert = await index.UpsertAsync(candidate, identity, ct);
            if (upsert.IsSuccess)
            {
                indexed++;
                if (searchIndex is not null)
                {
                    var fts = await searchIndex.IndexAsync(candidate, candidate.ExtractedTextRef, ct);
                    if (!fts.IsSuccess)
                        warnings.Add($"{candidate.DisplayName}: search index failed ({fts.Error!.Message})");
                }
            }
            else warnings.Add($"{candidate.DisplayName}: index failed ({upsert.Error!.Message})");
        }

        return Result<IndexResult>.Ok(new IndexResult(indexed, hashed, warnings));
    }
}

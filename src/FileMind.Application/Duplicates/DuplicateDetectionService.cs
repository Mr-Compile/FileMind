using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Duplicates;

/// <summary>
/// Exact-duplicate detection: size prefilter -> partial hash -> full hash.
/// Hashing is incremental — the file index supplies previously computed hashes
/// and is updated as deeper hashes complete. Files over the hash cap stay in
/// Confirmed=false groups (partial match only — review before trusting).
/// Similarity candidates use normalized-name overlap + size proximity and are
/// advisory only.
/// </summary>
public sealed class DuplicateDetectionService(
    IFileIndexRepository index,
    IHashService hash,
    IFileSystemAdapter fs,
    long maxFullHashBytes = 64L * 1024 * 1024)
{
    public async Task<Result<DuplicatesReport>> FindAsync(
        string? pathPrefix = null, CancellationToken ct = default)
    {
        var all = await index.GetAllAsync(ct);
        if (!all.IsSuccess) return Result<DuplicatesReport>.Fail(all.Error!);
        var warnings = new List<string>();

        var files = all.Value!
            .Where(c => pathPrefix is null ||
                c.SourceUri.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(c => c.SizeBytes > 0)
            .ToList();

        // 1) size prefilter
        var bySize = files.GroupBy(c => c.SizeBytes)
            .Where(g => g.Count() >= 2).ToList();

        var groups = new List<DuplicateGroup>();
        var hashedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sizeGroup in bySize)
        {
            ct.ThrowIfCancellationRequested();

            // 2) partial-hash pass (hash only what the index lacks)
            var byPartial = new Dictionary<string, List<FileCandidate>>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in sizeGroup)
            {
                var ph = await EnsureHashAsync(c, full: false, ct);
                if (ph is null) { warnings.Add($"{c.DisplayName}: skipped (unreadable or changed)"); continue; }
                hashedPaths.Add(c.SourceUri);
                (byPartial.TryGetValue(ph, out var l) ? l : byPartial[ph] = []).Add(c);
            }

            foreach (var (_, partialGroup) in byPartial.Where(kv => kv.Value.Count >= 2))
            {
                // 3) full-hash confirm — bounded by maxFullHashBytes
                if (partialGroup[0].SizeBytes <= maxFullHashBytes)
                {
                    var byFull = new Dictionary<string, List<FileCandidate>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in partialGroup)
                    {
                        var fh = await EnsureHashAsync(c, full: true, ct);
                        if (fh is null) { warnings.Add($"{c.DisplayName}: skipped (unreadable or changed)"); continue; }
                        (byFull.TryGetValue(fh, out var l) ? l : byFull[fh] = []).Add(c);
                    }
                    foreach (var (hash, g) in byFull.Where(kv => kv.Value.Count >= 2))
                        groups.Add(new DuplicateGroup(hash, g[0].SizeBytes,
                            g[0].SizeBytes * (g.Count - 1),
                            g.Select(x => new DuplicateFile(x.SourceUri, x.SizeBytes, x.ModifiedAt))
                                .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList(),
                            Confirmed: true));
                }
                else
                {
                    var partialKey = "partial:" + partialGroup[0].SizeBytes;
                    groups.Add(new DuplicateGroup(partialKey, partialGroup[0].SizeBytes,
                        partialGroup[0].SizeBytes * (partialGroup.Count - 1),
                        partialGroup.Select(x => new DuplicateFile(x.SourceUri, x.SizeBytes, x.ModifiedAt))
                            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList(),
                        Confirmed: false));
                    warnings.Add($"group of {partialGroup.Count} × {partialGroup[0].SizeBytes}B exceeds full-hash cap — partial match only");
                }
            }
        }

        // 4) similarity candidates (excluding exact dupe paths pairs)
        var dupePairs = groups.SelectMany(g => g.Files.Select(f => f.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var similar = FindSimilar(files.Where(f => !dupePairs.Contains(f.SourceUri)).ToList());

        return Result<DuplicatesReport>.Ok(new DuplicatesReport(
            groups.OrderByDescending(g => g.ReclaimableBytes).ToList(), similar, warnings));
    }

    private async Task<string?> EnsureHashAsync(FileCandidate c, bool full, CancellationToken ct)
    {
        // reuse index hash when deep enough; re-stat before hashing (index may drift)
        var have = c.HashState == HashState.Full ? 2 : c.HashState == HashState.Partial ? 1 : 0;
        if (have >= (full ? 2 : 1) && c.ContentHash is not null)
            return c.ContentHash;

        var stat = await fs.StatAsync(c.SourceUri, ct);
        if (!stat.IsSuccess || stat.Value!.IsDirectory) return null;
        if (stat.Value.SizeBytes != c.SizeBytes)
        {
            // changed since indexed — invalidate index row rather than trust it
            await index.RemoveAsync(c.SourceUri, ct);
            return null;
        }

        var h = await hash.HashAsync(c.SourceUri, full, ct);
        if (!h.IsSuccess) return null;

        // preserve the existing stable_id instead of minting a new one
        var fingerprint = FileIdentity.ComputeFingerprint(c.SizeBytes, c.ModifiedAt);
        var stableId = Guid.NewGuid().ToString("N");
        var matches = await index.FindByFingerprintAsync(fingerprint, ct);
        if (matches.IsSuccess && matches.Value!.Count > 0)
            stableId = matches.Value!
                .FirstOrDefault(m => string.Equals(m.PathHint, c.SourceUri, StringComparison.OrdinalIgnoreCase))
                ?.StableId ?? matches.Value![0].StableId;

        var updated = c with
        {
            ContentHash = h.Value,
            HashState = h.Value.StartsWith("sha256:", StringComparison.Ordinal)
                ? HashState.Full : HashState.Partial,
        };
        await index.UpsertAsync(updated,
            new FileIdentity { StableId = stableId, Fingerprint = fingerprint, PathHint = c.SourceUri }, ct);
        return h.Value;
    }

    private static IReadOnlyList<SimilarityCandidate> FindSimilar(List<FileCandidate> files)
    {
        var results = new List<SimilarityCandidate>();
        for (var i = 0; i < files.Count; i++)
        for (var j = i + 1; j < files.Count; j++)
        {
            var a = files[i];
            var b = files[j];
            var reasons = new List<string>();
            double score = 0;

            if (!a.Extension.Equals(b.Extension, StringComparison.OrdinalIgnoreCase)) continue;
            reasons.Add($"same type .{a.Extension}");
            score += 20;

            var ta = Normalizer.Normalize(Stem(a.DisplayName)).Tokens.ToHashSet();
            var tb = Normalizer.Normalize(Stem(b.DisplayName)).Tokens.ToHashSet();
            if (ta.Count == 0 || tb.Count == 0) continue;
            var overlap = ta.Intersect(tb).Count();
            var jaccard = (double)overlap / ta.Union(tb).Count();
            if (jaccard >= 0.5)
            {
                score += jaccard * 60;
                reasons.Add($"name overlap {jaccard:P0}");
            }
            else if (jaccard < 0.34) continue;

            var sizeRatio = a.SizeBytes > 0 && b.SizeBytes > 0
                ? Math.Min(a.SizeBytes, b.SizeBytes) / (double)Math.Max(a.SizeBytes, b.SizeBytes)
                : 0;
            if (sizeRatio >= 0.9)
            {
                score += 20;
                reasons.Add($"size within {sizeRatio:P0}");
            }
            else if (score < 40) continue;

            if (score >= 50)
                results.Add(new SimilarityCandidate(a.SourceUri, b.SourceUri,
                    Math.Round(score, 1), reasons));
        }
        return results.OrderByDescending(s => s.Score).Take(50).ToList();
    }

    private static string Stem(string name)
    {
        var d = name.LastIndexOf('.');
        return d > 0 ? name[..d] : name;
    }
}

using System.Text.RegularExpressions;
using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Learning;

/// <summary>
/// PLAN-013 M3 — detects version chains (doc_v1→v2, "report (2)", 2024→2025)
/// and same-directory related files across the file index. Output is
/// suggestion-shaped data only — callers present it, nothing acts on it.
/// </summary>
public sealed class RelatedFileService(IFileIndexRepository index)
{
    // v12, _v2, -v3 / " (2)" / trailing year 1990-2099
    private static readonly Regex VersionToken = new(
        @"(?:^|[_\-\s])v(\d{1,4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParenCopy = new(
        @"\s*\((\d{1,3})\)$", RegexOptions.Compiled);
    private static readonly Regex YearToken = new(
        @"(?:^|[_\-\s])((?:19|20)\d{2})$", RegexOptions.Compiled);

    private static (string Base, int Order, string Why, bool Versioned) Parse(string displayName)
    {
        var stem = Path.GetFileNameWithoutExtension(displayName);
        if (VersionToken.Match(stem) is { Success: true } m)
            return (VersionToken.Replace(stem, "").TrimEnd('_', '-', ' '), int.Parse(m.Groups[1].Value), $"v{m.Groups[1].Value}", true);
        if (ParenCopy.Match(stem) is { Success: true } p)
            return (ParenCopy.Replace(stem, ""), int.Parse(p.Groups[1].Value), $"copy ({p.Groups[1].Value})", true);
        if (YearToken.Match(stem) is { Success: true } y)
            return (YearToken.Replace(stem, "").TrimEnd('_', '-', ' '), int.Parse(y.Groups[1].Value), $"year {y.Groups[1].Value}", true);
        // unversioned same-base sibling — order 0, only counts when a
        // versioned sibling exists ("notes.txt" is the base of "notes (2)")
        return (stem, 0, "base", false);
    }

    /// <summary>All version chains in the index (≥2 members, same dir+ext+base).</summary>
    public async Task<Result<IReadOnlyList<VersionChain>>> FindVersionChainsAsync(CancellationToken ct = default)
    {
        var all = await index.GetAllAsync(ct);
        if (!all.IsSuccess) return Result<IReadOnlyList<VersionChain>>.Fail(all.Error!);

        var chains = new Dictionary<(string Dir, string Ext, string Base), List<(FileCandidate C, int Order, string Why, bool Versioned)>>();
        foreach (var c in all.Value!)
        {
            var parsed = Parse(c.DisplayName);
            var dir = (Path.GetDirectoryName(c.SourceUri) ?? "").ToLowerInvariant();
            var key = (Dir: dir, Ext: c.Extension.ToLowerInvariant(), Base: parsed.Base.ToLowerInvariant());
            if (key.Base.Length == 0) continue;
            (chains.TryGetValue(key, out var list) ? list : chains[key] = []).Add((c, parsed.Order, parsed.Why, parsed.Versioned));
        }

        var results = chains
            .Where(g => g.Value.Any(v => v.Versioned)
                && g.Value.Select(v => v.Order).Distinct().Count() >= 2)
            .Select(g =>
            {
                var ordered = g.Value.OrderBy(v => v.Order).ThenByDescending(v => v.C.ModifiedAt).ToList();
                return new VersionChain(
                    g.Key.Base,
                    ordered.Select(v => v.C.SourceUri).ToList(),
                    ordered[^1].C.SourceUri,
                    string.Join(" → ", ordered.Select(v => v.Why)));
            })
            .OrderBy(c => c.BaseName)
            .ToList();
        return Result<IReadOnlyList<VersionChain>>.Ok(results);
    }

    /// <summary>Chains containing <paramref name="path"/>, or all chains when null.</summary>
    public async Task<Result<IReadOnlyList<VersionChain>>> RelatedToAsync(string path, CancellationToken ct = default)
    {
        var chains = await FindVersionChainsAsync(ct);
        if (!chains.IsSuccess) return chains;
        var mine = chains.Value!
            .Where(c => c.PathsOrderedOldestToNewest.Any(
                p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return Result<IReadOnlyList<VersionChain>>.Ok(mine);
    }
}

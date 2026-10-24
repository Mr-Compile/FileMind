using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Search;

/// <summary>Structured search request. Null fields are unconstrained.</summary>
public sealed record SearchQuery(
    string? Text = null,
    string? NameContains = null,
    string? Extension = null,
    string? Kind = null,
    int? OlderThanDays = null,
    int? NewerThanDays = null,
    int Limit = 50);

public sealed record SearchResultItem(
    string Path,
    string DisplayName,
    string Kind,
    long SizeBytes,
    DateTimeOffset ModifiedAt,
    double Rank,
    bool TextMatched);

/// <summary>
/// Local-only search: FTS5 full-text via <see cref="ISearchIndex"/>, plus
/// structured filters (name/extension/kind/age) over the file index.
/// No provider in the retrieval path — an optional NL interpreter may be
/// added by PLAN-009 but retrieval degrades to plain FTS without it.
/// </summary>
public sealed class SearchUseCase(ISearchIndex search, IFileIndexRepository index)
{
    public async Task<Result<IReadOnlyList<SearchResultItem>>> RunAsync(
        SearchQuery query, CancellationToken ct = default)
    {
        var all = await index.GetAllAsync(ct);
        if (!all.IsSuccess) return Result<IReadOnlyList<SearchResultItem>>.Fail(all.Error!);
        var byPath = all.Value!.ToDictionary(c => c.SourceUri, StringComparer.OrdinalIgnoreCase);

        var rows = new List<(FileCandidate C, double Rank, bool TextHit)>();
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // over-fetch so post-filters still fill the requested page
            var hits = await search.SearchAsync(query.Text, Math.Max(query.Limit * 4, 64), ct);
            if (!hits.IsSuccess) return Result<IReadOnlyList<SearchResultItem>>.Fail(hits.Error!);
            foreach (var h in hits.Value!)
                if (byPath.TryGetValue(h.Path, out var c))
                    rows.Add((c, h.Rank, true));
        }
        else rows.AddRange(byPath.Values.Select(c => (c, 0.0, false)));

        IEnumerable<(FileCandidate C, double Rank, bool TextHit)> filtered = rows;
        if (!string.IsNullOrWhiteSpace(query.NameContains))
            filtered = filtered.Where(r => r.C.DisplayName.Contains(query.NameContains, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Extension))
        {
            var ext = query.Extension.TrimStart('.');
            filtered = filtered.Where(r => r.C.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(query.Kind) &&
            Enum.TryParse<FileKind>(query.Kind, ignoreCase: true, out var kind))
            filtered = filtered.Where(r => FileKindMap.FromExtension(r.C.Extension) == kind);
        var now = DateTimeOffset.UtcNow;
        if (query.OlderThanDays is { } od)
            filtered = filtered.Where(r => (now - r.C.ModifiedAt).TotalDays > od);
        if (query.NewerThanDays is { } nd)
            filtered = filtered.Where(r => (now - r.C.ModifiedAt).TotalDays <= nd);

        var items = filtered
            .OrderByDescending(r => r.TextHit ? -r.Rank : 0) // bm25: lower = better
            .ThenBy(r => r.C.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(r => new SearchResultItem(r.C.SourceUri, r.C.DisplayName,
                FileKindMap.FromExtension(r.C.Extension).ToString(),
                r.C.SizeBytes, r.C.ModifiedAt, r.Rank, r.TextHit))
            .ToList();
        return Result<IReadOnlyList<SearchResultItem>>.Ok(items);
    }
}

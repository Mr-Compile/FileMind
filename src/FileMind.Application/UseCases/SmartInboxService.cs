using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.UseCases;

public sealed record SmartInboxRecordResult(int Added, int AlreadyOpen, IReadOnlyList<string> Warnings);

public sealed class SmartInboxService(ISmartInboxRepository inbox)
{
    public async Task<Result<SmartInboxRecordResult>> RecordReviewAsync(
        IReadOnlyList<AnalyzedFile> analyzed, CancellationToken ct = default)
    {
        var open = await inbox.GetOpenAsync(ct);
        if (!open.IsSuccess) return Result<SmartInboxRecordResult>.Fail(open.Error!);
        var openPaths = open.Value!.Select(i => i.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var already = 0;
        var warnings = new List<string>();
        foreach (var a in analyzed)
        {
            var reviewDestined = a.Decision.Kind == DecisionKind.Review
                || a.Classification.NeedsReview;
            if (!reviewDestined) continue;

            var path = a.Candidate.SourceUri;
            if (!openPaths.Add(path)) { already++; continue; }

            var item = new SmartInboxItem(
                Guid.NewGuid().ToString("N"), path,
                a.Decision.Kind == DecisionKind.Review
                    ? string.Join("; ", a.Decision.Reasons)
                    : $"Confidence {a.Classification.Confidence:0} needs review before organizing.",
                a.Classification, DateTimeOffset.UtcNow, Resolved: false);
            var recorded = await inbox.AddAsync(item, ct);
            if (recorded.IsSuccess) added++;
            else warnings.Add($"{a.Candidate.DisplayName}: {recorded.Error!.Message}");
        }

        return Result<SmartInboxRecordResult>.Ok(new SmartInboxRecordResult(added, already, warnings));
    }
}

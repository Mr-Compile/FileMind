using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Learning;

/// <summary>
/// PLAN-013 M1 — records user corrections and mines them into draft rules.
/// Drafts are provenance-marked (RuleProvenance.Learned) and disabled;
/// activation goes through the normal rule review path — nothing
/// auto-activates, and enabled rules still pass through SafetyEngine.
/// </summary>
public sealed class CorrectionService(ICorrectionRepository corrections, IPathNormalizer paths)
{
    /// <summary>Minimum corrections agreeing on a destination before a suggestion is drafted.</summary>
    public const int DefaultMinCorrections = 2;

    /// <summary>An undo of an organize operation IS a correction signal.</summary>
    public async Task<Result<int>> ObserveUndoAsync(OperationRecord record, CancellationToken ct = default)
    {
        var recorded = 0;
        foreach (var item in record.Items)
        {
            ct.ThrowIfCancellationRequested();
            if (item.Operation != OperationType.Move || item.ResultingLocation is null) continue;
            var e = new CorrectionEvent(
                Guid.NewGuid().ToString("N"),
                item.OriginalLocation,
                item.ResultingLocation,
                item.OriginalLocation,
                $"undo:{record.OperationId}",
                DateTimeOffset.UtcNow);
            var saved = await corrections.AddAsync(e, ct);
            if (!saved.IsSuccess) return Result<int>.Fail(saved.Error!);
            recorded++;
        }
        return Result<int>.Ok(recorded);
    }

    public Task<Result<CorrectionEvent>> RecordAsync(
        string path, string chosenDestination, string source = "manual", CancellationToken ct = default) =>
        corrections.AddAsync(new CorrectionEvent(
            Guid.NewGuid().ToString("N"), path, null, chosenDestination, source,
            DateTimeOffset.UtcNow), ct);

    /// <summary>
    /// Groups corrections by (extension, chosen destination directory); groups
    /// reaching <paramref name="minCount"/> become disabled draft rules whose
    /// condition is the extension and whose action moves to the learned
    /// destination template. <paramref name="destinationRoot"/>, when given,
    /// makes emitted templates portable relative paths.
    /// </summary>
    public async Task<Result<IReadOnlyList<Rule>>> MineSuggestionsAsync(
        string? destinationRoot = null, int minCount = DefaultMinCorrections, CancellationToken ct = default)
    {
        var all = await corrections.GetAllAsync(ct);
        if (!all.IsSuccess) return Result<IReadOnlyList<Rule>>.Fail(all.Error!);

        var groups = new Dictionary<(string Ext, string DestDir), List<CorrectionEvent>>();
        foreach (var e in all.Value!)
        {
            var ext = Path.GetExtension(e.Path).TrimStart('.');
            if (ext.Length == 0) continue;
            var destDir = paths.DirectoryOf(e.ChosenDestination);
            if (destDir is null || destDir.Length == 0) continue;
            var key = (ext.ToLowerInvariant(), paths.Normalize(destDir));
            (groups.TryGetValue(key, out var list) ? list : groups[key] = []).Add(e);
        }

        var now = DateTimeOffset.UtcNow;
        var rules = new List<Rule>();
        foreach (var ((ext, destDir), events) in groups.OrderBy(g => g.Key.Ext))
        {
            if (events.Count < minCount) continue;
            var template = destinationRoot is not null
                ? paths.RelativeTo(destDir, destinationRoot) ?? destDir
                : destDir;
            rules.Add(new Rule
            {
                Id = $"learned-{ext}-{Math.Abs(destDir.GetHashCode()):x8}",
                Name = $"Learned: .{ext} → {destDir}",
                Enabled = false, // review-gated: user must enable explicitly
                Priority = 0,
                ConditionRoot = new ConditionGroup
                {
                    Operator = LogicalOperator.And,
                    Conditions = [new RuleCondition
                    {
                        Field = ConditionField.Extension,
                        Operator = ConditionOperator.Equals,
                        Value = ext
                    }]
                },
                Actions = [new RuleAction
                {
                    Kind = RuleActionKind.Move,
                    DestinationTemplate = template
                }],
                CreatedAt = now,
                UpdatedAt = now,
                SchemaVersion = 1,
                Provenance = RuleProvenance.Learned
            });
        }
        return Result<IReadOnlyList<Rule>>.Ok(rules);
    }
}

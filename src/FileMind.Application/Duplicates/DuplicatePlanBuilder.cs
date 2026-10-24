using FileMind.Domain.Contracts;

namespace FileMind.Application.Duplicates;

/// <summary>
/// Builds a review-first quarantine plan from confirmed duplicate groups.
/// Every item is a Move into a review area with KeepBoth — no file is ever
/// deleted, and unconfirmed (partial-only) groups are excluded entirely.
/// </summary>
public static class DuplicatePlanBuilder
{
    public static ActionPlan BuildQuarantinePlan(DuplicatesReport report, string reviewRoot, bool apply)
    {
        var items = report.Groups
            .Where(g => g.Confirmed)
            .SelectMany(g => g.Files.Skip(1).Select(f =>
            {
                var destPath = Path.Combine(reviewRoot, Path.GetFileName(f.Path));
                var fingerprint = FileIdentity.ComputeFingerprint(f.SizeBytes, f.ModifiedAt);
                return new ActionItem
                {
                    Operation = OperationType.Move,
                    Source = f.Path,
                    Destination = destPath,
                    ConflictPolicy = ConflictPolicy.KeepBoth,
                    Reason = $"exact duplicate of {g.Files[0].Path}",
                    Preconditions = [$"source-exists:{f.Path}", $"fingerprint:{fingerprint}"],
                    UndoRecipe = new UndoRecipe(
                        OperationType.Move, destPath, f.Path, fingerprint, [$"exists:{destPath}"]),
                };
            })).ToList();

        return new ActionPlan
        {
            PlanId = $"dupes-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedAt = DateTimeOffset.UtcNow,
            SourceDecision = "duplicates advisory",
            Items = items,
            RequiresReview = true,
            DryRun = !apply,
            ReversibleEstimate = true,
        };
    }
}

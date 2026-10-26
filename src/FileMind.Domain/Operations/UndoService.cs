using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Operations;

public sealed class UndoService(IFileSystemAdapter fs)
{
    public async Task<Result<ActionPlan>> BuildReversePlanAsync(OperationRecord record, CancellationToken ct = default)
    {
        // Undo what actually moved. NeedsAttention/Failed runs can still hold
        // committed items — partial results must be reversible too.
        var committed = record.Items.Where(i =>
            i.Status == OperationStatus.Committed && i.UndoRecipe is not null).ToList();
        if (committed.Count == 0)
            return Result<ActionPlan>.Fail(FileMindError.Validation(
                $"Operation {record.OperationId} has no committed items to undo (status={record.Status})."));

        var items = new List<ActionItem>();
        var blockers = new List<string>();
        var needsReview = false;

        foreach (var item in committed)
        {
            var recipe = item.UndoRecipe!;
            var current = await fs.StatAsync(recipe.ReverseSource, ct);
            if (!current.IsSuccess)
            {
                needsReview = true;
                blockers.Add($"'{recipe.ReverseSource}': {current.Error!.Message}");
                continue;
            }

            var fingerprint = FileIdentity.ComputeFingerprint(current.Value.SizeBytes, current.Value.ModifiedAt);
            if (fingerprint != recipe.Fingerprint)
            {
                needsReview = true;
                blockers.Add($"'{recipe.ReverseSource}' changed since the operation; refusing blind overwrite.");
                continue;
            }

            items.Add(new ActionItem
            {
                Operation = recipe.ReverseOperation,
                Source = recipe.ReverseSource,
                Destination = recipe.ReverseDestination,
                ConflictPolicy = ConflictPolicy.Ask,
                Reason = $"Undo of {record.OperationId} item {item.ItemId}",
                Preconditions = recipe.Preconditions
            });
        }

        if (items.Count == 0)
            return Result<ActionPlan>.Fail(FileMindError.Validation(
                $"No reversible items in operation {record.OperationId}. {string.Join("; ", blockers)}"));

        return Result<ActionPlan>.Ok(new ActionPlan
        {
            PlanId = $"undo-{record.OperationId}",
            CreatedAt = DateTimeOffset.UtcNow,
            SourceDecision = $"Undo:{record.OperationId}",
            Items = items,
            RequiresReview = needsReview,
            DryRun = false,
            ReversibleEstimate = false
        });
    }
}

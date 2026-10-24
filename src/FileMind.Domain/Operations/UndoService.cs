using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Operations;

public sealed class UndoService(IFileSystemAdapter fs)
{
    public async Task<Result<ActionPlan>> BuildReversePlanAsync(OperationRecord record, CancellationToken ct = default)
    {
        if (record.Status != OperationStatus.Committed)
            return Result<ActionPlan>.Fail(FileMindError.Validation(
                $"Operation {record.OperationId} is not committed (status={record.Status}); nothing to undo."));

        var items = new List<ActionItem>();
        var blockers = new List<string>();
        var needsReview = false;

        foreach (var item in record.Items)
        {
            if (item.UndoRecipe is null)
            {
                needsReview = true;
                blockers.Add($"Item '{item.ItemId}' has no undo recipe; manual review required.");
                continue;
            }

            var recipe = item.UndoRecipe;
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

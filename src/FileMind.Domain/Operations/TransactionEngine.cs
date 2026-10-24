using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;
using FileMind.Domain.Safety;

namespace FileMind.Domain.Operations;

public sealed class TransactionEngine(
    IFileSystemAdapter fs,
    IOperationStore store,
    SafetyEngine safety)
{
    public async Task<Result<OperationRecord>> ExecuteAsync(
        ActionPlan plan, SafetyContext context, CancellationToken ct = default)
    {
        var record = new OperationRecord
        {
            OperationId = Guid.NewGuid().ToString("N"),
            PlanId = plan.PlanId,
            Status = OperationStatus.Planned,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = plan.Items.Select((item, i) => new OperationItemState
            {
                ItemId = $"{plan.PlanId}-{i}",
                Operation = item.Operation,
                OriginalLocation = item.Source,
                Status = OperationStatus.Planned,
                UndoRecipe = item.UndoRecipe
            }).ToList()
        };

        var saved = await store.SaveAsync(record, ct);
        if (!saved.IsSuccess) return Result<OperationRecord>.Fail(saved.Error!);
        record = saved.Value!;

        record = await TransitionAsync(record, OperationStatus.Validating, ct);

        var safetyResult = await safety.ValidateAsync(plan, context, ct);
        if (!safetyResult.Allowed)
        {
            record = await FailAsync(record,
                FileMindError.Validation($"Safety blocked: {string.Join("; ", safetyResult.Blockers)}"), ct);
            return Result<OperationRecord>.Ok(record);
        }

        record = await TransitionAsync(record, OperationStatus.Executing, ct);
        var items = record.Items.ToList();

        for (var i = 0; i < plan.Items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var item = plan.Items[i];
            var itemState = items[i];
            var executed = await ExecuteItemAsync(item, ct);

            items[i] = executed.IsSuccess
                ? itemState with
                {
                    Status = OperationStatus.Committed,
                    ResultingLocation = executed.Value
                }
                : itemState with { Status = OperationStatus.Failed, Error = executed.Error };

            record = record with { Items = items };
            await store.SaveAsync(record, ct);
        }

        record = await TransitionAsync(record, OperationStatus.Verifying, ct);

        for (var i = 0; i < items.Count; i++)
        {
            var state = items[i];
            var planItem = plan.Items[i];
            if (state.Status != OperationStatus.Committed || state.ResultingLocation is null)
                continue;

            var fingerprint = planItem.UndoRecipe?.Fingerprint;
            var verify = fingerprint is not null
                ? await fs.VerifyAsync(state.ResultingLocation, fingerprint, ct)
                : await fs.ExistsAsync(state.ResultingLocation, ct);

            items[i] = verify is { IsSuccess: true, Value: true }
                ? state with { Verification = $"verified:{fingerprint ?? "exists"}" }
                : state with
                {
                    Status = OperationStatus.NeedsAttention,
                    Verification = "failed",
                    Error = verify.Error ?? FileMindError.Verification("Post-operation verification failed.")
                };
        }

        var anyFailed = items.Any(i => i.Status is OperationStatus.Failed or OperationStatus.NeedsAttention);
        record = (record with { Items = items });
        record = anyFailed
            ? await TransitionAsync(record, OperationStatus.NeedsAttention, ct)
            : await TransitionAsync(record, OperationStatus.Committed, ct, r => r with { CommittedAt = DateTimeOffset.UtcNow });

        return Result<OperationRecord>.Ok(record);
    }

    private async Task<Result<string>> ExecuteItemAsync(ActionItem item, CancellationToken ct) =>
        item.Operation switch
        {
            OperationType.Move => await fs.MoveAsync(item.Source, item.Destination!, item.ConflictPolicy, ct),
            OperationType.Copy => await fs.CopyAsync(item.Source, item.Destination!, item.ConflictPolicy, ct),
            OperationType.Rename => await fs.RenameAsync(item.Source, item.Destination!, item.ConflictPolicy, ct),
            OperationType.CreateFolder => ToPathResult(
                await fs.CreateDirectoryAsync(item.Destination!, ct), item.Destination!),
            OperationType.Ignore or OperationType.Ask or OperationType.Notify or OperationType.Tag =>
                Result<string>.Ok(item.Source),
            _ => Result<string>.Fail(new FileMindError(ErrorCode.UnsupportedOperation,
                $"Operation {item.Operation} not supported.", false, true))
        };

    private static Result<string> ToPathResult(Result<bool> result, string path) =>
        result.IsSuccess ? Result<string>.Ok(path) : Result<string>.Fail(result.Error);

    private async Task<OperationRecord> TransitionAsync(
        OperationRecord record, OperationStatus status, CancellationToken ct,
        Func<OperationRecord, OperationRecord>? mutate = null)
    {
        var next = (mutate ?? (r => r))(record with { Status = status });
        var saved = await store.SaveAsync(next, ct);
        return saved.IsSuccess ? saved.Value! : next;
    }

    private async Task<OperationRecord> FailAsync(OperationRecord record, FileMindError error, CancellationToken ct)
    {
        var failed = record with { Status = OperationStatus.Failed, Error = error };
        var saved = await store.SaveAsync(failed, ct);
        return saved.IsSuccess ? saved.Value! : failed;
    }
}

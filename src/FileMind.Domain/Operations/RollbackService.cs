using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Operations;

public sealed class RollbackService(
    IFileSystemAdapter fs,
    IOperationStore store)
{
    public async Task<Result<IReadOnlyList<OperationRecord>>> RecoverAsync(CancellationToken ct = default)
    {
        var incomplete = await store.LoadIncompleteAsync(ct);
        if (!incomplete.IsSuccess)
            return Result<IReadOnlyList<OperationRecord>>.Fail(incomplete.Error!);

        var results = new List<OperationRecord>();
        foreach (var record in incomplete.Value!)
            results.Add(await RecoverOneAsync(record, ct));

        return Result<IReadOnlyList<OperationRecord>>.Ok(results);
    }

    private async Task<OperationRecord> RecoverOneAsync(OperationRecord record, CancellationToken ct)
    {
        var reconciled = await ReconcileAsync(record, ct);
        var saved = await store.SaveAsync(reconciled, ct);
        return saved.IsSuccess ? saved.Value! : reconciled;
    }

    private async Task<OperationRecord> ReconcileAsync(OperationRecord record, CancellationToken ct)
    {
        var items = new List<OperationItemState>();
        var anyAttention = false;

        foreach (var item in record.Items)
        {
            if (item.Status == OperationStatus.Committed)
            {
                items.Add(item);
                continue;
            }

            var resulting = item.ResultingLocation;
            if (resulting is not null)
            {
                var destExists = await fs.ExistsAsync(resulting, ct);
                if (destExists is { IsSuccess: true, Value: true })
                {
                    var stat = await fs.StatAsync(resulting, ct);
                    var fingerprint = item.UndoRecipe?.Fingerprint;
                    var actual = stat.IsSuccess
                        ? FileIdentity.ComputeFingerprint(stat.Value.SizeBytes, stat.Value.ModifiedAt)
                        : null;

                    if (fingerprint is not null && actual == fingerprint)
                    {
                        items.Add(item with { Status = OperationStatus.Committed, Verification = "recovered:verified" });
                        continue;
                    }

                    items.Add(item with
                    {
                        Status = OperationStatus.NeedsAttention,
                        Error = FileMindError.Verification("Recovered result exists but fingerprint differs.")
                    });
                    anyAttention = true;
                    continue;
                }
            }

            var sourceExists = await fs.ExistsAsync(item.OriginalLocation, ct);
            if (sourceExists is { IsSuccess: true, Value: true })
            {
                items.Add(item with { Status = OperationStatus.RolledBack, Verification = "recovered:source-intact" });
                continue;
            }

            items.Add(item with
            {
                Status = OperationStatus.NeedsAttention,
                Error = FileMindError.Verification("Neither source nor result verifiable after crash.")
            });
            anyAttention = true;
        }

        var status = anyAttention
            ? OperationStatus.NeedsAttention
            : items.All(i => i.Status == OperationStatus.Committed)
                ? OperationStatus.Committed
                : OperationStatus.RolledBack;

        return record with { Items = items, Status = status };
    }
}

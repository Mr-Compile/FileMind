using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Recovery;

public sealed record RecoveryReport(
    int Reconciled,
    int FailedPristine,
    int NeedsAttention,
    IReadOnlyList<string> OperationIds,
    int JobsRequeued = 0);

public sealed class StartupRecoveryService(IOperationStore store, IJobRepository? jobs = null)
{
    public async Task<Result<RecoveryReport>> ReconcileAsync(CancellationToken ct = default)
    {
        var incomplete = await store.LoadIncompleteAsync(ct);
        if (!incomplete.IsSuccess) return Result<RecoveryReport>.Fail(incomplete.Error!);

        var failed = 0;
        var attention = 0;
        var ids = new List<string>();

        foreach (var record in incomplete.Value!)
        {
            ct.ThrowIfCancellationRequested();
            if (record.Status is OperationStatus.NeedsAttention or OperationStatus.Failed)
                continue; // already reconciled by a previous run
            var anyApplied = record.Items.Any(i => i.Status == OperationStatus.Committed);
            var ambiguous = anyApplied
                || record.Status is OperationStatus.Executing
                    or OperationStatus.Verifying or OperationStatus.RollingBack;

            var next = ambiguous
                ? record with
                {
                    Status = OperationStatus.NeedsAttention,
                    Error = record.Error ?? new FileMindError(ErrorCode.VerificationFailed,
                        "Operation interrupted; some items may have been applied — review before undo.",
                        RetrySafe: false, RequiresReview: true)
                }
                : record with
                {
                    Status = OperationStatus.Failed,
                    Error = record.Error ?? new FileMindError(ErrorCode.Cancelled,
                        "Operation interrupted by shutdown before any file was changed.",
                        RetrySafe: false, RequiresReview: false)
                };

            var saved = await store.SaveAsync(next, ct);
            if (!saved.IsSuccess) return Result<RecoveryReport>.Fail(saved.Error!);

            if (ambiguous) attention++; else failed++;
            ids.Add(record.OperationId);
        }

        var requeued = 0;
        if (jobs is not null)
        {
            var running = await jobs.GetByStatusAsync("running", ct);
            if (!running.IsSuccess) return Result<RecoveryReport>.Fail(running.Error!);
            foreach (var job in running.Value!)
            {
                ct.ThrowIfCancellationRequested();
                var reset = await jobs.UpdateStatusAsync(job.Id, "pending", ct);
                if (!reset.IsSuccess) return Result<RecoveryReport>.Fail(reset.Error!);
                requeued++;
            }
        }

        return Result<RecoveryReport>.Ok(new RecoveryReport(ids.Count, failed, attention, ids, requeued));
    }
}

using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Infrastructure.Sqlite;

/// <summary>
/// IJobQueue over IJobRepository. Dequeue atomically claims the oldest pending
/// job (status pending -> running). Jobs survive restart; a crash leaves
/// 'running' jobs for StartupRecoveryService to requeue.
/// </summary>
public sealed class SqliteJobQueue(IJobRepository repo) : IJobQueue
{
    public Task<Result<Job>> EnqueueAsync(JobRequest request, CancellationToken ct = default) =>
        repo.EnqueueAsync(request, ct);

    public async Task<Result<Job?>> DequeueAsync(CancellationToken ct = default)
    {
        var pending = await repo.GetPendingAsync(ct);
        if (!pending.IsSuccess) return Result<Job?>.Fail(pending.Error!);
        var job = pending.Value!.FirstOrDefault();
        if (job is null) return Result<Job?>.Ok(null);
        var claimed = await repo.UpdateStatusAsync(job.Id, "running", ct);
        if (!claimed.IsSuccess) return Result<Job?>.Fail(claimed.Error!);
        return Result<Job?>.Ok(job with { Status = "running" });
    }

    public Task<Result<bool>> CompleteAsync(string jobId, CancellationToken ct = default) =>
        repo.UpdateStatusAsync(jobId, "done", ct);

    public Task<Result<bool>> FailAsync(string jobId, FileMindError error, CancellationToken ct = default) =>
        repo.UpdateStatusAsync(jobId, $"failed:{error.Code}", ct);
}

using System.Text.Json;
using FileMind.Application.Decisions;
using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Watching;

public sealed record WatchRunResult(
    int EventsReceived,
    int EventsProcessed,
    int ActionsTaken,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Drive loop: event source -> bounded queue -> debounce -> orchestrator.
/// When an <see cref="IJobQueue"/> is supplied each due event is persisted as a
/// job before processing: the job is claimed (pending -> running), completed on
/// success, failed + scheduled for bounded backoff retry on RetrySafe errors.
/// A crash mid-processing leaves a 'running' job which StartupRecoveryService
/// requeues for the next watch run. Overflow triggers a rescan warning rather
/// than silent loss.
/// </summary>
public sealed class WatchService(
    IWatcherEventSource source,
    WatcherEventQueue queue,
    DebounceService debounce,
    WatchOrchestrator orchestrator,
    IClock clock,
    int pollMs = 200,
    IJobQueue? jobs = null,
    int maxRetries = 3)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromMilliseconds(Math.Min(250.0 * Math.Pow(2, attempts), 5000));

    public async Task<Result<WatchRunResult>> RunAsync(
        OrganizationMode mode, bool apply,
        Func<WatchEvent, (OrganizationMode Mode, bool Apply, string? SourceRoot, bool PreserveTree)>? router = null,
        CancellationToken ct = default)
    {
        var received = 0;
        var processed = 0;
        var actions = 0;
        var warnings = new List<string>();
        var retryAt = new PriorityQueue<WatchJobPayload, DateTimeOffset>();

        var pump = Task.Run(async () =>
        {
            await foreach (var ev in source.EventsAsync(ct))
            {
                received++;
                queue.TryEnqueue(ev, clock.UtcNow);
            }
        }, ct);

        while (!ct.IsCancellationRequested)
        {
            while (queue.Dequeue() is { } q)
            {
                debounce.Add(q.Event);
            }

            if (queue.Overflowed)
            {
                warnings.Add("watcher queue overflow — events dropped; rescan watched roots");
                queue.ClearOverflow();
            }

            foreach (var due in debounce.CollectDue())
            {
                var (m, a, sr, pt) = router?.Invoke(due) ?? (mode, apply, null, false);
                if (jobs is null)
                {
                    processed++;
                    var result = await orchestrator.ProcessEventAsync(due, m, a, sr, pt, ct);
                    if (!result.IsSuccess)
                    {
                        warnings.Add($"{due.Path}: {result.Error!.Message}");
                        continue;
                    }
                    actions += result.Value!.Actions.Count;
                    warnings.AddRange(result.Value.Warnings);
                }
                else
                {
                    var payload = new WatchJobPayload(due.Path, due.ChangeKind, m.ToString(), a,
                        SourceRoot: sr, PreserveTree: pt);
                    var enq = await jobs.EnqueueAsync(new JobRequest(
                        WatchJobPayload.JobType, JsonSerializer.Serialize(payload, JsonOpts)), ct);
                    if (!enq.IsSuccess) warnings.Add($"{due.Path}: job persist failed — {enq.Error!.Message}");
                }
            }

            if (jobs is not null)
            {
                // flush retry-scheduled payloads whose backoff has elapsed
                while (retryAt.TryPeek(out var p, out var due) && due <= clock.UtcNow)
                {
                    retryAt.Dequeue();
                    var enq = await jobs.EnqueueAsync(new JobRequest(
                        WatchJobPayload.JobType, JsonSerializer.Serialize(p, JsonOpts)), ct);
                    if (!enq.IsSuccess) warnings.Add($"{p.Path}: retry persist failed — {enq.Error!.Message}");
                }

                var deq = await jobs.DequeueAsync(ct);
                while (deq.IsSuccess && deq.Value is { } job)
                {
                    WatchJobPayload? p = null;
                    try { p = JsonSerializer.Deserialize<WatchJobPayload>(job.Request.Payload, JsonOpts); }
                    catch (JsonException) { }
                    if (p is null)
                    {
                        await jobs.FailAsync(job.Id,
                            new FileMindError(ErrorCode.ValidationFailed,
                                "unparseable watch job payload", RetrySafe: false, RequiresReview: true), ct);
                        deq = await jobs.DequeueAsync(ct);
                        continue;
                    }

                    processed++;
                    var jobMode = ModePolicy.TryParse(p.Mode, out var jm) ? jm : mode;
                    var ev = new WatchEvent(p.Path, p.ChangeKind, clock.UtcNow);
                    var result = await orchestrator.ProcessEventAsync(ev, jobMode, p.Apply,
                        p.SourceRoot, p.PreserveTree, ct);
                    if (result.IsSuccess)
                    {
                        await jobs.CompleteAsync(job.Id, ct);
                        actions += result.Value!.Actions.Count;
                        warnings.AddRange(result.Value.Warnings);
                    }
                    else
                    {
                        await jobs.FailAsync(job.Id, result.Error!, ct);
                        if (result.Error!.RetrySafe && p.Attempts < maxRetries)
                        {
                            retryAt.Enqueue(p with { Attempts = p.Attempts + 1 },
                                clock.UtcNow + Backoff(p.Attempts));
                            warnings.Add($"{p.Path}: {result.Error.Message} — retry {p.Attempts + 1}/{maxRetries} scheduled");
                        }
                        else
                        {
                            warnings.Add($"{p.Path}: {result.Error!.Message}");
                        }
                    }
                    deq = await jobs.DequeueAsync(ct);
                }
                if (!deq.IsSuccess) warnings.Add($"job dequeue failed: {deq.Error!.Message}");
            }

            try { await Task.Delay(pollMs, ct); }
            catch (OperationCanceledException) { break; }
        }

        return Result<WatchRunResult>.Ok(
            new WatchRunResult(received, processed, actions, warnings));
    }
}

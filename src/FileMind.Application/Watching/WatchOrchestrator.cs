using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.UseCases;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Application.Watching;

public sealed record WatchAction(string Path, string Route, string Detail);
public sealed record WatchCycleResult(
    IReadOnlyList<WatchAction> Actions,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Event → stability gate → FileCandidate → index → analyze → mode policy →
/// execute through OrganizerPipeline or Smart Inbox. Watcher events are hints;
/// everything is re-stat'ed before any mutation.
/// </summary>
public sealed class WatchOrchestrator(
    OrganizerPipeline pipeline,
    FileIndexingService indexing,
    SmartInboxService inbox,
    IPathNormalizer paths,
    FileStabilityChecker stability,
    IFileIndexRepository fileIndex,
    INotificationService? notify = null,
    string? destinationRoot = null)
{
    public async Task<Result<WatchCycleResult>> ProcessEventAsync(
        WatchEvent ev, OrganizationMode mode, bool apply = false, CancellationToken ct = default)
    {
        var actions = new List<WatchAction>();
        var warnings = new List<string>();
        var kind = WatchEventKind.Normalize(ev.ChangeKind);

        if (kind == WatchEventKind.Error)
        {
            warnings.Add($"watcher error at {ev.Path}: overflow or source failure — rescan required");
            return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
        }

        if (kind == WatchEventKind.Deleted)
        {
            var removed = await fileIndex.RemoveAsync(ev.Path, ct);
            if (removed.IsSuccess && removed.Value)
                actions.Add(new WatchAction(ev.Path, "IndexRemove", "removed from file index"));
            return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
        }

        var stable = await stability.CheckAsync(ev.Path, ct);
        switch (stable.Verdict)
        {
            case StabilityVerdict.Missing:
                warnings.Add($"{ev.Path}: disappeared before stability check");
                return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
            case StabilityVerdict.IncompleteDownload:
            case StabilityVerdict.StillChanging:
                warnings.Add($"{ev.Path}: {stable.Detail} — skipped (will re-evaluate on next event)");
                return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
            case StabilityVerdict.Unreadable:
                warnings.Add($"{ev.Path}: {stable.Detail}");
                return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
        }

        var stat = stable.Stat!;
        var candidate = new FileCandidate
        {
            ScanId = $"watch-{ev.At:yyyyMMddHHmmss}",
            SourceUri = stat.Path,
            DisplayName = paths.FileName(stat.Path),
            Extension = Path.GetExtension(stat.Path).TrimStart('.'),
            SizeBytes = stat.SizeBytes,
            CreatedAt = stat.CreatedAt,
            ModifiedAt = stat.ModifiedAt,
            SourceScope = SourceScope.WatchedFolder
        };

        var indexed = await indexing.IndexAsync([candidate], ct);
        if (!indexed.IsSuccess) warnings.Add($"{ev.Path}: index failed ({indexed.Error!.Message})");

        var analyzed = await pipeline.AnalyzeAsync([candidate], ct);
        if (!analyzed.IsSuccess) return Result<WatchCycleResult>.Fail(analyzed.Error!);
        var file = analyzed.Value![0];

        var policy = new ModePolicy(mode);
        var route = policy.RouteFor(file, apply);
        switch (route)
        {
            case ItemRoute.Skip:
                actions.Add(new WatchAction(ev.Path, "Skip", file.Decision.Kind.ToString()));
                break;
            case ItemRoute.SmartInbox:
            case ItemRoute.PreviewOnly:
            {
                var recorded = await inbox.RecordReviewAsync([file], ct);
                if (recorded.IsSuccess && recorded.Value!.Added > 0)
                    actions.Add(new WatchAction(ev.Path, "SmartInbox",
                        $"routed to Smart Inbox ({string.Join("; ", file.Decision.Reasons)})"));
                else if (route == ItemRoute.PreviewOnly)
                    actions.Add(new WatchAction(ev.Path, "PreviewOnly",
                        "manual mode — would organize on --apply"));
                else
                    actions.Add(new WatchAction(ev.Path, "SmartInbox", "already open in Smart Inbox"));
                await Notify("Smart Inbox item", $"{file.Candidate.DisplayName}: needs review", null, ct);
                break;
            }
            case ItemRoute.Execute:
            {
                var dest = destinationRoot ?? paths.Combine(paths.DirectoryOf(ev.Path) ?? ev.Path, "organized");
                var plan = pipeline.Plan([file], dryRun: false, destinationRoot: dest);
                if (!plan.IsSuccess) return Result<WatchCycleResult>.Fail(plan.Error!);
                var check = await pipeline.DryRunAsync(plan.Value!, ct);
                if (!check.IsSuccess || !check.Value!.Allowed)
                {
                    var blockers = check.IsSuccess
                        ? string.Join("; ", check.Value!.Blockers)
                        : check.Error!.Message;
                    warnings.Add($"{ev.Path}: safety blocked ({blockers}) — routed to review");
                    await inbox.RecordReviewAsync([file], ct);
                    break;
                }
                var executed = await pipeline.ExecuteAsync(plan.Value!, ct);
                if (!executed.IsSuccess) return Result<WatchCycleResult>.Fail(executed.Error!);
                actions.Add(new WatchAction(ev.Path, "Execute",
                    $"operation {executed.Value!.OperationId}: {executed.Value.Status}"));
                await Notify("File organized",
                    $"{file.Candidate.DisplayName} -> {executed.Value.Status}",
                    executed.Value.OperationId, ct);
                break;
            }
        }

        return Result<WatchCycleResult>.Ok(new WatchCycleResult(actions, warnings));
    }

    private Task Notify(string title, string body, string? opId = null, CancellationToken ct = default) =>
        notify is null ? Task.CompletedTask : notify.NotifyAsync(new Notification(title, body, opId), ct);
}

using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Scanning;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;

namespace FileMind.Application.UseCases;

public sealed record AnalyzedFile(
    FileCandidate Candidate,
    ClassificationResult Classification,
    RuleEvaluation Rules,
    Decision Decision);

public sealed class OrganizerPipeline(
    FileScanner scanner,
    ClassifierEngine classifier,
    RuleEngine ruleEngine,
    DecisionMaker decisionMaker,
    ActionPlanner planner,
    SafetyEngine safety,
    TransactionEngine transaction,
    UndoService undo,
    IFileSystemAdapter fs,
    IPathNormalizer paths,
    IProtectedPathProvider protectedPaths,
    IRuleRepository rules,
    ICollectionRepository collections,
    IOperationRepository operations,
    IOperationStore operationStore,
    FileIndexingService? indexing = null,
    ICandidateEnricher? enricher = null,
    IAiAdvisor? advisor = null,
    int enrichmentTextLimit = 8192)
{
    public async IAsyncEnumerable<FileCandidate> ScanAsync(
        string root, bool recursive = true,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var c in scanner.ScanAsync(root, recursive, ct).WithCancellation(ct))
            yield return c;
    }

    public async Task<Result<IReadOnlyList<AnalyzedFile>>> AnalyzeAsync(
        IReadOnlyList<FileCandidate> candidates, CancellationToken ct = default)
    {
        var ruleList = await rules.GetAllAsync(ct);
        if (!ruleList.IsSuccess) return Result<IReadOnlyList<AnalyzedFile>>.Fail(ruleList.Error!);
        var collectionList = await collections.GetAllAsync(ct);
        if (!collectionList.IsSuccess) return Result<IReadOnlyList<AnalyzedFile>>.Fail(collectionList.Error!);

        var results = new List<AnalyzedFile>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var effective = candidate;
            IReadOnlyList<Evidence>? extraEvidence = null;
            NormalizedTokens? contentTokens = null;
            IReadOnlyList<string>? enrichWarnings = null;

            if (enricher is not null)
            {
                var enr = await enricher.EnrichAsync(candidate, fs, ct);
                if (enr.IsSuccess && enr.Value is { } ev)
                {
                    effective = candidate with
                    {
                        Metadata = ev.Metadata,
                        ExtractedTextRef = ev.ExtractedText ?? candidate.ExtractedTextRef,
                    };
                    extraEvidence = ev.Evidence;
                    enrichWarnings = ev.Warnings;
                    if (ev.ExtractedText is { } t)
                        contentTokens = Normalizer.Normalize(t[..Math.Min(t.Length, enrichmentTextLimit)]);
                }
                else if (!enr.IsSuccess)
                {
                    enrichWarnings = [$"enrichment failed: {enr.Error!.Message}"];
                }
            }

            var classification = classifier.Classify(effective, extraEvidence, contentTokens);
            if (enrichWarnings is { Count: > 0 })
                classification = classification with
                {
                    Warnings = [.. classification.Warnings, .. enrichWarnings],
                };
            if (advisor is not null)
            {
                var assisted = await advisor.AssistAsync(effective, classification, ct);
                if (assisted.IsSuccess && assisted.Value is { } rescored)
                    classification = rescored;
            }
            var evaluation = ruleEngine.Evaluate(effective, classification, ruleList.Value);
            var decision = decisionMaker.Decide(effective, classification, evaluation, collectionList.Value);
            results.Add(new AnalyzedFile(effective, classification, evaluation, decision));
        }
        return Result<IReadOnlyList<AnalyzedFile>>.Ok(results);
    }

    public Result<ActionPlan> Plan(IReadOnlyList<AnalyzedFile> analyzed, bool dryRun,
        string? destinationRoot = null, DateTimeOffset? now = null,
        string? sourceRoot = null, bool preserveTree = false)
    {
        var items = new List<ActionItem>();
        var requiresReview = false;

        Func<string, string>? destBase = destinationRoot is null
            ? null
            : rel => paths.Combine(destinationRoot, rel);

        foreach (var a in analyzed)
        {
            var planned = planner.Plan(a.Decision, new PlanContext(paths, destBase, now, sourceRoot, preserveTree));
            if (!planned.IsSuccess) return Result<ActionPlan>.Fail(planned.Error!);
            items.AddRange(planned.Value.Items);
            requiresReview |= planned.Value.RequiresReview;
        }

        return Result<ActionPlan>.Ok(new ActionPlan
        {
            PlanId = Guid.NewGuid().ToString("N"),
            CreatedAt = now ?? DateTimeOffset.UtcNow,
            SourceDecision = "OrganizerPipeline",
            Items = items,
            RequiresReview = requiresReview,
            DryRun = dryRun,
            ReversibleEstimate = items.All(i => i.UndoRecipe is not null)
        });
    }

    public async Task<Result<SafetyResult>> DryRunAsync(ActionPlan plan, CancellationToken ct = default)
    {
        var context = await BuildSafetyContextAsync(plan, ct);
        var result = await safety.ValidateAsync(plan, context, ct);
        return Result<SafetyResult>.Ok(result);
    }

    public async Task<Result<OperationRecord>> ExecuteAsync(ActionPlan plan, CancellationToken ct = default)
    {
        var context = await BuildSafetyContextAsync(plan, ct);
        return await transaction.ExecuteAsync(plan, context, ct);
    }

    public async Task<Result<OperationRecord>> UndoAsync(string operationId, CancellationToken ct = default)
    {
        var loaded = await operationStore.LoadAsync(operationId, ct);
        if (!loaded.IsSuccess) return Result<OperationRecord>.Fail(loaded.Error!);

        var reverse = await undo.BuildReversePlanAsync(loaded.Value, ct);
        if (!reverse.IsSuccess) return Result<OperationRecord>.Fail(reverse.Error!);

        return await ExecuteAsync(reverse.Value, ct);
    }

    public Task<Result<IReadOnlyList<OperationRecord>>> RecentOperationsAsync(int limit, CancellationToken ct = default) =>
        operations.GetRecentAsync(limit, ct);

    public async Task<Result<IndexResult>> IndexAsync(
        IReadOnlyList<FileCandidate> candidates, CancellationToken ct = default) =>
        indexing is null
            ? Result<IndexResult>.Ok(new IndexResult(0, 0, []))
            : await indexing.IndexAsync(candidates, ct);

    private async Task<SafetyContext> BuildSafetyContextAsync(ActionPlan plan, CancellationToken ct)
    {
        var capabilities = new Dictionary<string, PermissionCapabilities>(StringComparer.OrdinalIgnoreCase);
        var freeSpace = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in plan.Items)
        {
            if (!capabilities.ContainsKey(item.Source))
            {
                var caps = await fs.CheckCapabilitiesAsync(item.Source, ct);
                if (caps.IsSuccess) capabilities[item.Source] = caps.Value;
            }
            if (item.Destination is not null)
            {
                var root = DestinationRoot(item.Destination);
                if (root is not null && !freeSpace.ContainsKey(item.Destination))
                {
                    var free = await fs.GetFreeSpaceAsync(root, ct);
                    if (free.IsSuccess) freeSpace[item.Destination] = free.Value;
                }
            }
        }

        return new SafetyContext(protectedPaths,
            capabilities.Count > 0 ? capabilities : null,
            freeSpace.Count > 0 ? freeSpace : null);
    }

    private string? DestinationRoot(string destination)
    {
        var normalized = paths.Normalize(destination);
        var i = normalized.IndexOf(':');
        return i == 1 ? normalized[..2] : null;
    }
}

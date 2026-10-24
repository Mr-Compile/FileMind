using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Operations;

public sealed record PlanContext(
    IPathNormalizer Paths,
    Func<string, string>? DestinationBase = null,
    DateTimeOffset? Now = null,
    string? SourceRoot = null,
    bool PreserveTree = false);

public sealed class ActionPlanner
{
    public Result<ActionPlan> Plan(Decision decision, PlanContext context)
    {
        if (decision.Kind is DecisionKind.NoOp or DecisionKind.Ignore)
        {
            return Result<ActionPlan>.Ok(new ActionPlan
            {
                PlanId = NewId(),
                CreatedAt = context.Now ?? DateTimeOffset.UtcNow,
                SourceDecision = decision.Kind.ToString(),
                Items = [],
                RequiresReview = false,
                DryRun = false,
                ReversibleEstimate = true
            });
        }

        if (decision.Kind == DecisionKind.Review)
        {
            return Result<ActionPlan>.Ok(new ActionPlan
            {
                PlanId = NewId(),
                CreatedAt = context.Now ?? DateTimeOffset.UtcNow,
                SourceDecision = "Review",
                Items = [new ActionItem
                {
                    Operation = OperationType.Ask,
                    Source = decision.Candidate.SourceUri,
                    Reason = string.Join("; ", decision.Reasons.DefaultIfEmpty("Low confidence or conflicts")),
                    ConflictPolicy = ConflictPolicy.Ask
                }],
                RequiresReview = true,
                DryRun = false,
                ReversibleEstimate = true
            });
        }

        var relDir = RelativeDirOf(decision.Candidate, context);
        var destination = ResolveTemplate(
            decision.DestinationTemplate ?? "{Collection}/{FileKind}",
            decision.Candidate, decision.Classification, context, relDir);
        // Category-then-tree: source-relative directory appended inside the
        // resolved category template, before DestinationBase prepends the root.
        if (context.PreserveTree && relDir.Length > 0)
            destination = context.Paths.Combine(destination, relDir);
        if (context.DestinationBase is not null)
            destination = context.DestinationBase(destination);
        var newName = decision.NamingTemplate is not null
            ? ResolveTemplate(decision.NamingTemplate, decision.Candidate, decision.Classification, context, "")
            : decision.Candidate.DisplayName;

        var destPath = context.Paths.Combine(destination, newName);
        var reason = string.Join("; ", decision.Reasons.DefaultIfEmpty(decision.Classification.Explanation));

        var item = new ActionItem
        {
            Operation = OperationType.Move,
            Source = decision.Candidate.SourceUri,
            Destination = destPath,
            ConflictPolicy = decision.ConflictPolicy,
            Reason = reason,
            Preconditions =
            [
                $"source-exists:{decision.Candidate.SourceUri}",
                $"fingerprint:{FileIdentity.ComputeFingerprint(decision.Candidate.SizeBytes, decision.Candidate.ModifiedAt)}"
            ],
            UndoRecipe = new UndoRecipe(
                OperationType.Move,
                destPath,
                decision.Candidate.SourceUri,
                FileIdentity.ComputeFingerprint(decision.Candidate.SizeBytes, decision.Candidate.ModifiedAt),
                [$"exists:{destPath}"])
        };

        return Result<ActionPlan>.Ok(new ActionPlan
        {
            PlanId = NewId(),
            CreatedAt = context.Now ?? DateTimeOffset.UtcNow,
            SourceDecision = decision.Kind.ToString(),
            Items = [item],
            RequiresReview = decision.Classification.NeedsReview,
            DryRun = false,
            ReversibleEstimate = true
        });
    }

    public ActionPlan ApplyTemplates(ActionPlan plan, PlanContext context) => plan;

    /// <summary>Source directory relative to SourceRoot; "" when unknown/unset.</summary>
    private static string RelativeDirOf(FileCandidate c, PlanContext ctx)
    {
        if (ctx.SourceRoot is null) return "";
        var dir = ctx.Paths.DirectoryOf(c.SourceUri);
        if (dir is null) return "";
        var rel = ctx.Paths.RelativeTo(dir, ctx.SourceRoot);
        return rel is null or "" or "." ? "" : rel;
    }

    private static string ResolveTemplate(string template, FileCandidate c, ClassificationResult r, PlanContext ctx, string relativeDir)
    {
        var now = ctx.Now ?? DateTimeOffset.UtcNow;
        return template
            .Replace("{Collection}", r.Collection ?? "Unsorted")
            .Replace("{Category}", r.Category ?? "General")
            .Replace("{Subcategory}", r.Subcategory ?? "General")
            .Replace("{Subject}", r.Subject ?? "Unsorted")
            .Replace("{Project}", r.Project ?? "General")
            .Replace("{FileKind}", r.FileKind.ToString())
            .Replace("{Name}", StemOf(ctx.Paths.FileName(c.DisplayName)))
            .Replace("{Ext}", c.Extension.TrimStart('.'))
            .Replace("{YYYY}", now.ToString("yyyy"))
            .Replace("{MM}", now.ToString("MM"))
            .Replace("{DD}", now.ToString("dd"))
            // Directory token: naming templates pass "" — embedding path
            // separators in a file name is never valid.
            .Replace("{RelativeDir}", relativeDir)
            // Extraction tokens: populated only when local intelligence wrote
            // reliable metadata — absence falls back to neutral values.
            .Replace("{Author}", MetaOr(c, "author", "Unknown"))
            .Replace("{Title}", MetaOr(c, "title", StemOf(ctx.Paths.FileName(c.DisplayName))))
            .Replace("{Year}", MetaOr(c, "year", now.ToString("yyyy")));
    }

    private static string MetaOr(FileCandidate c, string key, string fallback) =>
        c.Metadata.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    private static string StemOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}

using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Domain.Operations;

public sealed record PlanContext(
    IPathNormalizer Paths,
    Func<string, string>? DestinationBase = null,
    DateTimeOffset? Now = null);

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

        var destination = ResolveTemplate(
            decision.DestinationTemplate ?? "{Collection}/{FileKind}",
            decision.Candidate, decision.Classification, context);
        if (context.DestinationBase is not null)
            destination = context.DestinationBase(destination);
        var newName = decision.NamingTemplate is not null
            ? ResolveTemplate(decision.NamingTemplate, decision.Candidate, decision.Classification, context)
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

    private static string ResolveTemplate(string template, FileCandidate c, ClassificationResult r, PlanContext ctx)
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
            .Replace("{DD}", now.ToString("dd"));
    }

    private static string StemOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[..dot] : fileName;
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}

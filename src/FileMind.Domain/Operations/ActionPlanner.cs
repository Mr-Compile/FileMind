using FileMind.Domain.Classification;
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
        // Idempotent re-runs: a file already sitting at its resolved
        // destination produces no item — never a same-path "(2)" duplicate.
        if (context.Paths.Normalize(destPath) ==
            context.Paths.Normalize(decision.Candidate.SourceUri))
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
            .Replace("{KindFolder}", KindFolder(r.FileKind))
            .Replace("{SubKind}", SubKind(r.FileKind, StemOf(ctx.Paths.FileName(c.DisplayName))))
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

    /// <summary>
    /// Top-level folders FileMind creates — scanners skip these so
    /// already-organized files are never re-candidates (no churn).
    /// </summary>
    public static readonly IReadOnlySet<string> ManagedRoots = new HashSet<string>(
        ["school", "work", "documents", "images", "videos", "music", "archives", "other"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>The type-folder names users actually see (Documents, Music, …).</summary>
    public static string KindFolder(FileKind kind) => kind switch
    {
        FileKind.Document or FileKind.Spreadsheet or FileKind.Presentation => "Documents",
        FileKind.Image => "Images",
        FileKind.Video => "Videos",
        FileKind.Audio => "Music",
        FileKind.Archive => "Archives",
        _ => "Other"
    };

    // School-activity buckets under School/{Subject}/: token hits on the
    // filename stem; "lab3"/"hw2" count via word+digits matching.
    private static readonly string[] Assign =
        ["assign", "assignment", "homework", "hw", "exercise", "worksheet", "seatwork", "problemset", "task"];
    private static readonly string[] Activity =
        ["activity", "act", "quiz", "exam", "assessment", "performance", "recitation"];
    private static readonly string[] Lab = ["lab", "laboratory"];
    private static readonly string[] Note =
        ["note", "notes", "reviewer", "module", "syllabus", "handout", "reference", "summary", "outline"];
    private static readonly string[] Project = ["project", "capstone", "thesis", "practicum"];

    public static string SubKind(FileKind kind, string stem)
    {
        var toks = Normalizer.Normalize(stem).Tokens;
        static bool Hit(IReadOnlyList<string> toks, string[] keys) => toks.Any(t =>
            keys.Any(k => t == k || (t.StartsWith(k, StringComparison.Ordinal) &&
                t[k.Length..].All(char.IsDigit))));
        if (Hit(toks, Assign)) return "Assignments";
        if (Hit(toks, Activity)) return "Activities";
        if (Hit(toks, Lab)) return "Labs";
        if (Hit(toks, Note)) return "Notes";
        if (Hit(toks, Project)) return "Projects";
        return KindFolder(kind);
    }
}

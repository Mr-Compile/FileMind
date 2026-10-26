using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Rules;

namespace FileMind.Application.Decisions;

public sealed class DecisionMaker(ConfidenceEngine confidence)
{
    public Decision Decide(
        FileCandidate candidate,
        ClassificationResult classification,
        RuleEvaluation evaluation,
        IReadOnlyList<CollectionEntry> collections)
    {
        if (evaluation.Conflicts.Count > 0)
            return Review(candidate, classification,
                ["Rule destination conflicts: " + string.Join("; ", evaluation.Conflicts)]);

        var organize = evaluation.ProposedActions.FirstOrDefault(a =>
            a.Kind is RuleActionKind.Move or RuleActionKind.RouteToCollection
                or RuleActionKind.Rename or RuleActionKind.Copy);

        if (evaluation.ProposedActions.Any(a => a.Kind == RuleActionKind.Ignore))
            return new Decision
            {
                Kind = DecisionKind.Ignore,
                Candidate = candidate,
                Classification = classification,
                Reasons = ["Matched an ignore rule."]
            };

        if (evaluation.ProposedActions.Any(a => a.Kind == RuleActionKind.Ask))
            return Review(candidate, classification, ["Rule requires user confirmation."]);

        if (organize is not null)
        {
            var destinationTemplate = organize.DestinationTemplate
                ?? CollectionTemplate(organize.Collection, collections)
                ?? DefaultTemplate(classification);
            return new Decision
            {
                Kind = DecisionKind.Organize,
                Candidate = candidate,
                Classification = classification,
                DestinationTemplate = destinationTemplate,
                NamingTemplate = organize.NamingTemplate,
                ConflictPolicy = organize.ConflictPolicy,
                Reasons = [$"Rule '{evaluation.WinningRuleId}' matched."]
            };
        }

        var band = confidence.Band(classification.Confidence);

        if (band == ConfidenceBand.Low)
            return new Decision
            {
                Kind = DecisionKind.NoOp,
                Candidate = candidate,
                Classification = classification,
                Reasons = [$"Confidence {classification.Confidence:0} below Medium threshold; left untouched."]
            };

        if (band == ConfidenceBand.Medium || classification.NeedsReview)
            return Review(candidate, classification,
                [$"Confidence {classification.Confidence:0} requires Smart Inbox review."]);

        return new Decision
        {
            Kind = DecisionKind.Organize,
            Candidate = candidate,
            Classification = classification,
            DestinationTemplate = DefaultTemplate(classification),
            Reasons = [$"High-confidence classification ({classification.Confidence:0})."]
        };
    }

    private static string? CollectionTemplate(string? collectionId, IReadOnlyList<CollectionEntry> collections) =>
        collectionId is null ? null
            : collections.FirstOrDefault(c =>
                string.Equals(c.Id, collectionId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, collectionId, StringComparison.OrdinalIgnoreCase))
              ?.DestinationTemplate;

    private static string DefaultTemplate(ClassificationResult c) =>
        c.Subject is not null ? "School/{Subject}/{FileKind}"
        : c.Project is not null ? "Work/{Project}/{FileKind}"
        : "Files/{FileKind}";

    private static Decision Review(FileCandidate candidate, ClassificationResult classification,
        IReadOnlyList<string> reasons) => new()
        {
            Kind = DecisionKind.Review,
            Candidate = candidate,
            Classification = classification,
            Reasons = reasons
        };
}

using FileMind.Domain.Contracts;

namespace FileMind.Domain.Rules;

public sealed record RuleEvaluation
{
    public required IReadOnlyList<string> MatchedRuleIds { get; init; }
    public required IReadOnlyList<RuleAction> ProposedActions { get; init; }
    public required IReadOnlyList<string> Conflicts { get; init; }
    public required IReadOnlyList<string> Trace { get; init; }
    public bool NeedsReview { get; init; }
    public string? WinningRuleId { get; init; }
}

public sealed class RuleEngine
{
    public RuleEvaluation Evaluate(
        FileCandidate candidate,
        ClassificationResult classification,
        IReadOnlyList<Rule> rules)
    {
        var trace = new List<string>();
        var ordered = rules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.ConditionCount)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var matched = new List<Rule>();
        foreach (var rule in ordered)
        {
            var hit = ConditionEvaluator.Evaluate(rule.ConditionRoot, candidate, classification);
            trace.Add(hit
                ? $"MATCH {rule.Id} '{rule.Name}' (priority={rule.Priority}, conditions={rule.ConditionCount})"
                : $"SKIP  {rule.Id} '{rule.Name}'");
            if (!hit) continue;
            matched.Add(rule);
            if (rule.StopProcessing)
            {
                trace.Add($"STOP  {rule.Id} stop-processing=true");
                break;
            }
        }

        var actions = new List<RuleAction>();
        var conflicts = new List<string>();
        var destinations = new List<(string RuleId, string Template)>();

        foreach (var rule in matched)
        {
            foreach (var action in rule.Actions)
            {
                if (action.Kind is RuleActionKind.Move or RuleActionKind.RouteToCollection
                    && action.DestinationTemplate is not null)
                {
                    var existing = destinations.FirstOrDefault(d => d.Template != action.DestinationTemplate);
                    if (existing.RuleId is not null)
                    {
                        conflicts.Add(
                            $"Rules '{existing.RuleId}' and '{rule.Id}' propose different destinations " +
                            $"('{existing.Template}' vs '{action.DestinationTemplate}').");
                        continue;
                    }
                    destinations.Add((rule.Id, action.DestinationTemplate));
                }
                actions.Add(action);
            }
        }

        var needsReview = conflicts.Count > 0;
        if (needsReview)
            trace.Add("REVIEW conflicts detected; no silent winner chosen.");

        return new RuleEvaluation
        {
            MatchedRuleIds = matched.Select(m => m.Id).ToList(),
            ProposedActions = actions,
            Conflicts = conflicts,
            Trace = trace,
            NeedsReview = needsReview,
            WinningRuleId = matched.FirstOrDefault()?.Id
        };
    }
}

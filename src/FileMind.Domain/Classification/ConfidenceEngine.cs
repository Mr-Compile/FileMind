using FileMind.Domain.Contracts;

namespace FileMind.Domain.Classification;

public sealed record ConfidencePolicy
{
    public double HighThreshold { get; init; } = 95;
    public double MediumThreshold { get; init; } = 70;
    public double MaxScore { get; init; } = 100;
    public IReadOnlyDictionary<string, double> KindCaps { get; init; } =
        new Dictionary<string, double>
        {
            ["subject-keyword"] = 15,
            ["project-keyword"] = 12,
            ["extension-signal"] = 10,
            ["mime-signal"] = 8,
            ["content-signal"] = 25,
            ["ai-signal"] = 30
        };

    public static ConfidencePolicy Default { get; } = new();
}

public sealed class ConfidenceEngine(ConfidencePolicy? policy = null)
{
    private readonly ConfidencePolicy _policy = policy ?? ConfidencePolicy.Default;

    public double Score(IReadOnlyList<Evidence> evidence)
    {
        var capped = evidence
            .GroupBy(e => e.Kind)
            .Sum(g =>
            {
                var sum = g.Sum(e => e.ScoreContribution);
                return _policy.KindCaps.TryGetValue(g.Key, out var cap)
                    ? Math.Min(sum, cap)
                    : sum;
            });
        return Math.Clamp(capped, 0, _policy.MaxScore);
    }

    public ConfidenceBand Band(double score) =>
        score >= _policy.HighThreshold ? ConfidenceBand.High
        : score >= _policy.MediumThreshold ? ConfidenceBand.Medium
        : ConfidenceBand.Low;
}

using System.Text;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Intelligence.Local.Analysis;

/// <summary>
/// ICandidateEnricher backed by the local analyzer registry. Runs every
/// matching analyzer within the shared budget, merges metadata/text/evidence,
/// caps total enrichment evidence so content never swamps filename signals,
/// and converts analyzer failures into warnings (scan always continues).
/// </summary>
public sealed class LocalAnalyzerService(
    AnalyzerRegistry? registry = null,
    ExtractionBudget? budget = null) : ICandidateEnricher
{
    /// <summary>Max summed evidence contribution from enrichment.</summary>
    public const double EvidenceCap = 25;

    private readonly AnalyzerRegistry _registry = registry ?? new AnalyzerRegistry();
    private readonly ExtractionBudget _budget = budget ?? new ExtractionBudget();

    public async Task<Result<CandidateEnrichment>> EnrichAsync(
        FileCandidate candidate, IFileSystemAdapter fs, CancellationToken ct = default)
    {
        var analyzers = _registry.For(candidate);
        if (analyzers.Count == 0)
            return Result<CandidateEnrichment>.Ok(new CandidateEnrichment(
                candidate.Metadata, null, [], [], Reliable: false));

        var metadata = new Dictionary<string, string>(candidate.Metadata, StringComparer.OrdinalIgnoreCase);
        var evidence = new List<Evidence>();
        var warnings = new List<string>();
        var text = new StringBuilder();
        var reliable = false;
        var any = false;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(_budget.TimeoutOrDefault);

        foreach (var analyzer in analyzers)
        {
            any = true;
            try
            {
                var output = await analyzer.AnalyzeAsync(candidate, fs, _budget, linked.Token);
                reliable |= output.Reliable;
                foreach (var kv in output.Metadata)
                    metadata.TryAdd(kv.Key, kv.Value);
                evidence.AddRange(output.Evidence);
                warnings.AddRange(output.Warnings);
                if (output.ExtractedText is { } t && text.Length < _budget.MaxChars)
                    text.Append(t.AsSpan(0, Math.Min(t.Length, _budget.MaxChars - text.Length)));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                warnings.Add($"{analyzer.Name}: extraction timed out after {_budget.TimeoutOrDefault.TotalSeconds:0}s");
            }
            catch (Exception ex)
            {
                warnings.Add($"{analyzer.Name}: extraction failed safely — {ex.Message}");
            }
            if (linked.IsCancellationRequested) break;
        }

        if (!any) reliable = false;

        // Cap total enrichment contribution.
        var total = evidence.Sum(e => e.ScoreContribution);
        if (total > EvidenceCap && total > 0)
        {
            var scale = EvidenceCap / total;
            evidence = evidence.Select(e => e with
            {
                ScoreContribution = Math.Round(e.ScoreContribution * scale, 2)
            }).ToList();
        }

        var extracted = text.Length > 0 ? text.ToString() : null;
        if (extracted is not null)
            evidence.Add(new Evidence("extracted-text",
                $"Extracted {extracted.Length} chars of content", 5, ClassifierSource.Local));

        return Result<CandidateEnrichment>.Ok(new CandidateEnrichment(
            metadata, extracted, evidence, warnings, reliable));
    }
}

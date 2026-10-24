using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

/// <summary>
/// PLAN-009 M3.1 — escalation into the classifier pipeline. Runs only in
/// ai-assisted mode, only when deterministic confidence is below High, and
/// only after the privacy gate consents. AI output merges as capped
/// "ai-signal" evidence — it is never a verdict and never an action.
/// </summary>
public sealed class AiAdvisor(
    IAiPrivacyGate gate,
    IAIProvider provider,
    ConfidenceEngine confidence,
    Func<OrganizationMode> mode,
    IAiAuditRepository? audit = null) : IAiAdvisor
{
    /// <summary>Evidence contribution ceiling mirrors ConfidencePolicy KindCaps["ai-signal"].</summary>
    private const double AiSignalCap = 30;

    public async Task<Result<ClassificationResult?>> AssistAsync(
        FileCandidate candidate, ClassificationResult classification, CancellationToken ct = default)
    {
        if (mode() != OrganizationMode.AiAssisted) return Result<ClassificationResult?>.Ok(null);
        if (confidence.Band(classification.Confidence) == ConfidenceBand.High)
            return Result<ClassificationResult?>.Ok(null);
        if (!provider.IsAvailable) return Result<ClassificationResult?>.Ok(null);

        var prepared = gate.Prepare(candidate);
        if (!prepared.IsSuccess)
        {
            if (audit is not null)
            {
                try { await audit.RecordAsync("gate", $"gate-denied:{prepared.Error!.Code}", DateTimeOffset.UtcNow, ct); }
                catch { /* audit must never break the pipeline */ }
            }
            return Result<ClassificationResult?>.Ok(null);
        }

        var suggested = await provider.SuggestAsync(prepared.Value!, ct);
        if (!suggested.IsSuccess || suggested.Value is null)
            return Result<ClassificationResult?>.Ok(null);

        var s = suggested.Value;
        var detail = string.Join("; ", new[]
        {
            s.Collection is null ? null : $"collection={s.Collection}",
            s.Category is null ? null : $"category={s.Category}",
            s.Subject is null ? null : $"subject={s.Subject}",
            s.Project is null ? null : $"project={s.Project}",
            s.SuggestedName is null ? null : $"name={s.SuggestedName}",
            $"hint={s.ConfidenceHint:0.00}"
        }.Where(x => x is not null));

        var evidence = classification.Evidence
            .Append(new Evidence("ai-signal", $"Groq {detail}", s.ConfidenceHint * AiSignalCap, ClassifierSource.Ai))
            .ToList();

        var rescored = classification with
        {
            Evidence = evidence,
            Confidence = confidence.Score(evidence),
            Sources = classification.Sources.Contains(ClassifierSource.Ai)
                ? classification.Sources
                : [.. classification.Sources, ClassifierSource.Ai],
            Explanation = classification.Explanation + $" AI: {s.Explanation}"
        };
        return Result<ClassificationResult?>.Ok(rescored);
    }
}

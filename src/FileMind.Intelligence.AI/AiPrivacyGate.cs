using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Intelligence.AI;

/// <summary>
/// Minimizes a candidate into an <see cref="AiRequestContext"/> honoring the
/// resolved <see cref="AiConsent"/>: protected paths never leave the machine;
/// extracted text requires explicit `ai.consent.text`; metadata is allowlisted
/// (`ai.consent.metadata.keys`, `*` = all non-content keys) and revocable via
/// `ai.consent.metadata.off`. Off by default — nothing is sent until `ai.enabled`.
/// </summary>
public sealed class AiPrivacyGate(GroqOptions options, IProtectedPathProvider protectedPaths) : IAiPrivacyGate
{
    public Result<AiRequestContext> Prepare(FileCandidate candidate)
    {
        if (!options.Enabled)
            return Result<AiRequestContext>.Fail(
                new FileMindError(ErrorCode.AiOffline, "AI disabled (ai.enabled not set).", false, false));
        if (protectedPaths.IsProtected(candidate.SourceUri))
            return Result<AiRequestContext>.Fail(
                new FileMindError(ErrorCode.ProtectedPath, $"Protected path never leaves the device: {candidate.SourceUri}", false, true));

        var consent = options.Consent;
        var allowAll = consent.MetadataKeys.Contains("*");
        var metadata = consent.MetadataKeys.Count > 0
            ? (IReadOnlyDictionary<string, string>)candidate.Metadata
                .Where(kv => !kv.Key.StartsWith("content.", StringComparison.Ordinal)
                    && (allowAll || consent.MetadataKeys.Contains(kv.Key)))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Length > 200 ? kv.Value[..200] : kv.Value)
            : new Dictionary<string, string>();

        var snippet = consent.IncludeExtractedSnippet && candidate.ExtractedTextRef is { } t
            ? t[..Math.Min(t.Length, consent.MaxSnippetChars)]
            : null;

        return Result<AiRequestContext>.Ok(new AiRequestContext(
            candidate.DisplayName, candidate.Extension, metadata, snippet));
    }
}

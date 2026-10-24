namespace FileMind.Intelligence.AI;

/// <summary>
/// Per-field consent for AI payloads (PLAN-009 M0.2). Defaults are
/// privacy-first: filename + extension only; metadata keys must be explicitly
/// allow-listed; extracted file content is never sent unless enabled.
/// </summary>
public sealed record AiConsent
{
    public bool IncludeDisplayName { get; init; } = true;
    public bool IncludeExtension { get; init; } = true;
    /// <summary>Allow-list of FileCandidate.Metadata keys that may be sent. Empty = none; `*` = all non-content keys.</summary>
    public IReadOnlyList<string> MetadataKeys { get; init; } = [];
    public bool IncludeExtractedSnippet { get; init; }
    public int MaxSnippetChars { get; init; } = 512;

    public static AiConsent Default { get; } = new();
}

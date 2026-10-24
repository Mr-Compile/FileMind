using FileMind.Application.Ports;

namespace FileMind.Intelligence.AI;

/// <summary>
/// AI config — opt-in, off by default. `ai.enabled` master switch; consent
/// toggles for metadata / extracted text (filename always allowed when on).
/// Model is configuration, not a constant: `ai.model` setting or
/// `FILEMIND_GROQ_MODEL` env override. Key comes from `GROQ_API_KEY` env,
/// repo-root `.env`, or `ISecretStore` — never hard-coded.
/// </summary>
public sealed record GroqOptions(
    string Endpoint,
    string Model,
    string? ApiKey,
    bool Enabled,
    bool SendMetadata,
    bool SendExtractedText,
    AiQuotaPolicy Quota,
    IReadOnlyList<string>? MetadataAllowlist = null,
    int MaxSnippetChars = 2000,
    double Temperature = 0.1,
    int MaxRetries = 2,
    TimeSpan? RequestTimeout = null,
    TimeSpan? MaxRetryAfter = null)
{
    public const string DefaultEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public const string DefaultModel = "openai/gpt-oss-120b";
    public const string ApiKeyEnvVar = "GROQ_API_KEY";
    public const string SecretStoreKey = "GROQ_API_KEY";

    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
    public int MaxCompletionTokens => Quota.MaxResponseTokens;
    public TimeSpan Timeout => RequestTimeout ?? TimeSpan.FromSeconds(20);
    public TimeSpan RetryAfterCeiling => MaxRetryAfter ?? TimeSpan.FromSeconds(30);

    /// <summary>
    /// Field-level consent derived from options — metadata honors the explicit
    /// allowlist (`ai.consent.metadata.keys`), extracted text needs opt-in.
    /// </summary>
    public AiConsent Consent => new()
    {
        IncludeDisplayName = true,
        IncludeExtension = true,
        MetadataKeys = SendMetadata ? MetadataAllowlist ?? ["*"] : [],
        IncludeExtractedSnippet = SendExtractedText,
        MaxSnippetChars = MaxSnippetChars,
    };

    public static GroqOptions Load(ISettingsRepository settings, string? apiKey)
    {
        string? Get(string k) => settings.GetAsync(k).GetAwaiter().GetResult().Value;
        bool On(string k) => (Get(k) ?? "").Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        int Num(string k, int d) => int.TryParse(Get(k), out var v) ? v : d;

        var envModel = Environment.GetEnvironmentVariable("FILEMIND_GROQ_MODEL");
        var envEndpoint = Environment.GetEnvironmentVariable("FILEMIND_GROQ_ENDPOINT");
        var keys = Get("ai.consent.metadata.keys");
        return new GroqOptions(
            Endpoint: envEndpoint ?? Get("ai.endpoint") ?? DefaultEndpoint,
            Model: envModel ?? Get("ai.model") ?? DefaultModel,
            ApiKey: apiKey,
            Enabled: On("ai.enabled"),
            SendMetadata: !On("ai.consent.metadata.off"),   // metadata ok unless revoked
            SendExtractedText: On("ai.consent.text"),        // content needs explicit opt-in
            MetadataAllowlist: string.IsNullOrWhiteSpace(keys)
                ? null
                : keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            MaxSnippetChars: Num("ai.consent.snippet-chars", 2000),
            Quota: new AiQuotaPolicy
            {
                MaxRequestsPerDay = Num("ai.quota.daily-requests", 800),
                MaxTokensPerDay = Num("ai.quota.daily-tokens", 160_000),
                MaxRequestsPerWeek = Num("ai.quota.weekly-requests", 0),
                MaxRequestsPerMinute = Num("ai.quota.minute-requests", 24),
                MaxTokensPerMinute = Num("ai.quota.minute-tokens", 6_400),
                MaxTokensPerRequest = Num("ai.quota.max-request", 1024),
                MaxResponseTokens = Num("ai.quota.max-response", 512),
                CacheTtlMinutes = Num("ai.quota.cache-ttl", 30),
            });
    }
}

using FileMind.Application.Ports;
using FileMind.Domain.Errors;

namespace FileMind.Intelligence.AI;

/// <summary>What Groq's x-ratelimit-* headers reported on the last response.</summary>
public sealed record RateLimitSnapshot(
    int? RemainingRequestsDay,
    int? RemainingTokensMinute,
    DateTimeOffset ObservedAt);

/// <summary>
/// PLAN-020 budget policy. Defaults sit ~20% under Groq's published Free-plan
/// sample limits for openai/gpt-oss-120b (30 RPM / 1,000 RPD / 8,000 TPM /
/// 200,000 TPD, org-level, console is authoritative). Weekly caps are 0=off —
/// daily matches the provider's reset window; weekly exists for paid-plan
/// cost control.
/// </summary>
public sealed record AiQuotaPolicy
{
    public int MaxRequestsPerMinute { get; init; } = 24;
    public int MaxRequestsPerDay { get; init; } = 800;
    public int MaxRequestsPerWeek { get; init; } = 0;
    public int MaxTokensPerMinute { get; init; } = 6400;
    public int MaxTokensPerDay { get; init; } = 160_000;
    /// <summary>Hard per-request token ceiling applied to estimates.</summary>
    public int MaxTokensPerRequest { get; init; } = 1024;
    /// <summary>Bounded max_completion_tokens — a single response can never approach the TPM ceiling.</summary>
    public int MaxResponseTokens { get; init; } = 512;
    /// <summary>Suggestion cache TTL — hits spend zero quota.</summary>
    public int CacheTtlMinutes { get; init; } = 30;

    public static AiQuotaPolicy Default { get; } = new();
}

public sealed record AiQuotaStatus(
    bool Allowed,
    string? DenyReason,
    int RequestsLastMinute,
    int RequestsToday,
    int RequestsThisWeek,
    int TokensLastMinute,
    int TokensToday,
    int? ProviderRemainingRequestsDay,
    int? ProviderRemainingTokensMinute,
    AiQuotaPolicy Policy);

/// <summary>
/// PLAN-020 pre-flight budget gate. Local counters derive from the persisted
/// usage ledger; provider x-ratelimit headers reconcile down (the stricter of
/// local-computed vs provider-reported remaining wins, self-healing drift and
/// other installs sharing the org key).
/// </summary>
public sealed class AiQuotaGate(IAiUsageLedger? ledger, IClock clock, AiQuotaPolicy? policy = null)
{
    private readonly AiQuotaPolicy _policy = policy ?? AiQuotaPolicy.Default;
    private readonly object _sync = new();
    private RateLimitSnapshot? _provider;
    private IReadOnlyList<AiUsageRecord>? _window;
    private DateTimeOffset _windowLoaded = DateTimeOffset.MinValue;

    public void Observe(RateLimitSnapshot snapshot)
    {
        lock (_sync) _provider = snapshot;
    }

    /// <summary>
    /// Appends a just-completed call so the rolling minute/day counters see it
    /// immediately — the persisted ledger is re-read at most every 30s and would
    /// otherwise let a burst slip through the per-minute budget.
    /// </summary>
    public void RecordCall(int promptTokens, int completionTokens, DateTimeOffset at)
    {
        lock (_sync)
        {
            var rows = (_window ?? (IReadOnlyList<AiUsageRecord>)[]).ToList();
            rows.Add(new AiUsageRecord("", "", "", promptTokens, completionTokens, "ok", at));
            _window = rows;
            _windowLoaded = at;
        }
    }

    public async Task<Result<bool>> TryAcquireAsync(int estimatedTokens, CancellationToken ct = default)
    {
        var status = await StatusAsync(estimatedTokens, ct);
        return status.Allowed
            ? Result<bool>.Ok(true)
            : Result<bool>.Fail(new FileMindError(
                ErrorCode.AiRateLimited, status.DenyReason!,
                RetrySafe: status.DenyReason!.Contains("minute", StringComparison.OrdinalIgnoreCase),
                RequiresReview: false));
    }

    public async Task<AiQuotaStatus> StatusAsync(int estimatedTokens = 0, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var rows = await LoadWindowAsync(now, ct);

        var minuteAgo = now.AddMinutes(-1);
        var dayStart = now.Date;
        var weekStart = now.AddDays(-7);
        int reqMin = 0, reqDay = 0, reqWeek = 0, tokMin = 0, tokDay = 0;
        foreach (var r in rows)
        {
            if (r.At >= weekStart) reqWeek++;
            if (r.At >= dayStart) { reqDay++; tokDay += r.PromptTokens + r.CompletionTokens; }
            if (r.At >= minuteAgo) { reqMin++; tokMin += r.PromptTokens + r.CompletionTokens; }
        }

        int? provReq, provTok;
        lock (_sync) { provReq = _provider?.RemainingRequestsDay; provTok = _provider?.RemainingTokensMinute; }

        string? deny = null;
        if (estimatedTokens > _policy.MaxTokensPerRequest)
            deny = $"single-request estimate {estimatedTokens} exceeds per-request cap {_policy.MaxTokensPerRequest}";
        else if (reqMin >= _policy.MaxRequestsPerMinute)
            deny = $"per-minute request budget reached ({reqMin}/{_policy.MaxRequestsPerMinute})";
        else if (_policy.MaxTokensPerMinute > 0 && tokMin + estimatedTokens >= _policy.MaxTokensPerMinute)
            deny = $"per-minute token budget would be exceeded ({tokMin}+{estimatedTokens}/{_policy.MaxTokensPerMinute})";
        else if (reqDay >= _policy.MaxRequestsPerDay)
            deny = $"daily request budget reached ({reqDay}/{_policy.MaxRequestsPerDay})";
        else if (_policy.MaxTokensPerDay > 0 && tokDay + estimatedTokens >= _policy.MaxTokensPerDay)
            deny = $"daily token budget would be exceeded ({tokDay}+{estimatedTokens}/{_policy.MaxTokensPerDay})";
        else if (_policy.MaxRequestsPerWeek > 0 && reqWeek >= _policy.MaxRequestsPerWeek)
            deny = $"weekly request budget reached ({reqWeek}/{_policy.MaxRequestsPerWeek})";
        else if (provReq is 0)
            deny = "provider reports 0 requests remaining today (x-ratelimit header)";
        else if (provTok is 0)
            deny = "provider reports 0 tokens remaining this minute (x-ratelimit header)";

        return new AiQuotaStatus(deny is null, deny, reqMin, reqDay, reqWeek, tokMin, tokDay,
            provReq, provTok, _policy);
    }

    private async Task<IReadOnlyList<AiUsageRecord>> LoadWindowAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_window is not null && now - _windowLoaded < TimeSpan.FromSeconds(30)) return _window;
        if (ledger is null) return _window = [];
        var since = await ledger.SinceAsync(now.AddDays(-7), ct);
        _window = since.IsSuccess ? since.Value! : [];
        _windowLoaded = now;
        return _window;
    }
}

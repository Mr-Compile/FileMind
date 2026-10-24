using System.Net;
using System.Text;
using System.Text.Json;
using FileMind.Application.Ports;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Infrastructure.Sqlite;
using FileMind.Intelligence.AI;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

// ---- fakes ----

internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(respond(request));
    }
}

internal sealed class ThrowingHttpHandler : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        throw new HttpRequestException("simulated network failure");
    }
}

internal sealed class InMemoryUsageLedger : IAiUsageLedger
{
    public readonly List<AiUsageRecord> Rows = [];

    public Task<Result<bool>> RecordAsync(AiUsageRecord record, CancellationToken ct = default)
    {
        Rows.Add(record);
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public Task<Result<IReadOnlyList<AiUsageRecord>>> SinceAsync(DateTimeOffset from, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<AiUsageRecord>>.Ok(
            (IReadOnlyList<AiUsageRecord>)Rows.Where(r => r.At >= from).ToList()));

    public Task<Result<IReadOnlyList<AiUsageRecord>>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<AiUsageRecord>>.Ok(
            (IReadOnlyList<AiUsageRecord>)Rows.OrderByDescending(r => r.At).Take(limit).ToList()));
}

internal sealed class InMemoryAudit : IAiAuditRepository
{
    public readonly List<(string Hash, string Outcome)> Rows = [];

    public Task<Result<bool>> RecordAsync(string requestHash, string outcome, DateTimeOffset at, CancellationToken ct = default)
    {
        Rows.Add((requestHash, outcome));
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public Task<Result<IReadOnlyList<string>>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<string>>.Ok(
            (IReadOnlyList<string>)Rows.Select(r => $"{r.Hash}|{r.Outcome}").Take(limit).ToList()));
}

internal sealed class InMemorySettings(Dictionary<string, string>? seed = null) : ISettingsRepository
{
    private readonly Dictionary<string, string> _map = seed ?? [];

    public Task<Result<string?>> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Result<string?>.Ok(_map.GetValueOrDefault(key)));

    public Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default)
    {
        _map[key] = value;
        return Task.FromResult(Result<bool>.Ok(true));
    }
}

// ---- helpers ----

internal static class AiTestData
{
    public static GroqOptions EnabledOptions(AiQuotaPolicy? quota = null) =>
        new("https://test.local/v1/chat", "test-model", "test-key",
            Enabled: true, SendMetadata: true, SendExtractedText: false,
            quota ?? AiQuotaPolicy.Default);

    public static HttpResponseMessage ChatJson(string contentJson, int promptTokens = 10, int completionTokens = 5)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = "test-model",
            choices = new[] { new { message = new { role = "assistant", content = contentJson } } },
            usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens, total_tokens = promptTokens + completionTokens }
        });
        var res = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        res.Headers.Add("x-ratelimit-remaining-requests", "990");
        res.Headers.Add("x-ratelimit-remaining-tokens", "7000");
        return res;
    }

    public static FileCandidate Candidate(string path = @"C:\Users\me\Downloads\report.pdf") =>
        Candidates.Create(path) with
        {
            Metadata = new Dictionary<string, string>
            {
                ["author"] = "Jane", ["content.body"] = "secret body", ["title"] = "Q4"
            },
            ExtractedTextRef = new string('x', 3000)
        };
}

// ---- PLAN-009 M0.2 / M0.3: privacy gate ----

public class AiPrivacyGateTests
{
    [Fact]
    public void Disabled_ReturnsFail()
    {
        var gate = new AiPrivacyGate(
            AiTestData.EnabledOptions() with { Enabled = false },
            new StaticProtectedPaths());
        var r = gate.Prepare(AiTestData.Candidate());
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiOffline, r.Error!.Code);
    }

    [Fact]
    public void ProtectedPath_NeverLeavesDevice()
    {
        var gate = new AiPrivacyGate(
            AiTestData.EnabledOptions(),
            new StaticProtectedPaths(@"C:\Users\me\Downloads"));
        var r = gate.Prepare(AiTestData.Candidate());
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.ProtectedPath, r.Error!.Code);
    }

    [Fact]
    public void Minimizes_ContentMetadataAndSnippetConsent()
    {
        var gate = new AiPrivacyGate(AiTestData.EnabledOptions(), new StaticProtectedPaths());
        var ctx = gate.Prepare(AiTestData.Candidate()).Value!;
        Assert.DoesNotContain(ctx.Metadata.Keys, k => k.StartsWith("content."));
        Assert.Null(ctx.ExtractedSnippet); // text consent off
        Assert.Equal("report.pdf", ctx.DisplayName);
    }

    [Fact]
    public void MetadataAllowlist_RestrictsFields()
    {
        var gate = new AiPrivacyGate(
            AiTestData.EnabledOptions() with { MetadataAllowlist = ["author"] },
            new StaticProtectedPaths());
        var ctx = gate.Prepare(AiTestData.Candidate()).Value!;
        Assert.Equal(["author"], ctx.Metadata.Keys.Order().ToArray());
    }

    [Fact]
    public void SnippetConsent_CapsLength()
    {
        var gate = new AiPrivacyGate(
            AiTestData.EnabledOptions() with { SendExtractedText = true, MaxSnippetChars = 100 },
            new StaticProtectedPaths());
        var ctx = gate.Prepare(AiTestData.Candidate()).Value!;
        Assert.Equal(100, ctx.ExtractedSnippet!.Length);
    }
}

// ---- PLAN-020: quota gate ----

public class AiQuotaGateTests
{
    private static AiUsageRecord Row(DateTimeOffset at, int tokens = 10) =>
        new("h", "m", "p", tokens, 0, "ok", at);

    [Fact]
    public async Task UnderBudget_Allows()
    {
        var gate = new AiQuotaGate(new InMemoryUsageLedger(), new FakeClock());
        var r = await gate.TryAcquireAsync(100);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task DailyRequestCap_Denies()
    {
        var ledger = new InMemoryUsageLedger();
        var clock = new FakeClock();
        for (var i = 0; i < 5; i++) ledger.Rows.Add(Row(clock.UtcNow.AddHours(-2)));
        var gate = new AiQuotaGate(ledger, clock,
            new AiQuotaPolicy { MaxRequestsPerDay = 5 });
        var r = await gate.TryAcquireAsync(10);
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiRateLimited, r.Error!.Code);
    }

    [Fact]
    public async Task PerMinuteDenial_IsRetrySafe()
    {
        var ledger = new InMemoryUsageLedger();
        var clock = new FakeClock();
        ledger.Rows.Add(Row(clock.UtcNow.AddSeconds(-10)));
        var gate = new AiQuotaGate(ledger, clock,
            new AiQuotaPolicy { MaxRequestsPerMinute = 1 });
        var r = await gate.TryAcquireAsync(10);
        Assert.False(r.IsSuccess);
        Assert.True(r.Error!.RetrySafe);
    }

    [Fact]
    public async Task PerRequestCap_Denies()
    {
        var gate = new AiQuotaGate(new InMemoryUsageLedger(), new FakeClock(),
            new AiQuotaPolicy { MaxTokensPerRequest = 50 });
        var r = await gate.TryAcquireAsync(51);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task ProviderZeroRemaining_Denies()
    {
        var gate = new AiQuotaGate(new InMemoryUsageLedger(), new FakeClock());
        gate.Observe(new RateLimitSnapshot(0, 100, DateTimeOffset.UtcNow));
        var r = await gate.TryAcquireAsync(10);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task WeeklyCap_DeniesWhenConfigured()
    {
        var ledger = new InMemoryUsageLedger();
        var clock = new FakeClock();
        ledger.Rows.Add(Row(clock.UtcNow.AddDays(-3)));
        var gate = new AiQuotaGate(ledger, clock,
            new AiQuotaPolicy { MaxRequestsPerWeek = 1 });
        var r = await gate.TryAcquireAsync(10);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task DayRollover_ResetsCounters()
    {
        var ledger = new InMemoryUsageLedger();
        var clock = new FakeClock();
        ledger.Rows.Add(Row(clock.UtcNow)); // yesterday relative to advanced clock
        var gate = new AiQuotaGate(ledger, clock,
            new AiQuotaPolicy { MaxRequestsPerDay = 1 });
        Assert.False((await gate.TryAcquireAsync(10)).IsSuccess);
        clock.UtcNow = clock.UtcNow.AddDays(1);
        gate = new AiQuotaGate(ledger, clock,
            new AiQuotaPolicy { MaxRequestsPerDay = 1 });
        Assert.True((await gate.TryAcquireAsync(10)).IsSuccess);
    }

    [Fact]
    public async Task RecordCall_FeedsMinuteCounterImmediately()
    {
        var clock = new FakeClock();
        var gate = new AiQuotaGate(new InMemoryUsageLedger(), clock,
            new AiQuotaPolicy { MaxRequestsPerMinute = 1 });
        gate.RecordCall(10, 5, clock.UtcNow);
        var r = await gate.TryAcquireAsync(10);
        Assert.False(r.IsSuccess);
    }
}

// ---- PLAN-009 M1: chat client ----

public class GroqChatClientTests
{
    private static GroqChatClient Client(HttpMessageHandler handler, InMemoryUsageLedger? ledger = null,
        Action<RateLimitSnapshot>? sink = null, IClock? clock = null) =>
        new(new HttpClient(handler), AiTestData.EnabledOptions(), null,
            clock ?? new FakeClock(), ledger, sink);

    [Fact]
    public async Task Ok_ParsesContentAndUsageAndWritesLedger()
    {
        var ledger = new InMemoryUsageLedger();
        var handler = new FakeHttpHandler(_ => AiTestData.ChatJson("{\"a\":1}", 11, 7));
        var client = Client(handler, ledger);
        var r = await client.CompleteJsonAsync("sys", "pay", "classify", "h1");
        Assert.True(r.IsSuccess);
        Assert.Equal("{\"a\":1}", r.Value!.Content);
        Assert.Equal(11, r.Value.PromptTokens);
        Assert.Single(ledger.Rows);
        Assert.Equal("ok", ledger.Rows[0].Outcome);
    }

    [Fact]
    public async Task RateLimitHeaders_ReachSink()
    {
        RateLimitSnapshot? seen = null;
        var handler = new FakeHttpHandler(_ => AiTestData.ChatJson("{}"));
        var client = Client(handler, sink: s => seen = s);
        await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.NotNull(seen);
        Assert.Equal(990, seen!.RemainingRequestsDay);
    }

    [Fact]
    public async Task Http429_TripsBreakerAndReturnsRateLimited()
    {
        var clock = new FakeClock();
        var handler = new FakeHttpHandler(_ =>
        {
            var res = new HttpResponseMessage((HttpStatusCode)429);
            res.Headers.Add("Retry-After", "120");
            return res;
        });
        var client = Client(handler, clock: clock);
        var r = await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiRateLimited, r.Error!.Code);
        Assert.False(client.IsAvailable);
        Assert.Equal(clock.UtcNow.AddSeconds(120), client.BreakerUntil);
    }

    [Fact]
    public async Task Unauthorized_MarksAuthFailed()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = Client(handler);
        var r = await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.False(r.IsSuccess);
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public async Task InvalidJson_ReturnsTypedError()
    {
        var handler = new FakeHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json", Encoding.UTF8, "application/json")
            });
        var client = Client(handler);
        var r = await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiInvalidResponse, r.Error!.Code);
    }

    [Fact]
    public async Task NetworkFailure_RetriesThenOfflineRetrySafe()
    {
        var handler = new ThrowingHttpHandler();
        var client = Client(handler);
        var r = await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiOffline, r.Error!.Code);
        Assert.True(r.Error!.RetrySafe);
        Assert.Equal(3, handler.Calls); // initial + 2 retries
    }

    [Fact]
    public async Task NoKey_ReturnsOfflineWithoutCall()
    {
        Environment.SetEnvironmentVariable(GroqOptions.ApiKeyEnvVar, null);
        var handler = new FakeHttpHandler(_ => AiTestData.ChatJson("{}"));
        var options = AiTestData.EnabledOptions() with { ApiKey = null };
        var client = new GroqChatClient(new HttpClient(handler), options, null, new FakeClock());
        var r = await client.CompleteJsonAsync("sys", "pay", "p", "h");
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiOffline, r.Error!.Code);
        Assert.Equal(0, handler.Calls);
    }
}

// ---- PLAN-009 M1.2 + M2: provider orchestration ----

public class GroqProviderTests
{
    private static (GroqProvider, FakeHttpHandler, InMemoryUsageLedger, InMemoryAudit) Wire(
        AiQuotaPolicy? quota = null)
    {
        var options = AiTestData.EnabledOptions(quota);
        var handler = new FakeHttpHandler(_ => AiTestData.ChatJson(
            "{\"collection\":\"school\",\"subject\":\"IT421\",\"confidenceHint\":80,\"explanation\":\"looks like networks\"}"));
        var ledger = new InMemoryUsageLedger();
        var audit = new InMemoryAudit();
        var clock = new FakeClock();
        var gate = new AiQuotaGate(ledger, clock, quota ?? AiQuotaPolicy.Default);
        var client = new GroqChatClient(new HttpClient(handler), options, null, clock, ledger, gate.Observe);
        var cache = new AiSuggestionCache(clock);
        var provider = new GroqProvider(options, client, gate, cache, audit, clock);
        return (provider, handler, ledger, audit);
    }

    private static AiRequestContext Ctx() =>
        new("report.pdf", "pdf", new Dictionary<string, string>(), null);

    [Fact]
    public async Task Suggestion_ValidResponse_MapsFields()
    {
        var (provider, _, _, _) = Wire();
        var r = await provider.SuggestAsync(Ctx());
        Assert.True(r.IsSuccess);
        Assert.Equal("IT421", r.Value!.Subject);
        Assert.Equal(80, r.Value.ConfidenceHint);
    }

    [Fact]
    public async Task CacheHit_SpendsZeroQuota()
    {
        var (provider, handler, ledger, _) = Wire();
        await provider.SuggestAsync(Ctx());
        await provider.SuggestAsync(Ctx());
        Assert.Equal(1, handler.Calls);
        Assert.Single(ledger.Rows); // only the real call reached the ledger
    }

    [Fact]
    public async Task QuotaBlocked_FailsBeforeHttpAndAudits()
    {
        var (provider, handler, _, audit) = Wire(new AiQuotaPolicy { MaxRequestsPerMinute = 0 });
        var r = await provider.SuggestAsync(Ctx());
        Assert.False(r.IsSuccess);
        Assert.Equal(0, handler.Calls);
        Assert.Contains(audit.Rows, x => x.Outcome == "quota-blocked");
    }

    [Fact]
    public async Task Disabled_FailsWithoutHttp()
    {
        var (provider, handler, _, _) = Wire();
        var disabled = new GroqProvider(
            AiTestData.EnabledOptions() with { Enabled = false },
            new GroqChatClient(new HttpClient(handler), AiTestData.EnabledOptions(), null, new FakeClock()));
        Assert.False(disabled.IsAvailable);
        var r = await disabled.SuggestAsync(Ctx());
        Assert.False(r.IsSuccess);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task MalformedSuggestion_ReturnsInvalidResponse()
    {
        var handler = new FakeHttpHandler(_ => AiTestData.ChatJson("[1,2,3]"));
        var options = AiTestData.EnabledOptions();
        var client = new GroqChatClient(new HttpClient(handler), options, null, new FakeClock());
        var provider = new GroqProvider(options, client);
        var r = await provider.SuggestAsync(Ctx());
        Assert.False(r.IsSuccess);
        Assert.Equal(ErrorCode.AiInvalidResponse, r.Error!.Code);
    }
}

// ---- PLAN-009 M3.1: advisory escalation ----

public class AiAdvisorTests
{
    private static ClassificationResult LowConf() => new()
    {
        FileKind = FileKind.Document,
        Evidence = [new Evidence("ext-kind", "pdf", 40, ClassifierSource.Local)],
        Confidence = 40,
        Sources = [ClassifierSource.Local],
        Explanation = "weak match"
    };

    private static AiAdvisor Wire(OrganizationMode mode, IAiPrivacyGate? gate = null, IAIProvider? provider = null)
    {
        var options = AiTestData.EnabledOptions();
        return new AiAdvisor(
            gate ?? new AiPrivacyGate(options, new StaticProtectedPaths()),
            provider ?? new StubAiProvider
            {
                Next = new AiSuggestion("school", null, "IT421", null, null, 0.8, "networks content")
            },
            new ConfidenceEngine(), () => mode);
    }

    [Fact]
    public async Task NonAiAssistedMode_ReturnsNull()
    {
        var r = await Wire(OrganizationMode.Smart).AssistAsync(AiTestData.Candidate(), LowConf());
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task HighConfidence_SkipsAi()
    {
        var high = LowConf() with { Confidence = 96 };
        var r = await Wire(OrganizationMode.AiAssisted).AssistAsync(AiTestData.Candidate(), high);
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task ProviderUnavailable_ReturnsNull()
    {
        var stub = new StubAiProvider { IsAvailable = false };
        var r = await Wire(OrganizationMode.AiAssisted, provider: stub).AssistAsync(AiTestData.Candidate(), LowConf());
        Assert.Null(r.Value);
    }

    [Fact]
    public async Task MergesEvidence_CappedAndSourced()
    {
        var r = await Wire(OrganizationMode.AiAssisted).AssistAsync(AiTestData.Candidate(), LowConf());
        var rescored = r.Value!;
        Assert.Contains(rescored.Sources, s => s == ClassifierSource.Ai);
        var ai = Assert.Single(rescored.Evidence, e => e.Kind == "ai-signal");
        Assert.Equal(0.8 * 30, ai.ScoreContribution, 3);
        Assert.Contains("AI:", rescored.Explanation);
    }

    [Fact]
    public async Task GateDenied_ReturnsNull()
    {
        var gate = new AiPrivacyGate(AiTestData.EnabledOptions() with { Enabled = false },
            new StaticProtectedPaths());
        var r = await Wire(OrganizationMode.AiAssisted, gate: gate).AssistAsync(AiTestData.Candidate(), LowConf());
        Assert.Null(r.Value);
    }
}

// ---- PLAN-009 M0.3 + QA: secret stores, cache, ledger persistence ----

public class AiInfrastructureTests
{
    [Fact]
    public async Task EnvSecretStore_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fm-env-{Guid.NewGuid():N}", ".env");
        var store = new EnvSecretStore(path);
        await store.SetAsync("FM_TEST_KEY", "k123");
        Assert.Equal("k123", (await store.GetAsync("FM_TEST_KEY")).Value);
        await store.DeleteAsync("FM_TEST_KEY");
        Assert.False((await store.GetAsync("FM_TEST_KEY")).IsSuccess);
    }

    [Fact]
    public async Task WindowsSecretStore_RoundTrips()
    {
        var store = new FileMind.Platform.Windows.WindowsSecretStore(
            Path.Combine(Path.GetTempPath(), $"fm-sec-{Guid.NewGuid():N}"));
        var key = $"test-{Guid.NewGuid():N}";
        await store.SetAsync(key, "s3cr3t");
        Assert.Equal("s3cr3t", (await store.GetAsync(key)).Value);
        await store.DeleteAsync(key);
    }

    [Fact]
    public void Cache_ExpiresByTtl()
    {
        var clock = new FakeClock();
        var cache = new AiSuggestionCache(clock, ttl: TimeSpan.FromMinutes(5));
        var s = new AiSuggestion(null, null, null, null, null, 50, "e");
        cache.Store("h", s);
        Assert.True(cache.TryGet("h", out _));
        clock.UtcNow = clock.UtcNow.AddMinutes(6);
        Assert.False(cache.TryGet("h", out _));
    }

    [Fact]
    public async Task SqliteLedger_PersistsAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fm-ledger-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = FileMindDb.Open(path))
            {
                IAiUsageLedger ledger = new FileMind.Infrastructure.Sqlite.SqliteStore(db);
                await ledger.RecordAsync(new AiUsageRecord("h1", "m", "classify", 5, 3, "ok", DateTimeOffset.UtcNow));
            }
            using (var db = FileMindDb.Open(path))
            {
                IAiUsageLedger ledger = new FileMind.Infrastructure.Sqlite.SqliteStore(db);
                var rows = await ledger.SinceAsync(DateTimeOffset.UtcNow.AddHours(-1));
                Assert.Single(rows.Value!);
                Assert.Equal("h1", rows.Value![0].RequestHash);
                Assert.Equal(8, rows.Value![0].PromptTokens + rows.Value![0].CompletionTokens);
                var recent = await ledger.GetRecentAsync(10);
                Assert.Single(recent.Value!);
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

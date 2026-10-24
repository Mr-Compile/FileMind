using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Recovery;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;
using FileMind.Domain.Rules;
using FileMind.Infrastructure.Sqlite;
using FileMind.Platform.Windows;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

public sealed class ResidualCompletionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-resid-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;

    public ResidualCompletionTests()
    {
        Directory.CreateDirectory(_root);
        _db = FileMindDb.Open(Path.Combine(_root, "test.db"));
        _store = new SqliteStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string WriteFile(string relative, string content = "test content")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private FileCandidate CandidateFor(string realPath, string? uriOverride = null)
    {
        var info = new FileInfo(realPath);
        return new FileCandidate
        {
            ScanId = "scan-test",
            SourceUri = uriOverride ?? realPath,
            DisplayName = Path.GetFileName(realPath),
            Extension = Path.GetExtension(realPath).TrimStart('.'),
            SizeBytes = info.Length,
            CreatedAt = info.CreationTimeUtc,
            ModifiedAt = info.LastWriteTimeUtc,
            SourceScope = SourceScope.Downloads
        };
    }

    private (string Path, string StableId, string? ContentHash, string HashState) IndexRow(string path) =>
        _db.Query("SELECT path,stable_id,content_hash,hash_state FROM file_index WHERE path=$p",
            r => (r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)),
            ("$p", path)).Single();

    // ---- FileIndexingService ----

    [Fact]
    public async Task Index_UpsertsFingerprintAndHash()
    {
        var file = WriteFile("inbox/doc.txt");
        var service = new FileIndexingService(_store, new WindowsHashService());

        var result = await service.IndexAsync([CandidateFor(file)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Indexed);
        Assert.Equal(1, result.Value.Hashed);
        var row = IndexRow(file);
        Assert.StartsWith("sha256:", row.ContentHash);
        Assert.Equal("Full", row.HashState);

        var second = await service.IndexAsync([CandidateFor(file)]);
        Assert.Equal(row.StableId, IndexRow(file).StableId);
    }

    [Fact]
    public async Task Index_MovedFile_ReusesStableId()
    {
        var file = WriteFile("inbox/report.txt");
        var service = new FileIndexingService(_store, new WindowsHashService());
        var original = CandidateFor(file);
        await service.IndexAsync([original]);
        var originalId = IndexRow(file).StableId;

        var moved = original with { SourceUri = Path.Combine(_root, "moved", "report.txt") };
        await service.IndexAsync([moved]);

        var rows = _db.Query("SELECT DISTINCT stable_id FROM file_index", r => r.GetString(0));
        Assert.Single(rows);
        Assert.Equal(originalId, rows[0]);
    }

    [Fact]
    public async Task Index_OversizedFile_SkipsHash()
    {
        var file = WriteFile("inbox/big.bin", "x");
        var candidate = CandidateFor(file) with { SizeBytes = 200L * 1024 * 1024 };
        var service = new FileIndexingService(_store, new WindowsHashService());

        var result = await service.IndexAsync([candidate]);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.Hashed);
        var row = IndexRow(file);
        Assert.Null(row.ContentHash);
        Assert.Equal("NotComputed", row.HashState);
    }

    [Fact]
    public async Task Index_Cancellation_Stops()
    {
        var file = WriteFile("inbox/a.txt");
        var service = new FileIndexingService(_store, new WindowsHashService());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.IndexAsync([CandidateFor(file)], cts.Token));
    }

    // ---- StartupRecoveryService ----

    private OperationRecord Record(string id, OperationStatus status, params OperationStatus[] itemStates) =>
        new()
        {
            OperationId = id, PlanId = "plan-" + id, Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = itemStates.Select((s, i) => new OperationItemState
            {
                ItemId = $"{id}-{i}", Operation = OperationType.Move,
                OriginalLocation = $"/src/{i}.txt",
                ResultingLocation = s == OperationStatus.Committed ? $"/dst/{i}.txt" : null,
                Status = s
            }).ToList()
        };

    [Fact]
    public async Task Recovery_PristinePlanned_FailsCleanly()
    {
        var svc = new StartupRecoveryService(_store);
        await ((IOperationStore)_store).SaveAsync(Record("op-p", OperationStatus.Validating,
            OperationStatus.Planned, OperationStatus.Planned));

        var report = await svc.ReconcileAsync();

        Assert.True(report.IsSuccess);
        Assert.Equal(1, report.Value!.FailedPristine);
        Assert.Equal(0, report.Value.NeedsAttention);
        var loaded = await ((IOperationStore)_store).LoadAsync("op-p");
        Assert.Equal(OperationStatus.Failed, loaded.Value!.Status);
    }

    [Fact]
    public async Task Recovery_PartialOrExecuting_NeedsAttention()
    {
        var svc = new StartupRecoveryService(_store);
        await ((IOperationStore)_store).SaveAsync(Record("op-a", OperationStatus.Executing,
            OperationStatus.Committed, OperationStatus.Planned));
        await ((IOperationStore)_store).SaveAsync(Record("op-b", OperationStatus.Executing,
            OperationStatus.Planned));
        await ((IOperationStore)_store).SaveAsync(Record("op-c", OperationStatus.Verifying,
            OperationStatus.Committed));

        var report = await svc.ReconcileAsync();

        Assert.Equal(3, report.Value!.NeedsAttention);
        foreach (var id in new[] { "op-a", "op-b", "op-c" })
            Assert.Equal(OperationStatus.NeedsAttention,
                (await ((IOperationStore)_store).LoadAsync(id)).Value!.Status);
    }

    [Fact]
    public async Task Recovery_Idempotent_TerminalUntouched()
    {
        var svc = new StartupRecoveryService(_store);
        await ((IOperationStore)_store).SaveAsync(Record("op-t", OperationStatus.Committed,
            OperationStatus.Committed));
        await ((IOperationStore)_store).SaveAsync(Record("op-p", OperationStatus.Planned,
            OperationStatus.Planned));

        var first = await svc.ReconcileAsync();
        Assert.Equal(1, first.Value!.Reconciled);
        var second = await svc.ReconcileAsync();
        Assert.Equal(0, second.Value!.Reconciled);
        Assert.Equal(OperationStatus.Committed,
            (await ((IOperationStore)_store).LoadAsync("op-t")).Value!.Status);
    }

    // ---- SmartInboxService ----

    private static RuleEvaluation EmptyEval => new()
    { MatchedRuleIds = [], ProposedActions = [], Conflicts = [], Trace = [] };

    private static AnalyzedFile Analyzed(FileCandidate c, DecisionKind kind,
        double confidence = 50, bool needsReview = false)
    {
        var cls = new ClassificationResult
        {
            FileKind = FileKind.Document, Evidence = [], Confidence = confidence,
            Sources = [ClassifierSource.Local], Explanation = "test", NeedsReview = needsReview
        };
        var decision = new Decision
        { Kind = kind, Candidate = c, Classification = cls, Reasons = ["test reason"] };
        return new AnalyzedFile(c, cls, EmptyEval, decision);
    }

    [Fact]
    public async Task SmartInbox_ReviewDecision_PersistedOnce()
    {
        var file = WriteFile("inbox/maybe.docx");
        var service = new SmartInboxService(_store);
        var analyzed = new[] { Analyzed(CandidateFor(file), DecisionKind.Review) };

        var first = await service.RecordReviewAsync(analyzed);
        Assert.Equal(1, first.Value!.Added);
        var second = await service.RecordReviewAsync(analyzed);
        Assert.Equal(0, second.Value!.Added);
        Assert.Equal(1, second.Value.AlreadyOpen);

        var open = await _store.GetOpenAsync();
        Assert.Single(open.Value!);
        Assert.Equal(file, open.Value![0].Path);

        Assert.True((await _store.ResolveAsync(open.Value![0].Id)).Value);
        Assert.Empty((await _store.GetOpenAsync()).Value!);
    }

    [Fact]
    public async Task SmartInbox_NeedsReviewOrganize_AlsoRecorded()
    {
        var file = WriteFile("inbox/flagged.pdf");
        var service = new SmartInboxService(_store);
        var result = await service.RecordReviewAsync(
            [Analyzed(CandidateFor(file), DecisionKind.Organize, confidence: 80, needsReview: true)]);
        Assert.Equal(1, result.Value!.Added);
    }

    [Fact]
    public async Task SmartInbox_ConfidentOrganize_NotRecorded()
    {
        var file = WriteFile("inbox/clean.pdf");
        var service = new SmartInboxService(_store);
        var result = await service.RecordReviewAsync(
            [Analyzed(CandidateFor(file), DecisionKind.Organize, confidence: 98)]);
        Assert.Equal(0, result.Value!.Added);
    }

    // ---- ModePolicy ----

    private static AnalyzedFile ForMode(DecisionKind kind, bool needsReview = false)
    {
        var c = Candidates.Create(@"C:\in\file.txt");
        return Analyzed(c, kind, needsReview: needsReview);
    }

    [Fact]
    public void Mode_ManualWithoutApply_PreviewOnly()
    {
        var policy = new ModePolicy(OrganizationMode.Manual);
        Assert.Equal(ItemRoute.PreviewOnly, policy.RouteFor(ForMode(DecisionKind.Organize)));
        Assert.True(policy.ExecutionRequiresApply);
    }

    [Fact]
    public void Mode_ManualWithApply_RoutesLikeSmart()
    {
        var policy = new ModePolicy(OrganizationMode.Manual);
        Assert.Equal(ItemRoute.Execute, policy.RouteFor(ForMode(DecisionKind.Organize), apply: true));
        Assert.Equal(ItemRoute.SmartInbox, policy.RouteFor(ForMode(DecisionKind.Review), apply: true));
    }

    [Fact]
    public void Mode_Smart_RoutesReviewToInbox()
    {
        var policy = new ModePolicy(OrganizationMode.Smart);
        Assert.Equal(ItemRoute.Execute, policy.RouteFor(ForMode(DecisionKind.Organize)));
        Assert.Equal(ItemRoute.SmartInbox, policy.RouteFor(ForMode(DecisionKind.Organize, needsReview: true)));
        Assert.Equal(ItemRoute.SmartInbox, policy.RouteFor(ForMode(DecisionKind.Review)));
        Assert.Equal(ItemRoute.Skip, policy.RouteFor(ForMode(DecisionKind.NoOp)));
    }

    [Fact]
    public void Mode_Automatic_ExecutesAll()
    {
        var policy = new ModePolicy(OrganizationMode.Automatic);
        Assert.Equal(ItemRoute.Execute, policy.RouteFor(ForMode(DecisionKind.Organize)));
        Assert.Equal(ItemRoute.Execute, policy.RouteFor(ForMode(DecisionKind.Review)));
        Assert.False(policy.ExecutionRequiresApply);
    }

    [Fact]
    public void Mode_Parse_KebabAndCase()
    {
        Assert.True(ModePolicy.TryParse("ai-assisted", out var m1));
        Assert.Equal(OrganizationMode.AiAssisted, m1);
        Assert.True(ModePolicy.TryParse("LOCAL-ONLY", out var m2));
        Assert.Equal(OrganizationMode.LocalOnly, m2);
        Assert.False(ModePolicy.TryParse("bogus", out _));
        Assert.Equal("local-only", ModePolicy.Display(OrganizationMode.LocalOnly));
    }

    // ---- Rule import path ----

    private static Rule SampleRule => new()
    {
        Id = "r-test", Name = "Test rule", Enabled = true, Priority = 10,
        ConditionRoot = new ConditionGroup
        {
            Operator = LogicalOperator.And,
            Conditions = [new RuleCondition
            { Field = ConditionField.Extension, Operator = ConditionOperator.Equals, Value = "pdf" }]
        },
        Actions = [new RuleAction { Kind = RuleActionKind.Move, DestinationTemplate = "Docs/{FileKind}" }],
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        SchemaVersion = 1, Provenance = RuleProvenance.User
    };

    [Fact]
    public async Task RuleJson_RoundTrip_ValidateAndPersist()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(SampleRule, FileMindDb.JsonOptions);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<Rule>(json, FileMindDb.JsonOptions)!;
        Assert.Equal(SampleRule.Id, parsed.Id);

        var imported = parsed with { Provenance = RuleProvenance.Imported };
        Assert.True(RuleValidator.Validate(imported).IsSuccess);

        await ((IRuleRepository)_store).UpsertAsync(imported);
        var loaded = await ((IRuleRepository)_store).GetAsync("r-test");
        Assert.Equal(RuleProvenance.Imported, loaded.Value!.Provenance);
        Assert.Equal("Docs/{FileKind}", loaded.Value.Actions[0].DestinationTemplate);
    }

    [Fact]
    public void RuleJson_NewerSchemaVersion_FailsSafe()
    {
        var future = SampleRule with { SchemaVersion = 99 };
        var result = RuleValidator.Validate(future);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedOperation, result.Error!.Code);
    }

    [Fact]
    public void RuleJson_Malformed_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<Rule>("{\"id\":", FileMindDb.JsonOptions));
    }
}

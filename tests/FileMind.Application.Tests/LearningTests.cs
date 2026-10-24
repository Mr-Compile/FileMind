using FileMind.Application.Decisions;
using FileMind.Application.Learning;
using FileMind.Application.Ports;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Testing;
using Xunit;

namespace FileMind.Application.Tests;

/// <summary>
/// PLAN-013 M1–M3 — correction learning (suggestion-only draft rules),
/// bounded adaptive confidence, version-chain detection. Invariant:
/// learned artifacts never activate themselves and never bypass safety.
/// </summary>
public sealed class LearningTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-learn-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;
    private readonly FakeFileSystemAdapter _fs = new();
    private readonly WindowsStylePathNormalizer _paths = new();
    private readonly CorrectionService _corrections;
    private readonly ConfidenceCalibrator _calibrator;

    public LearningTests()
    {
        Directory.CreateDirectory(_root);
        _db = FileMindDb.Open(Path.Combine(_root, "learn.db"));
        _store = new SqliteStore(_db);
        _corrections = new CorrectionService(_store, _paths);
        _calibrator = new ConfidenceCalibrator(_store);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private OrganizerPipeline Pipeline()
    {
        var confidence = new ConfidenceEngine();
        var safety = new SafetyEngine(_fs, _paths);
        return new OrganizerPipeline(
            new FileMind.Application.Scanning.FileScanner(_fs, _paths, null),
            new ClassifierEngine(
                new SubjectMatcher(new InMemorySubjectCatalog([])),
                new ProjectMatcher(new InMemoryProjectCatalog([])),
                confidence),
            new RuleEngine(), new DecisionMaker(confidence),
            new ActionPlanner(), safety,
            new TransactionEngine(_fs, _store, safety),
            new UndoService(_fs),
            _fs, _paths, new StaticProtectedPaths(),
            _store, _store, _store, _store,
            calibrator: _calibrator,
            corrections: _corrections);
    }

    private static FileCandidate Candidate(string path) => new()
    {
        ScanId = "t", SourceUri = path, DisplayName = Path.GetFileName(path),
        Extension = Path.GetExtension(path).TrimStart('.'), SizeBytes = 10,
        CreatedAt = DateTimeOffset.UnixEpoch, ModifiedAt = DateTimeOffset.UnixEpoch,
        SourceScope = SourceScope.Downloads
    };

    // ---- M1: correction learning ----

    [Fact]
    public async Task Correction_Record_Persists()
    {
        var rec = await _corrections.RecordAsync("D:/in/a.pdf", "D:/out/school/a.pdf");
        Assert.True(rec.IsSuccess);
        var all = await ((ICorrectionRepository)_store).GetAllAsync();
        Assert.Single(all.Value!);
        Assert.Equal("D:/out/school/a.pdf", all.Value![0].ChosenDestination);
    }

    [Fact]
    public async Task Corrections_BelowThreshold_NoDraft()
    {
        await _corrections.RecordAsync("D:/in/a.pdf", "D:/out/school/a.pdf");
        var mined = await _corrections.MineSuggestionsAsync();
        Assert.Empty(mined.Value!);
    }

    [Fact]
    public async Task Corrections_Agreeing_DraftRule_LearnedDisabled()
    {
        await _corrections.RecordAsync("D:/in/a.pdf", "D:/out/school/a.pdf");
        await _corrections.RecordAsync("D:/in/b.pdf", "D:/out/school/b.pdf");
        await _corrections.RecordAsync("D:/in/c.docx", "D:/out/school/c.docx"); // diff ext — not counted
        var mined = await _corrections.MineSuggestionsAsync();
        var rule = Assert.Single(mined.Value!);
        Assert.Equal(RuleProvenance.Learned, rule.Provenance);
        Assert.False(rule.Enabled); // review gate — never auto-active
        Assert.Equal(ConditionField.Extension, rule.ConditionRoot.Conditions[0].Field);
        Assert.Equal("pdf", rule.ConditionRoot.Conditions[0].Value);
        Assert.Equal("D:\\out\\school", rule.Actions[0].DestinationTemplate);
    }

    [Fact]
    public async Task Corrections_Disagreeing_NoDraft()
    {
        await _corrections.RecordAsync("D:/in/a.pdf", "D:/out/school/a.pdf");
        await _corrections.RecordAsync("D:/in/b.pdf", "D:/out/work/b.pdf");
        var mined = await _corrections.MineSuggestionsAsync();
        Assert.Empty(mined.Value!);
    }

    [Fact]
    public async Task Corrections_DestRoot_RelativeTemplate()
    {
        await _corrections.RecordAsync("D:/in/a.pdf", "D:/out/school/a.pdf");
        await _corrections.RecordAsync("D:/in/b.pdf", "D:/out/school/b.pdf");
        var mined = await _corrections.MineSuggestionsAsync(destinationRoot: "D:/out");
        Assert.Equal("school", mined.Value![0].Actions[0].DestinationTemplate);
    }

    [Fact]
    public async Task Undo_FeedsCorrections_AndCalibration()
    {
        var pipeline = Pipeline();
        var t0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        _fs.SeedFile("D:/in/a.pdf", "data", t0);
        var fp = FileIdentity.ComputeFingerprint(4, t0);
        var plan = new ActionPlan
        {
            PlanId = "p1", CreatedAt = DateTimeOffset.UtcNow, SourceDecision = "t",
            Items = [new ActionItem
            {
                Operation = OperationType.Move, Source = "D:/in/a.pdf",
                Destination = "D:/out/a.pdf", Reason = "t",
                UndoRecipe = new UndoRecipe(OperationType.Move, "D:/out/a.pdf", "D:/in/a.pdf", fp, [])
            }]
        };
        var exec = await pipeline.ExecuteAsync(plan);
        Assert.Equal(OperationStatus.Committed, exec.Value!.Status);

        var undone = await pipeline.UndoAsync(exec.Value!.OperationId);
        Assert.True(undone.IsSuccess);

        var corrections = await ((ICorrectionRepository)_store).GetAllAsync();
        Assert.Single(corrections.Value!);
        Assert.StartsWith("undo:", corrections.Value![0].Source);
        Assert.Equal("D:/in/a.pdf", corrections.Value![0].ChosenDestination);

        var adj = await ((IConfidenceCalibrationRepository)_store).GetAdjustmentAsync("ext:pdf");
        Assert.Equal(-ConfidenceCalibrator.Step, adj.Value);
    }

    // ---- M2: adaptive confidence ----

    [Fact]
    public async Task Calibrator_RejectsClamp_AtMaxAdjustment()
    {
        for (var i = 0; i < 10; i++)
            await _calibrator.RecordAsync("ext:pdf", CalibrationSignal.Rejected);
        var adj = await ((IConfidenceCalibrationRepository)_store).GetAdjustmentAsync("ext:pdf");
        Assert.Equal(-ConfidenceCalibrator.MaxAdjustment, adj.Value);
        Assert.Equal(0, await _calibrator.AdjustAsync("ext:pdf", 10)); // clamps at 0, not negative
    }

    [Fact]
    public async Task Calibrator_Pipeline_ShiftsConfidence()
    {
        var pipeline = Pipeline();
        var before = await pipeline.AnalyzeAsync([Candidate("D:/in/notes.pdf")]);
        var raw = before.Value![0].Classification.Confidence;

        for (var i = 0; i < 4; i++)
            await _calibrator.RecordAsync("ext:pdf", CalibrationSignal.Rejected);
        var after = await pipeline.AnalyzeAsync([Candidate("D:/in/notes.pdf")]);
        var adjusted = after.Value![0].Classification.Confidence;
        Assert.Equal(Math.Clamp(raw - ConfidenceCalibrator.MaxAdjustment, 0, 100), adjusted, 3);
    }

    // ---- M1 invariant: learned rules never bypass safety ----

    [Fact]
    public async Task LearnedRule_Enabled_StillBlockedBySafety()
    {
        await _corrections.RecordAsync("D:/in/a.pdf", "C:/Windows/sys/a.pdf");
        await _corrections.RecordAsync("D:/in/b.pdf", "C:/Windows/sys/b.pdf");
        var mined = await _corrections.MineSuggestionsAsync();
        var learned = mined.Value![0] with { Enabled = true }; // user activates after review

        var safety = new SafetyEngine(_fs, _paths);
        _fs.SeedFile("D:/in/a.pdf");
        var plan = new ActionPlan
        {
            PlanId = "p", CreatedAt = DateTimeOffset.UtcNow, SourceDecision = "learned",
            Items = [new ActionItem
            {
                Operation = OperationType.Move, Source = "D:/in/a.pdf",
                Destination = "C:/Windows/sys/a.pdf", Reason = learned.Name
            }]
        };
        var ctx = new SafetyContext(new StaticProtectedPaths("C:\\Windows"));
        var verdict = await safety.ValidateAsync(plan, ctx);
        Assert.False(verdict.Allowed); // safety wins regardless of provenance
        Assert.Contains(verdict.Blockers, b => b.Contains("protected"));
    }

    // ---- M3: related/version intelligence ----

    private async Task Index(params string[] paths)
    {
        var svc = new FileMind.Application.Indexing.FileIndexingService(_store);
        await svc.IndexAsync(paths.Select(p => Candidate(p)).ToList());
    }

    [Fact]
    public async Task Related_VChain_OrderedOldestToNewest()
    {
        await Index("D:/docs/report_v3.pdf", "D:/docs/report_v1.pdf", "D:/docs/report_v2.pdf");
        var svc = new RelatedFileService(_store);
        var chains = await svc.FindVersionChainsAsync();
        var chain = Assert.Single(chains.Value!);
        Assert.Equal("report", chain.BaseName);
        Assert.Equal("D:/docs/report_v3.pdf", chain.LatestPath);
        Assert.Equal(
            ["D:/docs/report_v1.pdf", "D:/docs/report_v2.pdf", "D:/docs/report_v3.pdf"],
            chain.PathsOrderedOldestToNewest);
    }

    [Fact]
    public async Task Related_ParenCopy_And_Year()
    {
        await Index("D:/d/notes.txt", "D:/d/notes (2).txt",
            "D:/d/quarterly 2024.docx", "D:/d/quarterly 2025.docx");
        var svc = new RelatedFileService(_store);
        var chains = await svc.FindVersionChainsAsync();
        Assert.Equal(2, chains.Value!.Count);
        Assert.Contains(chains.Value!, c => c.LatestPath == "D:/d/notes (2).txt");
        Assert.Contains(chains.Value!, c => c.LatestPath == "D:/d/quarterly 2025.docx");
    }

    [Fact]
    public async Task Related_Singles_NoChain_RelatedToFilters()
    {
        await Index("D:/d/lonely.pdf", "D:/d/report_v1.pdf", "D:/d/report_v2.pdf");
        var svc = new RelatedFileService(_store);
        var none = await svc.RelatedToAsync("D:/d/lonely.pdf");
        Assert.Empty(none.Value!);
        var mine = await svc.RelatedToAsync("D:/d/report_v1.pdf");
        Assert.Single(mine.Value!);
    }
}

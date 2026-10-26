using FileMind.Application.Decisions;
using FileMind.Application.Ports;
using FileMind.Application.Scanning;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Operations;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Platform.Windows;
using Xunit;

namespace FileMind.Application.Tests;

public sealed class WindowsEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-e2e-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _scanRoot;
    private readonly string _dbPath;
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;
    private readonly OrganizerPipeline _pipeline;
    private readonly WindowsPathNormalizer _paths = new();

    public WindowsEndToEndTests()
    {
        _scanRoot = Path.Combine(_root, "inbox");
        Directory.CreateDirectory(_scanRoot);
        _dbPath = _root + ".db";
        _db = FileMindDb.Open(_dbPath);
        _store = new SqliteStore(_db);

        var adapter = new WindowsFileSystemAdapter();
        var confidence = new ConfidenceEngine();
        var safety = new SafetyEngine(adapter, _paths);

        ((ISubjectRepository)_store).UpsertAsync(new SubjectLike(
            "IT421", "IT421 Networks", "IT421",
            ["IT 421", "IT-421", "IT_421"], ["networks"])).GetAwaiter().GetResult();
        ((ICollectionRepository)_store).UpsertAsync(
            new CollectionEntry("school", "School", "School/{Subject}", ["IT421"])).GetAwaiter().GetResult();

        var classifier = new ClassifierEngine(
            new SubjectMatcher(new InMemorySubjectCatalog(
            [
                new SubjectEntry("IT421", "IT421 Networks", "IT421",
                    ["IT 421", "IT-421", "IT_421"], ["networks"])
            ])),
            new ProjectMatcher(new InMemoryProjectCatalog([])),
            confidence);

        var scanner = new FileScanner(adapter, _paths,
            new ExclusionMatcher(["*.tmp", "*.part", "*.crdownload"]));

        _pipeline = new OrganizerPipeline(
            scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
            new ActionPlanner(), safety,
            new TransactionEngine(adapter, _store, safety),
            new UndoService(adapter),
            adapter, _paths, adapter.ProtectedPaths,
            (IRuleRepository)_store, (ICollectionRepository)_store,
            (IOperationRepository)_store, _store);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private string WriteFile(string relative, string content = "test content")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<IReadOnlyList<AnalyzedFile>> AnalyzeAsync()
    {
        var candidates = new List<FileCandidate>();
        await foreach (var c in _pipeline.ScanAsync(_scanRoot, recursive: true))
            candidates.Add(c);
        var analyzed = await _pipeline.AnalyzeAsync(candidates);
        Assert.True(analyzed.IsSuccess, analyzed.Error?.Message);
        return analyzed.Value!;
    }

    [Fact]
    public async Task Scan_Classify_Plan_Execute_Verify_History_Undo()
    {
        WriteFile("inbox/IT421_Lecture_5.mp4");
        WriteFile("inbox/IT-421_hw.pdf");
        WriteFile("inbox/random_unrelated.xyz");

        var analyzed = await AnalyzeAsync();
        var it421 = analyzed.Where(a => a.Classification.Subject == "IT421").ToList();
        Assert.Equal(2, it421.Count);

        var destRoot = Path.Combine(_root, "organized");
        var plan = _pipeline.Plan(analyzed, dryRun: false, destinationRoot: destRoot);
        Assert.True(plan.IsSuccess);
        var moves = plan.Value!.Items.Where(i => i.Operation == OperationType.Move).ToList();
        Assert.Equal(2, moves.Count);

        var executed = await _pipeline.ExecuteAsync(plan.Value);
        Assert.True(executed.IsSuccess, executed.Error?.Message);
        var record = executed.Value!;
        Assert.Equal(OperationStatus.Committed, record.Status);

        Assert.True(File.Exists(Path.Combine(destRoot, "School", "IT421", "Video", "IT421_Lecture_5.mp4")));
        Assert.True(File.Exists(Path.Combine(destRoot, "School", "IT421", "Document", "IT-421_hw.pdf")));
        Assert.False(File.Exists(Path.Combine(_root, "inbox", "IT421_Lecture_5.mp4")));
        Assert.True(File.Exists(Path.Combine(_root, "inbox", "random_unrelated.xyz")));

        var history = await ((IOperationRepository)_store).GetRecentAsync(10);
        Assert.Contains(history.Value!, r => r.OperationId == record.OperationId
            && r.Status == OperationStatus.Committed);

        var undone = await _pipeline.UndoAsync(record.OperationId);
        Assert.True(undone.IsSuccess, undone.Error?.Message);
        Assert.Equal(OperationStatus.Committed, undone.Value!.Status);

        Assert.True(File.Exists(Path.Combine(_root, "inbox", "IT421_Lecture_5.mp4")));
        Assert.True(File.Exists(Path.Combine(_root, "inbox", "IT-421_hw.pdf")));
        Assert.False(File.Exists(Path.Combine(destRoot, "School", "IT421", "Video", "IT421_Lecture_5.mp4")));
    }

    [Fact]
    public async Task Execute_Conflict_KeepBoth_NeverOverwrites()
    {
        var destRoot = Path.Combine(_root, "organized");
        WriteFile("inbox/IT421_notes.txt", "first file");
        WriteFile("organized/School/IT421/Document/IT421_notes.txt", "pre-existing content");

        var analyzed = await AnalyzeAsync();
        var plan = _pipeline.Plan(analyzed, dryRun: false, destinationRoot: destRoot);
        var executed = await _pipeline.ExecuteAsync(plan.Value!);
        Assert.True(executed.IsSuccess);

        var kept = File.ReadAllText(Path.Combine(destRoot, "School", "IT421", "Document", "IT421_notes.txt"));
        Assert.Equal("pre-existing content", kept);
        Assert.True(File.Exists(Path.Combine(destRoot, "School", "IT421", "Document", "IT421_notes (2).txt")));
    }

    [Fact]
    public async Task ExcludedExtensions_NotScanned()
    {
        WriteFile("inbox/partial.crdownload");
        WriteFile("inbox/done.txt");

        var candidates = new List<FileCandidate>();
        await foreach (var c in _pipeline.ScanAsync(_scanRoot, recursive: true))
            candidates.Add(c);

        Assert.Single(candidates);
        Assert.Equal("done.txt", candidates[0].DisplayName);
    }

    [Fact]
    public async Task InferredSubject_AutoGroupsWithoutCatalog()
    {
        // MATH-210 is NOT in the subject catalog — the filename cluster must
        // infer it (F-P12) and group both files, including the nested one.
        WriteFile("inbox/MATH210_hw1.pdf");
        WriteFile("inbox/nested/MATH-210 notes.docx");
        WriteFile("inbox/plain_file.xyz");

        var analyzed = await AnalyzeAsync();
        var grouped = analyzed.Where(a => a.Classification.Subject == "MATH-210").ToList();
        Assert.Equal(2, grouped.Count);
        Assert.All(grouped, a => Assert.Contains(
            a.Classification.Evidence, e => e.Kind == "inferred-subject"));
        Assert.Null(analyzed.Single(
            a => a.Candidate.DisplayName == "plain_file.xyz").Classification.Subject);

        var destRoot = Path.Combine(_root, "organized");
        var plan = _pipeline.Plan(analyzed, dryRun: false, destinationRoot: destRoot);
        Assert.True(plan.IsSuccess);
        var executed = await _pipeline.ExecuteAsync(plan.Value!);
        Assert.True(executed.IsSuccess, executed.Error?.Message);
        var decisions = string.Join("; ", analyzed.Select(a =>
            $"{a.Candidate.DisplayName}:{a.Decision.Kind}:{a.Decision.DestinationTemplate}:{a.Classification.Confidence}" +
            $" warn=[{string.Join('|', a.Classification.Warnings)}] reasons=[{string.Join('|', a.Decision.Reasons)}]"));

        var allDest = Directory.Exists(destRoot)
            ? Directory.EnumerateFiles(destRoot, "*", SearchOption.AllDirectories).ToList()
            : [];
        var underSubject = allDest.Where(
            f => f.Contains(Path.Combine("School", "MATH-210") + Path.DirectorySeparatorChar)).ToList();
        Assert.True(underSubject.Count == 2,
            $"dests=[{string.Join(", ", allDest)}] decisions=[{decisions}]");
        Assert.True(File.Exists(Path.Combine(_root, "inbox", "plain_file.xyz")));
    }
}

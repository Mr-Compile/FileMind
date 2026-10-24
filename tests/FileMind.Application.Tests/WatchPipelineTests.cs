using System.Threading.Channels;
using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Recovery;
using FileMind.Application.Scanning;
using FileMind.Application.UseCases;
using FileMind.Application.Watching;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Platform.Windows;
using Xunit;

namespace FileMind.Application.Tests;

public sealed class WatchPipelineTests : IDisposable
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeEventSource : IWatcherEventSource
    {
        private readonly Channel<WatchEvent> _ch = Channel.CreateUnbounded<WatchEvent>();
        public void Emit(string path, string kind) =>
            _ch.Writer.TryWrite(new WatchEvent(path, kind, DateTimeOffset.UtcNow.AddSeconds(-2)));
        public async IAsyncEnumerable<WatchEvent> EventsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            while (await _ch.Reader.WaitToReadAsync(ct))
                while (_ch.Reader.TryRead(out var e)) yield return e;
        }
        public Task<Result<bool>> StartWatchingAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Ok(true));
        public Task<Result<bool>> StopWatchingAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(Result<bool>.Ok(true));
    }

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "filemind-watch-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _scanRoot;
    private readonly string _destRoot;
    private readonly FileMindDb _db;
    private readonly SqliteStore _store;
    private readonly OrganizerPipeline _pipeline;
    private readonly FileIndexingService _indexing;
    private readonly SmartInboxService _inbox;
    private readonly WindowsPathNormalizer _paths = new();
    private readonly WindowsFileSystemAdapter _adapter = new();
    private readonly FakeClock _clock = new();
    private readonly List<string> _notifications = [];

    private sealed class CapturingNotify(List<string> sink) : INotificationService
    {
        public Task<Result<bool>> NotifyAsync(Notification n, CancellationToken ct = default)
        {
            sink.Add($"{n.Title}: {n.Body}");
            return Task.FromResult(Result<bool>.Ok(true));
        }
    }

    public WatchPipelineTests()
    {
        _scanRoot = Path.Combine(_root, "inbox");
        _destRoot = Path.Combine(_root, "organized");
        Directory.CreateDirectory(_scanRoot);
        _db = FileMindDb.Open(Path.Combine(_root, "test.db"));
        _store = new SqliteStore(_db);

        var confidence = new ConfidenceEngine();
        var safety = new SafetyEngine(_adapter, _paths);
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
        var scanner = new FileScanner(_adapter, _paths, new ExclusionMatcher(["*.tmp"]));
        _indexing = new FileIndexingService(_store, new WindowsHashService());
        _inbox = new SmartInboxService(_store);
        _pipeline = new OrganizerPipeline(
            scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
            new ActionPlanner(), safety,
            new TransactionEngine(_adapter, _store, safety),
            new UndoService(_adapter),
            _adapter, _paths, _adapter.ProtectedPaths,
            (IRuleRepository)_store, (ICollectionRepository)_store,
            (IOperationRepository)_store, _store, _indexing);
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

    private WatchOrchestrator Orchestrator(FileStabilityChecker? stability = null) =>
        new(_pipeline, _indexing, _inbox, _paths,
            stability ?? new FileStabilityChecker(_adapter, pollCount: 2, pollInterval: TimeSpan.FromMilliseconds(10)),
            _store, new CapturingNotify(_notifications), _destRoot);

    // ---- WatcherEventQueue ----

    [Fact]
    public void Queue_DedupesKeepsNewest_OverflowFlag()
    {
        var q = new WatcherEventQueue(capacity: 2);
        var now = DateTimeOffset.UtcNow;
        Assert.True(q.TryEnqueue(new WatchEvent("a.txt", "Created", now), now));
        Assert.True(q.TryEnqueue(new WatchEvent("a.txt", "Created", now.AddSeconds(1)), now.AddSeconds(1)));
        Assert.Equal(1, q.Count); // same (path,kind) collapsed

        q.TryEnqueue(new WatchEvent("b.txt", "Created", now), now);
        q.TryEnqueue(new WatchEvent("c.txt", "Created", now), now); // over capacity
        Assert.True(q.Overflowed);
        Assert.Equal(2, q.Count);
        Assert.Equal("b.txt", q.Dequeue()!.Event.Path);
    }

    // ---- DebounceService ----

    [Fact]
    public void Debounce_LatestEventWins_DueAfterWindow()
    {
        var deb = new DebounceService(_clock, TimeSpan.FromMilliseconds(500));
        deb.Add(new WatchEvent("f.txt", "Created", _clock.UtcNow));
        _clock.UtcNow = _clock.UtcNow.AddMilliseconds(300);
        Assert.Empty(deb.CollectDue());
        deb.Add(new WatchEvent("f.txt", "Changed", _clock.UtcNow)); // reset window
        _clock.UtcNow = _clock.UtcNow.AddMilliseconds(600);
        var due = deb.CollectDue();
        Assert.Single(due);
        Assert.Equal("Changed", due[0].ChangeKind);
        Assert.Empty(deb.CollectDue()); // drained
    }

    // ---- FileStabilityChecker ----

    [Fact]
    public async Task Stability_StableFile_Passes()
    {
        var f = WriteFile("inbox/a.txt");
        var result = await new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(10))
            .CheckAsync(f);
        Assert.Equal(StabilityVerdict.Stable, result.Verdict);
        Assert.NotNull(result.Stat);
    }

    [Fact]
    public async Task Stability_IncompleteExtensions_NeverStable()
    {
        foreach (var ext in new[] { "crdownload", "part", "partial", "download", "tmp" })
        {
            var f = WriteFile($"inbox/x.{ext}");
            var r = await new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(5))
                .CheckAsync(f);
            Assert.Equal(StabilityVerdict.IncompleteDownload, r.Verdict);
        }
    }

    [Fact]
    public async Task Stability_MissingPath()
    {
        var r = await new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(5))
            .CheckAsync(Path.Combine(_root, "inbox", "ghost.txt"));
        Assert.Equal(StabilityVerdict.Missing, r.Verdict);
    }

    [Fact]
    public async Task Stability_ActivelyWrittenFile_StillChanging()
    {
        var f = WriteFile("inbox/growing.bin", "x");
        using var cts = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 40 && !cts.IsCancellationRequested; i++)
            {
                File.AppendAllText(f, "more data ");
                await Task.Delay(5, cts.Token).ContinueWith(_ => { });
            }
        });
        var r = await new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(60))
            .CheckAsync(f);
        cts.Cancel();
        await writer;
        Assert.Equal(StabilityVerdict.StillChanging, r.Verdict);
    }

    // ---- WatchOrchestrator E2E ----

    [Fact]
    public async Task Orchestrator_CreatedStableFile_AutoOrganizes()
    {
        var f = WriteFile("inbox/IT421_Lecture_5.mp4");
        var orch = Orchestrator();
        var result = await orch.ProcessEventAsync(
            new WatchEvent(f, "Created", DateTimeOffset.UtcNow),
            OrganizationMode.Automatic);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var act = Assert.Single(result.Value!.Actions);
        Assert.Equal("Execute", act.Route);
        Assert.False(File.Exists(f));
        Assert.True(File.Exists(Path.Combine(_destRoot, "School", "IT421", "IT421_Lecture_5.mp4")));
        Assert.Contains(_notifications, n => n.StartsWith("File organized"));
    }

    [Fact]
    public async Task Orchestrator_Crdownload_Skipped()
    {
        var f = WriteFile("inbox/video.crdownload");
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(f, "Created", DateTimeOffset.UtcNow),
            OrganizationMode.Automatic);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Actions);
        Assert.Single(result.Value.Warnings);
        Assert.True(File.Exists(f));
    }

    [Fact]
    public async Task Orchestrator_ReviewDecision_RoutesToInbox()
    {
        // An Ask rule forces DecisionKind.Review regardless of classification confidence.
        var rules = (IRuleRepository)_store;
        var upsert = await rules.UpsertAsync(new Rule
        {
            Id = "ask-zzz", Name = "Ask on .zzz", Enabled = true, Priority = 5,
            SchemaVersion = 1,
            ConditionRoot = new ConditionGroup
            {
                Operator = LogicalOperator.And,
                Conditions = [new RuleCondition
                {
                    Field = ConditionField.Extension,
                    Operator = ConditionOperator.Equals,
                    Value = "zzz"
                }]
            },
            Actions = [new RuleAction { Kind = RuleActionKind.Ask }]
        });
        Assert.True(upsert.IsSuccess, upsert.Error?.Message);

        var f = WriteFile("inbox/mysterious.zzz");
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(f, "Created", DateTimeOffset.UtcNow),
            OrganizationMode.Smart);
        Assert.True(result.IsSuccess);
        var open = (await _store.GetOpenAsync()).Value!;
        Assert.Contains(open, i => Path.GetFullPath(i.Path) == Path.GetFullPath(f));
        Assert.True(File.Exists(f));
    }

    [Fact]
    public async Task Orchestrator_Deleted_RemovesFromIndex()
    {
        var f = WriteFile("inbox/vanish.txt");
        await _indexing.IndexAsync([new FileCandidate
        {
            ScanId = "s", SourceUri = f, DisplayName = "vanish.txt", Extension = "txt",
            SizeBytes = 1, CreatedAt = DateTimeOffset.UtcNow, ModifiedAt = DateTimeOffset.UtcNow,
            SourceScope = SourceScope.WatchedFolder
        }]);
        File.Delete(f);
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(f, "Deleted", DateTimeOffset.UtcNow),
            OrganizationMode.Smart);
        Assert.True(result.IsSuccess);
        var act = Assert.Single(result.Value!.Actions);
        Assert.Equal("IndexRemove", act.Route);
    }

    [Fact]
    public async Task Orchestrator_ErrorEvent_WarnsRescan()
    {
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(_scanRoot, "Error", DateTimeOffset.UtcNow),
            OrganizationMode.Smart);
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Warnings, w => w.Contains("rescan"));
    }

    // ---- SqliteJobQueue + recovery ----

    [Fact]
    public async Task JobQueue_EnqueueDequeueCompleteFail_Persists()
    {
        var jq = new SqliteJobQueue(_store);
        var enq = await jq.EnqueueAsync(new JobRequest("organize", "{\"path\":\"x\"}"));
        Assert.True(enq.IsSuccess);

        var deq = await jq.DequeueAsync();
        Assert.Equal(enq.Value!.Id, deq.Value!.Id);
        Assert.Equal("running", deq.Value.Status);
        Assert.Null((await jq.DequeueAsync()).Value); // claimed

        Assert.True((await jq.CompleteAsync(deq.Value.Id)).Value);
        var done = await ((IJobRepository)_store).GetByStatusAsync("done");
        Assert.Single(done.Value!);

        var enq2 = await jq.EnqueueAsync(new JobRequest("organize", "{}"));
        var deq2 = await jq.DequeueAsync();
        Assert.True((await jq.FailAsync(deq2.Value!.Id,
            FileMindError.Io("boom"))).Value);
        var failed = await ((IJobRepository)_store).GetByStatusAsync("failed:TransientIo");
        Assert.Single(failed.Value!);
    }

    [Fact]
    public async Task Recovery_RunningJob_Requeued()
    {
        var jq = new SqliteJobQueue(_store);
        await jq.EnqueueAsync(new JobRequest("organize", "{}"));
        var deq = await jq.DequeueAsync(); // leaves it 'running'

        var svc = new StartupRecoveryService(_store, _store);
        var report = await svc.ReconcileAsync();

        Assert.Equal(1, report.Value!.JobsRequeued);
        var pending = await ((IJobRepository)_store).GetPendingAsync();
        Assert.Single(pending.Value!);
        Assert.Equal(deq.Value!.Id, pending.Value![0].Id);
    }

    // ---- WindowsWatcherEventSource (real FileSystemWatcher) ----

    [Fact]
    public async Task WindowsSource_RealFSW_EmitsCreated()
    {
        using var src = new WindowsWatcherEventSource();
        var started = await src.StartWatchingAsync(_scanRoot);
        Assert.True(started.IsSuccess, started.Error?.Message);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var received = new TaskCompletionSource<WatchEvent>();
        var pump = Task.Run(async () =>
        {
            await foreach (var e in src.EventsAsync(cts.Token))
            {
                if (e.ChangeKind == "Created") { received.TrySetResult(e); break; }
            }
        });
        await Task.Delay(300); // let FSW arm
        var path = WriteFile("inbox/dropped.txt");
        var done = await Task.WhenAny(received.Task, Task.Delay(10_000));
        Assert.True(done == received.Task, "no Created event within 10s");
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath((await received.Task).Path));
        cts.Cancel();
    }

    // ---- WatchService full loop ----

    [Fact]
    public async Task WatchService_DroppedFile_OrganizesEndToEnd()
    {
        var f = WriteFile("inbox/IT421_Lecture_9.mp4");
        var src = new FakeEventSource();
        var orch = Orchestrator(new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(10)));
        var svc = new WatchService(src, new WatcherEventQueue(), new DebounceService(_clock,
            TimeSpan.FromMilliseconds(10)), orch, _clock, pollMs: 5);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = svc.RunAsync(OrganizationMode.Automatic, apply: true, router: null, ct: cts.Token);
        await Task.Delay(50);
        src.Emit(f, "Created");
        // advance fake clock past debounce window so the event becomes due
        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        await Task.Delay(300);
        cts.Cancel();
        var run = await runTask;

        Assert.True(run.IsSuccess, run.Error?.Message);
        Assert.True(run.Value!.EventsProcessed >= 1);
        Assert.True(File.Exists(Path.Combine(_destRoot, "School", "IT421", "IT421_Lecture_9.mp4")));
    }

    [Fact]
    public async Task WatchService_WithJobQueue_PersistsAndCompletesJob()
    {
        var f = WriteFile("inbox/IT421_Lecture_10.mp4");
        var src = new FakeEventSource();
        var jq = new SqliteJobQueue((IJobRepository)_store);
        var orch = Orchestrator(new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(10)));
        var svc = new WatchService(src, new WatcherEventQueue(), new DebounceService(_clock,
            TimeSpan.FromMilliseconds(10)), orch, _clock, pollMs: 5, jobs: jq);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = svc.RunAsync(OrganizationMode.Automatic, apply: true, router: null, ct: cts.Token);
        await Task.Delay(50);
        src.Emit(f, "Created");
        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        await Task.Delay(400);
        cts.Cancel();
        var run = await runTask;

        Assert.True(run.IsSuccess, run.Error?.Message);
        Assert.True(File.Exists(Path.Combine(_destRoot, "School", "IT421", "IT421_Lecture_10.mp4")));
        var done = await ((IJobRepository)_store).GetByStatusAsync("done");
        Assert.Contains(done.Value!, j => j.Request.JobType == WatchJobPayload.JobType);
    }

    [Fact]
    public async Task WatchService_NoDanglingRunningJob()
    {
        // A missing path exercises the failure edge of the persisted-job path:
        // whatever the outcome, no job may be left in 'running' state.
        var src = new FakeEventSource();
        var jq = new SqliteJobQueue((IJobRepository)_store);
        var orch = Orchestrator(new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(10)));
        var svc = new WatchService(src, new WatcherEventQueue(), new DebounceService(_clock,
            TimeSpan.FromMilliseconds(10)), orch, _clock, pollMs: 5, jobs: jq);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = svc.RunAsync(OrganizationMode.Automatic, apply: true, router: null, ct: cts.Token);
        await Task.Delay(50);
        src.Emit(Path.Combine(_root, "inbox", "ghost-never-created.txt"), "Created");
        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        await Task.Delay(400);
        cts.Cancel();
        var run = await runTask;

        Assert.True(run.IsSuccess);
        // missing file -> orchestrator treats as Deleted -> index remove (success path)
        // OR a warning; either way no 'running' job may be left behind
        var running = await ((IJobRepository)_store).GetByStatusAsync("running");
        Assert.Empty(running.Value!);
    }

    // ---- Preserve-tree (PLAN-018) ----

    [Fact]
    public async Task Orchestrator_PreserveTree_RecreatesSubdirUnderDest()
    {
        var f = WriteFile("inbox/receipts/jan/IT421_notes.pdf");
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(f, "Created", DateTimeOffset.UtcNow),
            OrganizationMode.Automatic,
            sourceRoot: _scanRoot, preserveTree: true);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var act = Assert.Single(result.Value!.Actions);
        Assert.Equal("Execute", act.Route);
        Assert.True(File.Exists(Path.Combine(
            _destRoot, "School", "IT421", "receipts", "jan", "IT421_notes.pdf")));
    }

    [Fact]
    public async Task Orchestrator_NoPreserveTree_FlattensAsBefore()
    {
        var f = WriteFile("inbox/receipts/jan/IT421_notes.pdf");
        var result = await Orchestrator().ProcessEventAsync(
            new WatchEvent(f, "Created", DateTimeOffset.UtcNow),
            OrganizationMode.Automatic,
            sourceRoot: _scanRoot, preserveTree: false);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(File.Exists(Path.Combine(_destRoot, "School", "IT421", "IT421_notes.pdf")));
    }

    [Fact]
    public async Task WatchService_RouterPreserveTree_ThreadsThroughJobPayload()
    {
        var f = WriteFile("inbox/receipts/jan/IT421_hw.pdf");
        var src = new FakeEventSource();
        var jq = new SqliteJobQueue((IJobRepository)_store);
        var orch = Orchestrator(new FileStabilityChecker(_adapter, 2, TimeSpan.FromMilliseconds(10)));
        var svc = new WatchService(src, new WatcherEventQueue(), new DebounceService(_clock,
            TimeSpan.FromMilliseconds(10)), orch, _clock, pollMs: 5, jobs: jq);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var runTask = svc.RunAsync(OrganizationMode.Automatic, apply: true,
            router: _ => (OrganizationMode.Automatic, true, _scanRoot, true), ct: cts.Token);
        await Task.Delay(50);
        src.Emit(f, "Created");
        _clock.UtcNow = _clock.UtcNow.AddSeconds(5);
        await Task.Delay(400);
        cts.Cancel();
        var run = await runTask;

        Assert.True(run.IsSuccess, run.Error?.Message);
        Assert.True(File.Exists(Path.Combine(
            _destRoot, "School", "IT421", "receipts", "jan", "IT421_hw.pdf")));
    }
}

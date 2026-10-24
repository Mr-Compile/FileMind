using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Recovery;
using FileMind.Application.Scanning;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Operations;
using FileMind.Domain.Ports;
using FileMind.Domain.Rules;
using FileMind.Domain.Safety;
using FileMind.Infrastructure.Sqlite;
using FileMind.Platform.Windows;

namespace FileMind.WinUI.Services;

/// <summary>
/// PLAN-014 M4 — composition root mirroring FileMind.Windows.Host wiring:
/// same SQLite store, same pipeline, same ports. UI calls use cases only —
/// zero direct FS mutation; everything flows Decision + Safety + ActionPlan.
/// </summary>
public sealed class FmBackend : IAsyncDisposable
{
    public FileMindDb Db { get; private set; } = null!;
    public SqliteStore Store { get; private set; } = null!;
    public OrganizerPipeline Pipeline { get; private set; } = null!;
    public FileMind.Application.Search.SearchUseCase Search { get; private set; } = null!;
    public FileMind.Application.Duplicates.StorageAnalysisService Storage { get; private set; } = null!;
    public FileMind.Application.Duplicates.DuplicateDetectionService Duplicates { get; private set; } = null!;
    public SmartInboxService Inbox { get; private set; } = null!;
    public ModePolicy ModePolicy { get; private set; } = null!;
    public int RecoveredOnStart { get; private set; }

    public static async Task<FmBackend> OpenAsync()
    {
        var be = new FmBackend();
        var dbPath = Environment.GetEnvironmentVariable("FILEMIND_DB")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FileMind", "filemind.db");
        be.Db = FileMindDb.Open(dbPath);
        be.Store = new SqliteStore(be.Db);

        var paths = new WindowsPathNormalizer();
        var userProtected = (await ((IProtectedPathRepository)be.Store).GetUserPathsAsync()).Value ?? [];
        var adapter = new WindowsFileSystemAdapter(userProtected);

        var subjects = (await ((ISubjectRepository)be.Store).GetAllAsync()).Value ?? [];
        var projects = (await ((IProjectRepository)be.Store).GetAllAsync()).Value ?? [];
        var exclusions = (await ((IExclusionRepository)be.Store).GetPatternsAsync()).Value ?? [];

        var confidence = new ConfidenceEngine();
        var classifier = new ClassifierEngine(
            new SubjectMatcher(new InMemorySubjectCatalog(
                subjects.Select(s => new SubjectEntry(s.Id, s.Name, s.Code, s.Aliases, s.Keywords)).ToList())),
            new ProjectMatcher(new InMemoryProjectCatalog(
                projects.Select(p => new ProjectEntry(p.Id, p.Name, p.ClientName, p.Aliases, p.Keywords)).ToList())),
            confidence);

        var defaultExclusions = new[] { "*.tmp", "*.part", "*.crdownload", "desktop.ini", "thumbs.db" };
        var scanner = new FileScanner(adapter, paths,
            new ExclusionMatcher(defaultExclusions.Concat(exclusions)));

        var safety = new SafetyEngine(adapter, paths);
        var hashing = new WindowsHashService();
        var indexing = new FileIndexingService(be.Store, hashing, searchIndex: (ISearchIndex)be.Store);
        be.Search = new FileMind.Application.Search.SearchUseCase(
            (ISearchIndex)be.Store, (IFileIndexRepository)be.Store);

        var modeSetting = (await ((ISettingsRepository)be.Store).GetAsync(ModePolicy.SettingsKey)).Value;
        var currentMode = modeSetting is not null && ModePolicy.TryParse(modeSetting, out var savedMode)
            ? savedMode
            : OrganizationMode.Manual;
        be.ModePolicy = new ModePolicy(currentMode);

        // AI (opt-in, off by default) — identical wiring to the host.
        FileMind.Intelligence.AI.DotEnv.LoadIntoEnvironment();
        var dpapiSecrets = new WindowsSecretStore();
        var aiClock = new SystemClock();
        var aiOptions = FileMind.Intelligence.AI.GroqOptions.Load((ISettingsRepository)be.Store, null);
        var aiUsageLedger = (IAiUsageLedger)be.Store;
        var aiQuota = new FileMind.Intelligence.AI.AiQuotaGate(aiUsageLedger, aiClock, aiOptions.Quota);
        var aiClient = new FileMind.Intelligence.AI.GroqChatClient(
            new HttpClient(), aiOptions, dpapiSecrets, aiClock, aiUsageLedger, aiQuota.Observe);
        var aiProvider = new FileMind.Intelligence.AI.GroqProvider(
            aiOptions, aiClient, aiQuota,
            new FileMind.Intelligence.AI.AiSuggestionCache(
                aiClock, ttl: TimeSpan.FromMinutes(aiOptions.Quota.CacheTtlMinutes)),
            (IAiAuditRepository)be.Store, aiClock);
        var aiGate = new FileMind.Intelligence.AI.AiPrivacyGate(aiOptions, adapter.ProtectedPaths);
        var aiAdvisor = new FileMind.Intelligence.AI.AiAdvisor(
            aiGate, aiProvider, confidence, () => currentMode, (IAiAuditRepository)be.Store);

        be.Duplicates = new FileMind.Application.Duplicates.DuplicateDetectionService(
            (IFileIndexRepository)be.Store, hashing, adapter);
        be.Storage = new FileMind.Application.Duplicates.StorageAnalysisService(
            (IFileIndexRepository)be.Store);
        be.Inbox = new SmartInboxService(be.Store);

        var recoveryService = new StartupRecoveryService(be.Store, be.Store);
        var correctionService = new FileMind.Application.Learning.CorrectionService(be.Store, paths);
        var calibrator = new FileMind.Application.Learning.ConfidenceCalibrator(be.Store);
        be.Pipeline = new OrganizerPipeline(
            scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
            new ActionPlanner(), safety,
            new TransactionEngine(adapter, be.Store, safety),
            new UndoService(adapter),
            adapter, paths, adapter.ProtectedPaths,
            (IRuleRepository)be.Store, (ICollectionRepository)be.Store,
            (IOperationRepository)be.Store, (IOperationStore)be.Store, indexing,
            enricher: new FileMind.Intelligence.Local.Analysis.LocalAnalyzerService(),
            advisor: aiAdvisor,
            calibrator: calibrator,
            corrections: correctionService);

        var recovered = await recoveryService.ReconcileAsync();
        be.RecoveredOnStart = recovered.IsSuccess ? recovered.Value!.Reconciled : 0;
        return be;
    }

    public ValueTask DisposeAsync()
    {
        Db.Dispose();
        return ValueTask.CompletedTask;
    }
}

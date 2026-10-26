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

var dbPath = Environment.GetEnvironmentVariable("FILEMIND_DB")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FileMind", "filemind.db");

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        FileMind - offline-first file organizer (Windows backend)

        Usage:
          filemind scan <dir> [--shallow]
          filemind analyze <dir> [--shallow]
          filemind plan <dir> [--dest <root>] [--shallow] [--preserve-tree]
          filemind execute <dir> [--dest <root>] [--dry-run] [--apply] [--shallow] [--preserve-tree]
          filemind undo <operationId>
          filemind history [count]
          filemind smart-inbox [resolve <id>]
          filemind watch [run] [--seconds N] [--apply]
          filemind watch add <dir> [--mode <mode>] [--tree]
          filemind watch remove <dir>
          filemind watch list
          filemind watch tree <dir> [on|off]
          filemind watch-status
          filemind mode [<manual|smart|automatic|ai-assisted|local-only>]
          filemind rules [list]
          filemind rules show <id>
          filemind rules test <id|rule.json> <dir>
          filemind rules import <file>
          filemind rules export <id> [--out <file>]
          filemind enrich <dir> [--shallow]
          filemind dupes [<prefix>] [--plan]
          filemind storage [<prefix>]
          filemind search [text] [--name <s>] [--ext <e>] [--kind <k>] [--older-than <d>] [--newer-than <d>] [--limit <n>]
          filemind ai-status
          filemind ai-usage [count]
          filemind ai-key <set <key>|clear|status>
          filemind ai-suggest <file>
          filemind ai-rule <natural language>
          filemind ai-search <natural language>
          filemind learn [list]
          filemind learn record <path> <chosen-destination>
          filemind learn suggest [--dest <root>]
          filemind learn calibration
          filemind related [<path>]
          filemind profile export [--out <file>]
          filemind profile import <file> [--preview|--apply] [--policy merge|skip]
          filemind profile backup [--dir <dir>]
          filemind profile restore <file>
          filemind seed-demo

        Environment:
          FILEMIND_DB   SQLite database path (default: %LOCALAPPDATA%\FileMind\filemind.db)
          FILEMIND_DEST Default destination root for plan/execute
        """);
    return 0;
}

var command = args[0].ToLowerInvariant();
var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
var flags = args.Skip(1).Where(a => a.StartsWith("--")).ToHashSet();
string? flagValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

using var db = FileMindDb.Open(dbPath);
var store = new SqliteStore(db);
IRuleRepository rulesRepo = store;
ISubjectRepository subjectsRepo = store;
IProjectRepository projectsRepo = store;
ICollectionRepository collectionsRepo = store;
IOperationRepository opsRepo = store;
IOperationStore opStore = store;
IExclusionRepository exclusionsRepo = store;
IProtectedPathRepository protectedRepo = store;

var paths = new WindowsPathNormalizer();
var userProtected = (await protectedRepo.GetUserPathsAsync()).Value ?? [];
var adapter = new WindowsFileSystemAdapter(userProtected);

var subjects = (await subjectsRepo.GetAllAsync()).Value ?? [];
var projects = (await projectsRepo.GetAllAsync()).Value ?? [];
var exclusions = (await exclusionsRepo.GetPatternsAsync()).Value ?? [];

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
var indexing = new FileIndexingService(store, hashing, searchIndex: (ISearchIndex)store);
var searchUseCase = new FileMind.Application.Search.SearchUseCase(
    (ISearchIndex)store, (IFileIndexRepository)store);

var modeSetting = (await ((ISettingsRepository)store).GetAsync(ModePolicy.SettingsKey)).Value;
var currentMode = modeSetting is not null && ModePolicy.TryParse(modeSetting, out var savedMode)
    ? savedMode
    : OrganizationMode.Manual;
var modePolicy = new ModePolicy(currentMode);

// AI (opt-in, off by default): .env lifts into env vars first; GroqChatClient
// resolves the key env/.env -> DPAPI secret store. Advisory only.
FileMind.Intelligence.AI.DotEnv.LoadIntoEnvironment();
var dpapiSecrets = new WindowsSecretStore();
var aiClock = new SystemClock();
var aiOptions = FileMind.Intelligence.AI.GroqOptions.Load((ISettingsRepository)store, null);
var aiUsageLedger = (IAiUsageLedger)store;
var aiQuota = new FileMind.Intelligence.AI.AiQuotaGate(aiUsageLedger, aiClock, aiOptions.Quota);
var aiClient = new FileMind.Intelligence.AI.GroqChatClient(
    new HttpClient(), aiOptions, dpapiSecrets, aiClock, aiUsageLedger, aiQuota.Observe);
var aiProvider = new FileMind.Intelligence.AI.GroqProvider(
    aiOptions, aiClient, aiQuota,
    new FileMind.Intelligence.AI.AiSuggestionCache(
        aiClock, ttl: TimeSpan.FromMinutes(aiOptions.Quota.CacheTtlMinutes)),
    (IAiAuditRepository)store, aiClock);
var aiGate = new FileMind.Intelligence.AI.AiPrivacyGate(aiOptions, adapter.ProtectedPaths);
var aiAdvisor = new FileMind.Intelligence.AI.AiAdvisor(
    aiGate, aiProvider, confidence, () => currentMode, (IAiAuditRepository)store);
var dupeService = new FileMind.Application.Duplicates.DuplicateDetectionService(
    (IFileIndexRepository)store, hashing, adapter);
var storageService = new FileMind.Application.Duplicates.StorageAnalysisService(
    (IFileIndexRepository)store);
var inboxService = new SmartInboxService(store);
var profileSvc = new FileMind.Application.Profiles.ProfileService(
    rulesRepo, collectionsRepo, subjectsRepo, projectsRepo, exclusionsRepo,
    (ISettingsRepository)store, (IProfileRepository)store,
    writeText: async (p, c, ct) =>
    {
        try { await File.WriteAllTextAsync(p, c, ct); return Result<bool>.Ok(true); }
        catch (Exception e) { return Result<bool>.Fail(FileMindError.Io(e.Message)); }
    },
    readText: async (p, ct) =>
    {
        try { return Result<string>.Ok(await File.ReadAllTextAsync(p, ct)); }
        catch (Exception e) { return Result<string>.Fail(FileMindError.Io(e.Message)); }
    });
var recoveryService = new StartupRecoveryService(store, store);
var correctionService = new FileMind.Application.Learning.CorrectionService(store, paths);
var calibrator = new FileMind.Application.Learning.ConfidenceCalibrator(store);
var pipeline = new OrganizerPipeline(
    scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
    new ActionPlanner(), safety,
    new TransactionEngine(adapter, store, safety),
    new UndoService(adapter),
    adapter, paths, adapter.ProtectedPaths,
    rulesRepo, collectionsRepo, opsRepo, opStore, indexing,
    enricher: new FileMind.Intelligence.Local.Analysis.LocalAnalyzerService(),
    advisor: aiAdvisor,
    calibrator: calibrator,
    corrections: correctionService);

var recovered = await recoveryService.ReconcileAsync();
if (recovered.IsSuccess && recovered.Value!.Reconciled > 0)
    Console.WriteLine($"recovered {recovered.Value.Reconciled} operation(s): " +
        $"{recovered.Value.NeedsAttention} needs-attention, {recovered.Value.FailedPristine} failed-pristine");

var recursive = !flags.Contains("--shallow");
var dest = flagValue("--dest") ?? Environment.GetEnvironmentVariable("FILEMIND_DEST");

switch (command)
{
    case "seed-demo":
    {
        await subjectsRepo.UpsertAsync(new SubjectLike("IT421", "IT421 Networks", "IT421",
            ["IT 421", "IT-421", "IT_421"], ["networks", "lecture"]));
        await projectsRepo.UpsertAsync(new ProjectLike("alpha", "Alpha Rollout", "Contoso",
            ["alpha"], ["rollout"]));
        await collectionsRepo.UpsertAsync(new CollectionEntry("school", "School", "School/{Subject}/{FileKind}", ["IT421"]));
        await collectionsRepo.UpsertAsync(new CollectionEntry("work", "Work", "Work/{Project}", ["alpha"]));
        Console.WriteLine("Seeded demo catalog: subject IT421, project alpha, collections school/work.");
        return 0;
    }

    case "scan":
    {
        var root = RequiredDir(positional);
        var scanned = new List<FileCandidate>();
        await foreach (var c in pipeline.ScanAsync(root, recursive))
        {
            Console.WriteLine($"{c.DisplayName,-40} {c.SizeBytes,10:N0}  {c.SourceScope}");
            scanned.Add(c);
        }
        var idx = await pipeline.IndexAsync(scanned);
        Console.WriteLine($"-- {scanned.Count} file(s); indexed {idx.Value!.Indexed}, hashed {idx.Value.Hashed}");
        return 0;
    }

    case "analyze":
    {
        var analyzed = await AnalyzeDirAsync();
        foreach (var a in analyzed)
        {
            var c = a.Classification;
            Console.WriteLine($"{a.Candidate.DisplayName,-40} kind={c.FileKind,-12} conf={c.Confidence,5:0} " +
                $"subject={c.Subject ?? "-"} project={c.Project ?? "-"} -> {a.Decision.Kind}");
            if (a.Decision.Kind == DecisionKind.Review)
                Console.WriteLine($"    review: {string.Join("; ", a.Decision.Reasons)}");
        }
        return 0;
    }

    case "enrich":
    {
        var analyzed = await AnalyzeDirAsync();
        foreach (var a in analyzed)
        {
            var c = a.Classification;
            var extracted = a.Candidate.ExtractedTextRef;
            var meta = a.Candidate.Metadata
                .Where(kv => !kv.Key.StartsWith("text.") && !kv.Key.StartsWith("code."))
                .Select(kv => $"{kv.Key}={kv.Value}");
            var metaStr = meta.Any() ? string.Join(", ", meta.Take(6)) : "-";
            var enrichEv = c.Evidence.Where(e => e.Kind.StartsWith("content-")
                || e.Kind.Contains("extract") || e.Kind.Contains("title")
                || e.Kind.Contains("manifest") || e.Kind.Contains("id3")
                || e.Kind.Contains("exif") || e.Kind.Contains("ooxml")
                || e.Kind.Contains("pdf") || e.Kind.Contains("code")
                || e.Kind.Contains("video") || e.Kind.Contains("audio")
                || e.Kind.Contains("image")).ToList();
            Console.WriteLine($"{a.Candidate.DisplayName,-40} kind={c.FileKind,-12} " +
                $"extracted={(extracted?.Length ?? 0),5}ch meta=[{metaStr}]");
            foreach (var e in enrichEv)
                Console.WriteLine($"    ev {e.Kind,-28} +{e.ScoreContribution,5:0.##}  {e.Detail}");
            foreach (var w in c.Warnings) Console.WriteLine($"    warn: {w}");
        }
        return 0;
    }

    case "plan":
    {
        var analyzed = await AnalyzeDirAsync();
        var plan = pipeline.Plan(analyzed, dryRun: true, destinationRoot: dest ?? DefaultDest(positional[0]),
            sourceRoot: positional[0], preserveTree: flags.Contains("--preserve-tree"));
        if (!plan.IsSuccess) return Fail(plan.Error!.Message);
        PrintPlan(plan.Value);
        return 0;
    }

    case "execute":
    {
        var analyzed = await AnalyzeDirAsync();
        var apply = flags.Contains("--apply");
        var previewOnly = modePolicy.ExecutionRequiresApply && !apply;
        var executable = analyzed
            .Where(a => modePolicy.RouteFor(a, apply || previewOnly) == ItemRoute.Execute).ToList();
        Console.WriteLine($"-- mode={ModePolicy.Display(currentMode)}; {executable.Count} item(s) routed to execute");
        var plan = pipeline.Plan(executable, dryRun: flags.Contains("--dry-run") || previewOnly,
            destinationRoot: dest ?? DefaultDest(positional[0]),
            sourceRoot: positional[0], preserveTree: flags.Contains("--preserve-tree"));
        if (!plan.IsSuccess) return Fail(plan.Error!.Message);
        PrintPlan(plan.Value);

        var check = await pipeline.DryRunAsync(plan.Value);
        var safetyResult = check.Value!;
        foreach (var w in safetyResult.Warnings) Console.WriteLine($"  warn: {w}");
        if (!safetyResult.Allowed)
        {
            foreach (var b in safetyResult.Blockers) Console.WriteLine($"  blocked: {b}");
            return Fail("Safety validation failed; nothing executed.");
        }
        if (previewOnly)
        {
            Console.WriteLine("-- mode=manual: preview only. Re-run with --apply to execute.");
            return 0;
        }
        if (plan.Value.DryRun)
        {
            Console.WriteLine("-- dry-run: no changes made.");
            return 0;
        }

        var executed = await pipeline.ExecuteAsync(plan.Value);
        if (!executed.IsSuccess) return Fail(executed.Error!.Message);
        var record = executed.Value!;
        Console.WriteLine($"operation {record.OperationId}: {record.Status}");
        foreach (var item in record.Items)
            Console.WriteLine($"  {item.Operation,-10} {item.OriginalLocation} -> {item.ResultingLocation} [{item.Status}]");
        return record.Status == OperationStatus.Committed ? 0 : 2;
    }

    case "undo":
    {
        if (positional.Count == 0) return Fail("usage: filemind undo <operationId>");
        var undone = await pipeline.UndoAsync(positional[0]);
        if (!undone.IsSuccess) return Fail(undone.Error!.Message);
        var record = undone.Value!;
        Console.WriteLine($"undo operation {record.OperationId}: {record.Status}");
        foreach (var item in record.Items)
            Console.WriteLine($"  {item.Operation,-10} {item.OriginalLocation} -> {item.ResultingLocation} [{item.Status}]");
        return record.Status == OperationStatus.Committed ? 0 : 2;
    }

    case "history":
    {
        var limit = positional.Count > 0 && int.TryParse(positional[0], out var n) ? n : 10;
        var recent = await pipeline.RecentOperationsAsync(limit);
        if (!recent.IsSuccess) return Fail(recent.Error!.Message);
        foreach (var r in recent.Value!)
            Console.WriteLine($"{r.OperationId}  {r.Status,-15} {r.CreatedAt:u}  plan={r.PlanId}  items={r.Items.Count}");
        return 0;
    }

    case "dupes":
    {
        var dupeArgs = positional.Where(p => !string.Equals(p, dest, StringComparison.OrdinalIgnoreCase)).ToList();
        var prefix = dupeArgs.Count > 0 ? Path.GetFullPath(dupeArgs[0]) : null;
        var report = await dupeService.FindAsync(prefix);
        if (!report.IsSuccess) return Fail(report.Error!.Message);
        var r = report.Value!;
        Console.WriteLine($"-- {r.Groups.Count} duplicate group(s), {r.Similar.Count} similar pair(s)");
        foreach (var g in r.Groups)
        {
            Console.WriteLine($"group {(g.Confirmed ? "exact" : "partial-only")}  {FmtBytes(g.SizeBytes)} x{g.Files.Count}  reclaimable={FmtBytes(g.ReclaimableBytes)}");
            foreach (var f in g.Files) Console.WriteLine($"  {f.Path}");
        }
        foreach (var s in r.Similar)
            Console.WriteLine($"similar {s.Score,5:0.0}  {s.PathA} <-> {s.PathB}\n    {string.Join("; ", s.Reasons)}");
        foreach (var w in r.Warnings) Console.WriteLine($"  warn: {w}");
        Console.WriteLine($"total reclaimable: {FmtBytes(r.TotalReclaimable)} (advisory — no files were moved or deleted)");

        if (!flags.Contains("--plan")) return 0;
        var reviewRoot = Path.Combine(
            dest ?? DefaultDest(prefix ?? Directory.GetCurrentDirectory()), "duplicates-review");
        var dupePlan = FileMind.Application.Duplicates.DuplicatePlanBuilder
            .BuildQuarantinePlan(r, reviewRoot, apply: flags.Contains("--apply"));
        if (dupePlan.Items.Count == 0)
        {
            Console.WriteLine("-- no confirmed duplicates to quarantine; nothing planned");
            return 0;
        }
        PrintPlan(dupePlan);
        var dupeCheck = await pipeline.DryRunAsync(dupePlan);
        if (!dupeCheck.IsSuccess) return Fail(dupeCheck.Error!.Message);
        foreach (var w in dupeCheck.Value!.Warnings) Console.WriteLine($"  warn: {w}");
        if (!dupeCheck.Value.Allowed)
        {
            foreach (var b in dupeCheck.Value.Blockers) Console.WriteLine($"  blocked: {b}");
            return Fail("Safety validation failed; nothing executed.");
        }
        if (!flags.Contains("--apply"))
        {
            Console.WriteLine("-- preview only. Re-run with --plan --apply to quarantine copies.");
            return 0;
        }
        var dupeResult = await pipeline.ExecuteAsync(dupePlan);
        if (!dupeResult.IsSuccess) return Fail(dupeResult.Error!.Message);
        var rec = dupeResult.Value!;
        Console.WriteLine($"-- executed {rec.Items.Count(i => i.Status == OperationStatus.Committed)}/{rec.Items.Count} item(s); op={rec.OperationId}");
        return 0;
    }

    case "storage":
    {
        var storageArgs = positional.Where(p => !string.Equals(p, dest, StringComparison.OrdinalIgnoreCase)).ToList();
        var prefix = storageArgs.Count > 0 ? Path.GetFullPath(storageArgs[0]) : null;
        var rep = await storageService.AnalyzeAsync(prefix);
        if (!rep.IsSuccess) return Fail(rep.Error!.Message);
        var s = rep.Value!;
        Console.WriteLine($"-- {s.FileCount} file(s), {FmtBytes(s.TotalBytes)} total; reclaimable dupes={FmtBytes(s.DuplicateReclaimableBytes)}");
        Console.WriteLine("by kind:");
        foreach (var b in s.ByKind)
            Console.WriteLine($"  {b.Label,-12} {FmtBytes(b.Bytes),10}  {b.Count} file(s)");
        Console.WriteLine("by age:");
        foreach (var b in s.ByAgeBand)
            Console.WriteLine($"  {b.Label,-12} {FmtBytes(b.Bytes),10}  {b.Count} file(s)");
        if (s.Largest.Count > 0)
        {
            Console.WriteLine("largest:");
            foreach (var f in s.Largest)
                Console.WriteLine($"  {FmtBytes(f.SizeBytes),10}  {f.Path}");
        }
        if (s.Oldest.Count > 0)
        {
            Console.WriteLine("oldest:");
            foreach (var f in s.Oldest)
                Console.WriteLine($"  {f.AgeDays,5}d  {f.Path}");
        }
        return 0;
    }

    case "search":
    {
        var searchArgs = positional.Where(p => !string.Equals(p, dest, StringComparison.OrdinalIgnoreCase)).ToList();
        var flagsWithValues = new[] { "--name", "--ext", "--kind", "--older-than", "--newer-than", "--limit" }
            .Select(flagValue).Where(v => v is not null).ToHashSet();
        var text = searchArgs.FirstOrDefault(a => !flagsWithValues.Contains(a));
        int? OptInt(string n) =>
            int.TryParse(flagValue(n), out var v) ? v : null;
        var q = new FileMind.Application.Search.SearchQuery(
            Text: text,
            NameContains: flagValue("--name"),
            Extension: flagValue("--ext"),
            Kind: flagValue("--kind"),
            OlderThanDays: OptInt("--older-than"),
            NewerThanDays: OptInt("--newer-than"),
            Limit: OptInt("--limit") ?? 50);
        var res = await searchUseCase.RunAsync(q);
        if (!res.IsSuccess) return Fail(res.Error!.Message);
        foreach (var i in res.Value!)
            Console.WriteLine($"{i.DisplayName,-40} {i.Kind,-10} {FmtBytes(i.SizeBytes),10}  rank={i.Rank,7:0.000}  {i.Path}");
        Console.WriteLine($"-- {res.Value!.Count} result(s)" +
            (res.Value.Count == 0 ? " — nothing indexed matches; run `scan`/`enrich` to populate the index" : ""));
        return 0;
    }

    case "ai-status":
    {
        var (key, keySource) = await aiClient.ResolveKeyAsync();
        var quotaStatus = await aiQuota.StatusAsync();
        Console.WriteLine($"enabled={aiOptions.Enabled}  model={aiOptions.Model}");
        Console.WriteLine($"endpoint={aiOptions.Endpoint}");
        Console.WriteLine($"api-key={(key is null ? "MISSING (set GROQ_API_KEY in .env or `filemind ai key set`)" : $"present ({keySource})")}");
        Console.WriteLine($"consent: metadata={aiOptions.SendMetadata} extracted-text={aiOptions.SendExtractedText}");
        Console.WriteLine($"quota: {quotaStatus.RequestsToday}/{quotaStatus.Policy.MaxRequestsPerDay} req/day, " +
            $"{quotaStatus.TokensToday}/{quotaStatus.Policy.MaxTokensPerDay} tok/day, " +
            $"{quotaStatus.RequestsLastMinute}/{quotaStatus.Policy.MaxRequestsPerMinute} req/min");
        if (quotaStatus.ProviderRemainingRequestsDay is { } pr)
            Console.WriteLine($"provider-reported: {pr} requests left today, {quotaStatus.ProviderRemainingTokensMinute} tokens left this minute");
        Console.WriteLine($"provider: available={aiProvider.IsAvailable}" +
            (aiClient.BreakerUntil is { } b ? $"  circuit-breaker until {b:HH:mm:ss}Z" : ""));
        if (!quotaStatus.Allowed) Console.WriteLine($"quota gate: {quotaStatus.DenyReason}");
        Console.WriteLine("-- advisory only; AI output never mutates files and never bypasses SafetyEngine");
        return 0;
    }

    case "ai-usage":
    {
        var limit = positional.Count > 0 && int.TryParse(positional[0], out var n) ? n : 10;
        var usage = await aiUsageLedger.GetRecentAsync(limit);
        if (!usage.IsSuccess) return Fail(usage.Error!.Message);
        foreach (var u in usage.Value!)
            Console.WriteLine($"{u.At:u}  {u.Purpose,-16} {u.Model,-22} in={u.PromptTokens,5} out={u.CompletionTokens,5}  {u.Outcome}");
        Console.WriteLine($"-- {usage.Value!.Count} record(s) — metadata only, payload contents are never stored");
        return 0;
    }

    case "ai-key":
    {
        if (positional.Count < 1) return Fail("usage: filemind ai-key <set <key>|clear|status>");
        switch (positional[0])
        {
            case "set":
                if (positional.Count < 2) return Fail("usage: filemind ai-key set <key>");
                var set = await dpapiSecrets.SetAsync(FileMind.Intelligence.AI.GroqOptions.SecretStoreKey, positional[1]);
                if (!set.IsSuccess) return Fail(set.Error!.Message);
                Console.WriteLine("Groq API key stored in the DPAPI user store. It never enters the repo or logs.");
                return 0;
            case "clear":
                await dpapiSecrets.DeleteAsync(FileMind.Intelligence.AI.GroqOptions.SecretStoreKey);
                Console.WriteLine("Groq API key removed from the DPAPI user store.");
                return 0;
            case "status":
                var (key, source) = await aiClient.ResolveKeyAsync();
                Console.WriteLine(key is null ? "no key configured" : $"key configured (source: {source})");
                return 0;
            default:
                return Fail($"unknown ai-key action '{positional[0]}' — set|clear|status");
        }
    }

    case "ai-suggest":
    {
        if (positional.Count < 1) return Fail("usage: filemind ai-suggest <file>");
        var target = Path.GetFullPath(positional[0]);
        var stat = await adapter.StatAsync(target);
        if (!stat.IsSuccess || stat.Value!.IsDirectory) return Fail($"'{target}' not found.");
        var candidate = new FileCandidate
        {
            ScanId = "ai-suggest", SourceUri = target,
            DisplayName = Path.GetFileName(target),
            Extension = Path.GetExtension(target).TrimStart('.'),
            SizeBytes = stat.Value.SizeBytes, CreatedAt = stat.Value.CreatedAt,
            ModifiedAt = stat.Value.ModifiedAt, SourceScope = SourceScope.Other,
        };
        var prepared = aiGate.Prepare(candidate);
        if (!prepared.IsSuccess) return Fail(prepared.Error!.Message);
        var sug = await aiProvider.SuggestAsync(prepared.Value!);
        if (!sug.IsSuccess) return Fail($"provider: {sug.Error!.Message}");
        var s = sug.Value!;
        Console.WriteLine($"collection={s.Collection ?? "-"} category={s.Category ?? "-"} subject={s.Subject ?? "-"} project={s.Project ?? "-"}");
        Console.WriteLine($"suggested-name={s.SuggestedName ?? "-"}  confidence={s.ConfidenceHint:0}");
        Console.WriteLine($"explanation: {s.Explanation}");
        Console.WriteLine("-- advisory: this suggestion changes nothing unless merged as evidence by the pipeline");
        return 0;
    }

    case "ai-rule":
    {
        var nl = string.Join(' ', positional);
        if (nl.Length == 0) return Fail("usage: filemind ai-rule <natural language>");
        var draft = await FileMind.Intelligence.AI.AiRuleDrafter.DraftAsync(aiProvider, nl);
        if (!draft.IsSuccess) return Fail($"provider: {draft.Error!.Message}");
        Console.WriteLine(draft.Value!.RootElement.GetRawText());
        Console.WriteLine("-- draft only: validate + confirm before any import; nothing was persisted");
        return 0;
    }

    case "ai-search":
    {
        var nl = string.Join(' ', positional);
        if (nl.Length == 0) return Fail("usage: filemind ai-search <natural language>");
        var interpreted = await FileMind.Intelligence.AI.AiQueryInterpreter.InterpretAsync(aiProvider, nl);
        var q = interpreted.IsSuccess && interpreted.Value is { } iq
            ? iq with { Text = iq.Text ?? nl }
            : new FileMind.Application.Search.SearchQuery(Text: nl);
        if (!interpreted.IsSuccess)
            Console.WriteLine($"-- interpretation unavailable ({interpreted.Error!.Code}); plain text search");
        else
            Console.WriteLine($"-- interpreted: text='{q.Text}' name='{q.NameContains}' ext='{q.Extension}' kind='{q.Kind}' older>{q.OlderThanDays} newer<{q.NewerThanDays}");
        var res = await searchUseCase.RunAsync(q);
        if (!res.IsSuccess) return Fail(res.Error!.Message);
        foreach (var i in res.Value!)
            Console.WriteLine($"{i.DisplayName,-40} {i.Kind,-10} {FmtBytes(i.SizeBytes),10}  {i.Path}");
        Console.WriteLine($"-- {res.Value!.Count} result(s)");
        return 0;
    }

    case "smart-inbox":
    {
        if (positional.Count > 0 && positional[0] == "resolve")
        {
            if (positional.Count < 2) return Fail("usage: filemind smart-inbox resolve <id>");
            var resolved = await store.ResolveAsync(positional[1]);
            return resolved.IsSuccess && resolved.Value ? 0
                : Fail($"could not resolve '{positional[1]}'");
        }
        var openItems = await store.GetOpenAsync();
        if (!openItems.IsSuccess) return Fail(openItems.Error!.Message);
        foreach (var item in openItems.Value!)
            Console.WriteLine($"{item.Id}  conf={item.Classification.Confidence,5:0}  {item.Path}\n    reason: {item.Reason}");
        Console.WriteLine($"-- {openItems.Value!.Count} open item(s)");
        return 0;
    }

    case "watch":
    {
        var sub = positional.Count > 0 ? positional[0] : "run";
        switch (sub)
        {
            case "add":
            {
                if (positional.Count < 2) return Fail("usage: filemind watch add <dir> [--mode <mode>] [--tree]");
                if (!Directory.Exists(positional[1])) return Fail($"directory '{positional[1]}' does not exist");
                var added = await ((IWatchedFolderRepository)store).AddAsync(positional[1]);
                if (!added.IsSuccess) return Fail(added.Error!.Message);
                var modeArg = flagValue("--mode");
                if (modeArg is not null)
                {
                    if (!ModePolicy.TryParse(modeArg, out var fm)) return Fail($"unknown mode '{modeArg}'.");
                    await ((ISettingsRepository)store).SetAsync($"watch.mode.{positional[1]}", fm.ToString());
                }
                if (flags.Contains("--tree"))
                    await ((ISettingsRepository)store).SetAsync($"watch.tree.{positional[1]}", "true");
                Console.WriteLine($"watching {positional[1]}");
                return 0;
            }
            case "tree":
            {
                if (positional.Count < 2) return Fail("usage: filemind watch tree <dir> [on|off]");
                var treeKey = $"watch.tree.{positional[1]}";
                if (positional.Count < 3)
                {
                    var cur = (await ((ISettingsRepository)store).GetAsync(treeKey)).Value;
                    Console.WriteLine($"tree={(cur == "true" ? "on" : "off")}  {positional[1]}");
                    return 0;
                }
                var onOff = positional[2].ToLowerInvariant();
                if (onOff is not ("on" or "off")) return Fail($"usage: filemind watch tree <dir> [on|off]");
                await ((ISettingsRepository)store).SetAsync(treeKey, onOff == "on" ? "true" : "false");
                Console.WriteLine($"tree={onOff}  {positional[1]}");
                return 0;
            }
            case "remove":
            {
                if (positional.Count < 2) return Fail("usage: filemind watch remove <dir>");
                var removed = await ((IWatchedFolderRepository)store).RemoveAsync(positional[1]);
                return removed.IsSuccess && removed.Value ? 0 : Fail($"not watched: '{positional[1]}'");
            }
            case "list":
            {
                var folders = (await ((IWatchedFolderRepository)store).GetAllAsync()).Value ?? [];
                foreach (var f in folders)
                {
                    var fm = (await ((ISettingsRepository)store).GetAsync($"watch.mode.{f}")).Value;
                    var tree = (await ((ISettingsRepository)store).GetAsync($"watch.tree.{f}")).Value == "true";
                    Console.WriteLine($"{f,-50} mode={fm ?? ModePolicy.Display(currentMode)}  tree={(tree ? "on" : "off")}");
                }
                Console.WriteLine($"-- {folders.Count} watched folder(s)");
                return 0;
            }
            default:
            {
                // run the watcher over persisted folders until --seconds elapses or Ctrl+C
                var folders = (await ((IWatchedFolderRepository)store).GetAllAsync()).Value ?? [];
                var extra = flagValue("--dir");
                if (extra is not null && Directory.Exists(extra)) folders = folders.Append(extra).ToList();
                if (folders.Count == 0) return Fail("no watched folders — add one: filemind watch add <dir>");
                var seconds = flagValue("--seconds") is { } s && int.TryParse(s, out var n) ? n : 0;

                using var src = new WindowsWatcherEventSource();
                foreach (var f in folders)
                {
                    var started = await src.StartWatchingAsync(f);
                    if (!started.IsSuccess) return Fail($"{f}: {started.Error!.Message}");
                }
                var notify = new ConsoleNotificationService();
                var stability = new FileStabilityChecker(adapter);
                var orchestrator = new WatchOrchestrator(
                    pipeline, indexing, inboxService, paths, stability, store,
                    notify, dest ?? DefaultDest(folders[0]));
                var sysClock = new SystemClock();
                var svc = new WatchService(src, new WatcherEventQueue(), new DebounceService(sysClock),
                    orchestrator, sysClock, jobs: new SqliteJobQueue((IJobRepository)store));
                var apply = flags.Contains("--apply");

                var folderModes = new Dictionary<string, OrganizationMode>(StringComparer.OrdinalIgnoreCase);
                var folderTree = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in folders)
                {
                    var saved = (await ((ISettingsRepository)store).GetAsync($"watch.mode.{f}")).Value;
                    if (saved is not null && ModePolicy.TryParse(saved, out var fm)) folderModes[f] = fm;
                    if ((await ((ISettingsRepository)store).GetAsync($"watch.tree.{f}")).Value == "true")
                        folderTree.Add(f);
                }
                (OrganizationMode, bool, string?, bool) Route(WatchEvent e)
                {
                    var owner = folders.FirstOrDefault(f =>
                        e.Path.StartsWith(f, StringComparison.OrdinalIgnoreCase));
                    var m = owner is not null && folderModes.TryGetValue(owner, out var fm)
                        ? fm : currentMode;
                    return (m, apply, owner, owner is not null && folderTree.Contains(owner));
                }

                Console.WriteLine($"watching {folders.Count} folder(s)" +
                    (seconds > 0 ? $" for {seconds}s" : " — Ctrl+C to stop"));
                using var cts = seconds > 0
                    ? new CancellationTokenSource(TimeSpan.FromSeconds(seconds))
                    : new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                // Removable drives: pause watchers on detached roots, resume on reattach.
                var paused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var drives = new WindowsRemovableStorageEvents();
                var drivePump = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var d in drives.EventsAsync(cts.Token))
                        {
                            if (d.Kind == "Removed")
                            {
                                foreach (var f in folders.Where(f =>
                                    f.StartsWith(d.DriveRoot, StringComparison.OrdinalIgnoreCase)))
                                {
                                    await src.StopWatchingAsync(f, cts.Token);
                                    paused.Add(f);
                                    await notify.NotifyAsync(new Notification(
                                        "Watcher paused", $"drive {d.DriveRoot} removed — watching paused for {f}"), cts.Token);
                                }
                            }
                            else if (d.Kind == "Attached")
                            {
                                foreach (var f in paused.Where(f =>
                                    f.StartsWith(d.DriveRoot, StringComparison.OrdinalIgnoreCase)).ToList())
                                {
                                    var restarted = await src.StartWatchingAsync(f, cts.Token);
                                    if (restarted.IsSuccess)
                                    {
                                        paused.Remove(f);
                                        await notify.NotifyAsync(new Notification(
                                            "Watcher resumed", $"drive {d.DriveRoot} reattached — watching {f}"), cts.Token);
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { }
                }, cts.Token);

                var run = await svc.RunAsync(currentMode, apply, Route, cts.Token);
                try { await drivePump; } catch (OperationCanceledException) { }
                if (!run.IsSuccess) return Fail(run.Error!.Message);
                foreach (var w in run.Value!.Warnings) Console.WriteLine($"  warn: {w}");
                Console.WriteLine($"-- {run.Value.EventsReceived} event(s), " +
                    $"{run.Value.EventsProcessed} processed, {run.Value.ActionsTaken} action(s)");
                return 0;
            }
        }
    }

    case "watch-status":
    {
        var folders = (await ((IWatchedFolderRepository)store).GetAllAsync()).Value ?? [];
        Console.WriteLine($"mode={ModePolicy.Display(currentMode)}  watched={folders.Count}");
        foreach (var f in folders)
        {
            var fm = (await ((ISettingsRepository)store).GetAsync($"watch.mode.{f}")).Value;
            Console.WriteLine($"  {f}  mode={fm ?? ModePolicy.Display(currentMode)}");
        }
        var pending = (await ((IJobRepository)store).GetByStatusAsync("pending")).Value?.Count ?? 0;
        var openInbox = (await store.GetOpenAsync()).Value?.Count ?? 0;
        Console.WriteLine($"pending jobs={pending}  smart-inbox open={openInbox}");
        return 0;
    }

    case "mode":
    {
        if (positional.Count == 0)
        {
            Console.WriteLine($"mode={ModePolicy.Display(currentMode)}");
            Console.WriteLine("modes: manual (preview until --apply), smart (organize confident, rest to Smart Inbox),");
            Console.WriteLine("       automatic (execute all safety-passed), ai-assisted (smart + AI, needs PLAN-009), local-only (smart, AI off)");
            return 0;
        }
        var requested = positional[0] == "set" && positional.Count > 1 ? positional[1] : positional[0];
        if (!ModePolicy.TryParse(requested, out var nextMode))
            return Fail($"unknown mode '{requested}'.");
        var setResult = await ((ISettingsRepository)store).SetAsync(ModePolicy.SettingsKey, nextMode.ToString());
        if (!setResult.IsSuccess) return Fail(setResult.Error!.Message);
        Console.WriteLine($"mode={ModePolicy.Display(nextMode)}");
        return 0;
    }

    case "rules":
    {
        return await RulesVerbAsync();
    }

    case "learn":
    {
        var sub = positional.Count > 0 ? positional[0] : "list";
        switch (sub)
        {
            case "record":
                if (positional.Count < 3)
                    return Fail("usage: filemind learn record <path> <chosen-destination>");
                var rec = await correctionService.RecordAsync(positional[1], positional[2]);
                return rec.IsSuccess ? 0 : Fail(rec.Error!.Message);
            case "suggest":
                var mined = await correctionService.MineSuggestionsAsync(dest);
                if (!mined.IsSuccess) return Fail(mined.Error!.Message);
                if (mined.Value!.Count == 0)
                {
                    Console.WriteLine("no learned suggestions yet (need 2+ agreeing corrections)");
                    return 0;
                }
                foreach (var r in mined.Value!)
                    Console.WriteLine($"[draft:{r.Provenance.ToString().ToLowerInvariant()}] {r.Id}\n    {r.Name}\n    disabled — review before enabling");
                Console.WriteLine($"-- {mined.Value!.Count} draft rule(s); none active");
                return 0;
            case "calibration":
                var adj = await ((FileMind.Application.Learning.IConfidenceCalibrationRepository)store).GetAllAsync();
                if (!adj.IsSuccess) return Fail(adj.Error!.Message);
                foreach (var (k, v) in adj.Value!.OrderBy(x => x.Key))
                    Console.WriteLine($"{k,-20} {v,5:+0;-0;0}");
                if (adj.Value!.Count == 0) Console.WriteLine("no calibration signals recorded");
                return 0;
            default:
                var all = await ((FileMind.Application.Learning.ICorrectionRepository)store).GetAllAsync();
                if (!all.IsSuccess) return Fail(all.Error!.Message);
                foreach (var c in all.Value!)
                    Console.WriteLine($"{c.Path}  ->  {c.ChosenDestination}   ({c.Source})");
                Console.WriteLine($"-- {all.Value!.Count} correction(s)");
                return 0;
        }
    }

    case "related":
    {
        var relatedSvc = new FileMind.Application.Learning.RelatedFileService(store);
        var chains = positional.Count > 0
            ? await relatedSvc.RelatedToAsync(positional[0])
            : await relatedSvc.FindVersionChainsAsync();
        if (!chains.IsSuccess) return Fail(chains.Error!.Message);
        if (chains.Value!.Count == 0)
        {
            Console.WriteLine("no version chains found");
            return 0;
        }
        foreach (var c in chains.Value!)
        {
            Console.WriteLine($"{c.BaseName}  ({c.Evidence})");
            foreach (var p in c.PathsOrderedOldestToNewest)
                Console.WriteLine($"    {(p == c.LatestPath ? "* " : "  ")}{p}");
        }
        Console.WriteLine("-- suggestion only; nothing moved");
        return 0;
    }

    case "profile":
    {
        var sub = positional.Count > 0 ? positional[0].ToLowerInvariant() : "";
        switch (sub)
        {
            case "export":
            {
                var ex = await profileSvc.ExportAsync();
                if (!ex.IsSuccess) return Fail(ex.Error!.Message);
                var outPath = flagValue("--out");
                if (outPath is null) { Console.WriteLine(ex.Value!); return 0; }
                await File.WriteAllTextAsync(outPath, ex.Value!);
                Console.WriteLine($"exported {outPath}");
                return 0;
            }
            case "import":
            {
                if (positional.Count < 2) return Fail("usage: filemind profile import <file> [--preview|--apply] [--policy merge|skip]");
                var file = positional[1];
                if (!File.Exists(file)) return Fail($"file '{file}' does not exist");
                var json = await File.ReadAllTextAsync(file);
                var policyName = flagValue("--policy") ?? "merge";
                if (policyName is not ("merge" or "skip"))
                    return Fail($"unknown policy '{policyName}' (merge|skip)");
                var policy = policyName == "skip"
                    ? FileMind.Application.Profiles.ProfileImportPolicy.Skip
                    : FileMind.Application.Profiles.ProfileImportPolicy.Merge;

                if (flags.Contains("--apply"))
                {
                    var backup = await profileSvc.BackupAsync(Path.GetDirectoryName(dbPath) is { Length: > 0 } bd ? bd : ".");
                    if (!backup.IsSuccess) return Fail($"backup failed: {backup.Error!.Message}");
                    Console.WriteLine($"backup {backup.Value!.Path}");
                    var res = await profileSvc.ImportAsync(json, policy);
                    if (!res.IsSuccess) return Fail(res.Error!.Message);
                    Console.WriteLine($"applied={res.Value!.Applied} skipped={res.Value.Skipped} policy={policyName}");
                    foreach (var w in res.Value.Warnings) Console.Error.WriteLine($"warn: {w}");
                    return 0;
                }
                // default & --preview: diff only, nothing written
                var prev = await profileSvc.PreviewAsync(json);
                if (!prev.IsSuccess) return Fail(prev.Error!.Message);
                foreach (var d in prev.Value!.Diff)
                    Console.WriteLine($"{d.Kind,-8} {d.Section,-12} {d.Id}");
                foreach (var w in prev.Value.Warnings) Console.Error.WriteLine($"warn: {w}");
                Console.WriteLine($"-- preview only (schema v{prev.Value.SchemaVersion}); re-run with --apply to write ({prev.Value.Diff.Count(d => d.Kind != "same")} change(s))");
                return 0;
            }
            case "backup":
            {
                var dir = flagValue("--dir") ?? (Path.GetDirectoryName(dbPath) is { Length: > 0 } bd ? bd : ".");
                var b = await profileSvc.BackupAsync(dir);
                if (!b.IsSuccess) return Fail(b.Error!.Message);
                Console.WriteLine($"backup {b.Value!.Path}");
                return 0;
            }
            case "restore":
            {
                if (positional.Count < 2) return Fail("usage: filemind profile restore <file>");
                var file = positional[1];
                if (!File.Exists(file)) return Fail($"file '{file}' does not exist");
                var res = await profileSvc.RestoreAsync(file);
                if (!res.IsSuccess) return Fail(res.Error!.Message);
                Console.WriteLine($"restored {file}: applied={res.Value!.Applied} skipped={res.Value.Skipped}");
                foreach (var w in res.Value.Warnings) Console.Error.WriteLine($"warn: {w}");
                return 0;
            }
            default:
                return Fail("usage: filemind profile [export|import|backup|restore]");
        }
    }

    default:
        return Fail($"Unknown command '{command}'. Run 'filemind --help'.");
}

async Task<IReadOnlyList<AnalyzedFile>> AnalyzeDirAsync()
{
    var root = RequiredDir(positional);
    var candidates = new List<FileCandidate>();
    await foreach (var c in pipeline.ScanAsync(root, recursive)) candidates.Add(c);
    var idx = await pipeline.IndexAsync(candidates);
    if (!idx.IsSuccess)
        Console.Error.WriteLine($"warn: indexing failed ({idx.Error!.Message}); continuing without index");
    var analyzed = await pipeline.AnalyzeAsync(candidates);
    if (!analyzed.IsSuccess) throw new InvalidOperationException(analyzed.Error!.Message);
    await inboxService.RecordReviewAsync(analyzed.Value!);
    return analyzed.Value!;
}

string RequiredDir(List<string> pos)
{
    if (pos.Count == 0) throw new InvalidOperationException("missing <dir> argument");
    if (!Directory.Exists(pos[0])) throw new InvalidOperationException($"directory '{pos[0]}' does not exist");
    return pos[0];
}

string DefaultDest(string root) => Path.Combine(root, "organized");

async Task<int> RulesVerbAsync()
{
    var sub = positional.Count > 0 ? positional[0] : "list";
    switch (sub)
    {
        case "list":
        {
            var all = await rulesRepo.GetAllAsync();
            if (!all.IsSuccess) return Fail(all.Error!.Message);
            foreach (var r in all.Value!.OrderByDescending(r => r.Priority))
                Console.WriteLine($"{r.Id,-16} {r.Name,-30} priority={r.Priority,4} enabled={r.Enabled} provenance={r.Provenance} conditions={r.ConditionCount}");
            Console.WriteLine($"-- {all.Value!.Count} rule(s)");
            return 0;
        }
        case "show":
        {
            if (positional.Count < 2) return Fail("usage: filemind rules show <id>");
            var rule = await rulesRepo.GetAsync(positional[1]);
            if (!rule.IsSuccess) return Fail(rule.Error!.Message);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rule.Value,
                new System.Text.Json.JsonSerializerOptions(FileMindDb.JsonOptions) { WriteIndented = true }));
            return 0;
        }
        case "export":
        {
            if (positional.Count < 2) return Fail("usage: filemind rules export <id> [--out <file>]");
            var rule = await rulesRepo.GetAsync(positional[1]);
            if (!rule.IsSuccess) return Fail(rule.Error!.Message);
            var json = System.Text.Json.JsonSerializer.Serialize(rule.Value,
                new System.Text.Json.JsonSerializerOptions(FileMindDb.JsonOptions) { WriteIndented = true });
            var outPath = flagValue("--out");
            if (outPath is null) Console.WriteLine(json);
            else { File.WriteAllText(outPath, json); Console.WriteLine($"exported {rule.Value!.Id} -> {outPath}"); }
            return 0;
        }
        case "import":
        {
            if (positional.Count < 2) return Fail("usage: filemind rules import <file>");
            if (!File.Exists(positional[1])) return Fail($"file '{positional[1]}' does not exist");
            Rule parsed;
            try
            {
                parsed = System.Text.Json.JsonSerializer.Deserialize<Rule>(
                    File.ReadAllText(positional[1]), FileMindDb.JsonOptions)
                    ?? throw new InvalidOperationException("empty document");
            }
            catch (Exception ex)
            {
                return Fail($"invalid rule JSON: {ex.Message}");
            }
            parsed = parsed with { Provenance = RuleProvenance.Imported, UpdatedAt = DateTimeOffset.UtcNow };
            var valid = RuleValidator.Validate(parsed);
            if (!valid.IsSuccess) return Fail(valid.Error!.Message);
            var upserted = await rulesRepo.UpsertAsync(valid.Value!);
            if (!upserted.IsSuccess) return Fail(upserted.Error!.Message);
            Console.WriteLine($"imported rule {parsed.Id} '{parsed.Name}' (schemaVersion={parsed.SchemaVersion})");
            return 0;
        }
        case "test":
        {
            if (positional.Count < 3) return Fail("usage: filemind rules test <id|rule.json> <dir>");
            Rule testRule;
            if (File.Exists(positional[1]))
            {
                try
                {
                    testRule = System.Text.Json.JsonSerializer.Deserialize<Rule>(
                        File.ReadAllText(positional[1]), FileMindDb.JsonOptions)!;
                }
                catch (Exception ex) { return Fail($"invalid rule JSON: {ex.Message}"); }
            }
            else
            {
                var loaded = await rulesRepo.GetAsync(positional[1]);
                if (!loaded.IsSuccess) return Fail(loaded.Error!.Message);
                testRule = loaded.Value!;
            }
            var dir = positional[2];
            if (!Directory.Exists(dir)) return Fail($"directory '{dir}' does not exist");
            var candidates = new List<FileCandidate>();
            await foreach (var c in pipeline.ScanAsync(dir, recursive)) candidates.Add(c);
            var analyzed = await pipeline.AnalyzeAsync(candidates);
            if (!analyzed.IsSuccess) return Fail(analyzed.Error!.Message);
            var engine = new RuleEngine();
            var matched = 0;
            foreach (var a in analyzed.Value!)
            {
                var eval = engine.Evaluate(a.Candidate, a.Classification, [testRule]);
                var hit = eval.MatchedRuleIds.Count > 0;
                if (hit) matched++;
                var action = hit
                    ? string.Join(", ", eval.ProposedActions.Select(x => x.Kind.ToString()))
                    : "-";
                Console.WriteLine($"{(hit ? "MATCH" : "SKIP "),-6} {a.Candidate.DisplayName,-40} {action}");
            }
            Console.WriteLine($"-- rule {testRule.Id}: {matched}/{analyzed.Value!.Count} candidate(s) matched (test only, nothing changed)");
            return 0;
        }
        default:
            return Fail($"usage: filemind rules [list|show|test|import|export]");
    }
}

void PrintPlan(ActionPlan plan)
{
    Console.WriteLine($"plan {plan.PlanId}  items={plan.Items.Count}  review={plan.RequiresReview}  dryRun={plan.DryRun}");
    foreach (var item in plan.Items)
        Console.WriteLine($"  {item.Operation,-12} {item.Source}{(item.Destination is null ? "" : $" -> {item.Destination}")}");
}

int Fail(string message)
{
    Console.Error.WriteLine($"error: {message}");
    return 1;
}

string FmtBytes(long b) => b switch
{
    >= 1L << 30 => $"{b / (double)(1L << 30):0.0} GiB",
    >= 1L << 20 => $"{b / (double)(1L << 20):0.0} MiB",
    >= 1L << 10 => $"{b / (double)(1L << 10):0.0} KiB",
    _ => $"{b} B",
};

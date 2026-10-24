using FileMind.Application.Decisions;
using FileMind.Application.Indexing;
using FileMind.Application.Ports;
using FileMind.Application.Recovery;
using FileMind.Application.Scanning;
using FileMind.Application.UseCases;
using FileMind.Domain.Classification;
using FileMind.Domain.Contracts;
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
          filemind plan <dir> [--dest <root>] [--shallow]
          filemind execute <dir> [--dest <root>] [--dry-run] [--apply] [--shallow]
          filemind undo <operationId>
          filemind history [count]
          filemind smart-inbox [resolve <id>]
          filemind mode [<manual|smart|automatic|ai-assisted|local-only>]
          filemind rules [list]
          filemind rules show <id>
          filemind rules test <id|rule.json> <dir>
          filemind rules import <file>
          filemind rules export <id> [--out <file>]
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
var indexing = new FileIndexingService(store, new WindowsHashService());
var inboxService = new SmartInboxService(store);
var recoveryService = new StartupRecoveryService(store);
var pipeline = new OrganizerPipeline(
    scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
    new ActionPlanner(), safety,
    new TransactionEngine(adapter, store, safety),
    new UndoService(adapter),
    adapter, paths, adapter.ProtectedPaths,
    rulesRepo, collectionsRepo, opsRepo, opStore, indexing);

var modeSetting = (await ((ISettingsRepository)store).GetAsync(ModePolicy.SettingsKey)).Value;
var currentMode = modeSetting is not null && ModePolicy.TryParse(modeSetting, out var savedMode)
    ? savedMode
    : OrganizationMode.Manual;
var modePolicy = new ModePolicy(currentMode);

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
        await collectionsRepo.UpsertAsync(new CollectionEntry("school", "School", "School/{Subject}", ["IT421"]));
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

    case "plan":
    {
        var analyzed = await AnalyzeDirAsync();
        var plan = pipeline.Plan(analyzed, dryRun: true, destinationRoot: dest ?? DefaultDest(positional[0]));
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
            destinationRoot: dest ?? DefaultDest(positional[0]));
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

using FileMind.Application.Decisions;
using FileMind.Application.Ports;
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
          filemind execute <dir> [--dest <root>] [--dry-run] [--shallow]
          filemind undo <operationId>
          filemind history [count]
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
var pipeline = new OrganizerPipeline(
    scanner, classifier, new RuleEngine(), new DecisionMaker(confidence),
    new ActionPlanner(), safety,
    new TransactionEngine(adapter, store, safety),
    new UndoService(adapter),
    adapter, paths, adapter.ProtectedPaths,
    rulesRepo, collectionsRepo, opsRepo, opStore);

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
        var count = 0;
        await foreach (var c in pipeline.ScanAsync(root, recursive))
        {
            Console.WriteLine($"{c.DisplayName,-40} {c.SizeBytes,10:N0}  {c.SourceScope}");
            count++;
        }
        Console.WriteLine($"-- {count} file(s)");
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
        var plan = pipeline.Plan(analyzed, dryRun: flags.Contains("--dry-run"),
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

    default:
        return Fail($"Unknown command '{command}'. Run 'filemind --help'.");
}

async Task<IReadOnlyList<AnalyzedFile>> AnalyzeDirAsync()
{
    var root = RequiredDir(positional);
    var candidates = new List<FileCandidate>();
    await foreach (var c in pipeline.ScanAsync(root, recursive)) candidates.Add(c);
    var analyzed = await pipeline.AnalyzeAsync(candidates);
    if (!analyzed.IsSuccess) throw new InvalidOperationException(analyzed.Error!.Message);
    return analyzed.Value!;
}

string RequiredDir(List<string> pos)
{
    if (pos.Count == 0) throw new InvalidOperationException("missing <dir> argument");
    if (!Directory.Exists(pos[0])) throw new InvalidOperationException($"directory '{pos[0]}' does not exist");
    return pos[0];
}

string DefaultDest(string root) => Path.Combine(root, "organized");

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

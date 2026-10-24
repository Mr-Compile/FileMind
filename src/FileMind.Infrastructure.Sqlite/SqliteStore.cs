using System.Globalization;
using System.Text.Json;
using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Infrastructure.Sqlite;

public sealed class SqliteStore(FileMindDb db) :
    IRuleRepository, ICollectionRepository, ISubjectRepository, IProjectRepository,
    IOperationRepository, IOperationStore, ISmartInboxRepository, IExclusionRepository,
    IProtectedPathRepository, IWatchedFolderRepository, ISettingsRepository,
    IJobRepository, IAiAuditRepository, IProfileRepository, IFileIndexRepository
{
    private static JsonSerializerOptions Json => FileMindDb.JsonOptions;

    private static string Iso(DateTimeOffset t) => t.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseIso(string s) =>
        DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static string Ser<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T De<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;
    private static List<string> DeList(string json) => De<List<string>>(json);

    private static Result<T> Try<T>(Func<Result<T>> body)
    {
        try { return body(); }
        catch (Exception ex) { return Result<T>.Fail(FileMindError.Persistence(ex.Message)); }
    }

    // ---- IRuleRepository ----

    public Task<Result<IReadOnlyList<Rule>>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<Rule>>(() => Result<IReadOnlyList<Rule>>.Ok(
            db.Query("SELECT * FROM rules", MapRule).ToList())));

    public Task<Result<Rule>> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            var rows = db.Query("SELECT * FROM rules WHERE id=$id", MapRule, ("$id", id));
            return rows.Count == 0
                ? Result<Rule>.Fail(FileMindError.NotFound($"Rule '{id}' not found."))
                : Result<Rule>.Ok(rows[0]);
        }));

    public Task<Result<Rule>> UpsertAsync(Rule rule, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO rules(id,name,enabled,priority,conditions_json,actions_json,stop_processing,mode,created_at,updated_at,schema_version,provenance)
                VALUES($id,$name,$enabled,$priority,$cond,$act,$stop,$mode,$created,$updated,$ver,$prov)
                ON CONFLICT(id) DO UPDATE SET name=$name,enabled=$enabled,priority=$priority,conditions_json=$cond,
                    actions_json=$act,stop_processing=$stop,mode=$mode,updated_at=$updated,schema_version=$ver,provenance=$prov
                """,
                ("$id", rule.Id), ("$name", rule.Name), ("$enabled", rule.Enabled ? 1 : 0),
                ("$priority", rule.Priority), ("$cond", Ser(rule.ConditionRoot)),
                ("$act", Ser(rule.Actions)), ("$stop", rule.StopProcessing ? 1 : 0),
                ("$mode", rule.Mode), ("$created", Iso(rule.CreatedAt)),
                ("$updated", Iso(rule.UpdatedAt)), ("$ver", rule.SchemaVersion),
                ("$prov", rule.Provenance.ToString()));
            return Result<Rule>.Ok(rule);
        }));

    public Task<Result<bool>> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Try(() => Result<bool>.Ok(db.Exec("DELETE FROM rules WHERE id=$id", ("$id", id)) > 0)));

    private static Rule MapRule(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        Id = r.GetString(0), Name = r.GetString(1), Enabled = r.GetInt32(2) != 0,
        Priority = r.GetInt32(3), ConditionRoot = De<ConditionGroup>(r.GetString(4)),
        Actions = De<List<RuleAction>>(r.GetString(5)), StopProcessing = r.GetInt32(6) != 0,
        Mode = r.GetString(7), CreatedAt = ParseIso(r.GetString(8)), UpdatedAt = ParseIso(r.GetString(9)),
        SchemaVersion = r.GetInt32(10),
        Provenance = Enum.Parse<RuleProvenance>(r.GetString(11))
    };

    // ---- ICollectionRepository ----

    Task<Result<IReadOnlyList<CollectionEntry>>> ICollectionRepository.GetAllAsync(CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<CollectionEntry>>(() => Result<IReadOnlyList<CollectionEntry>>.Ok(
            db.Query("SELECT id,name,destination_template,categories_json FROM collections",
                r => new CollectionEntry(r.GetString(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2), DeList(r.GetString(3)))).ToList())));

    public Task<Result<CollectionEntry>> UpsertAsync(CollectionEntry entry, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO collections(id,name,destination_template,categories_json) VALUES($id,$name,$tpl,$cats)
                ON CONFLICT(id) DO UPDATE SET name=$name,destination_template=$tpl,categories_json=$cats
                """,
                ("$id", entry.Id), ("$name", entry.Name),
                ("$tpl", entry.DestinationTemplate), ("$cats", Ser(entry.Categories)));
            return Result<CollectionEntry>.Ok(entry);
        }));

    // ---- ISubjectRepository ----

    Task<Result<IReadOnlyList<SubjectLike>>> ISubjectRepository.GetAllAsync(CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<SubjectLike>>(() => Result<IReadOnlyList<SubjectLike>>.Ok(
            db.Query("SELECT id,name,code,aliases_json,keywords_json FROM subjects",
                r => new SubjectLike(r.GetString(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2), DeList(r.GetString(3)), DeList(r.GetString(4)))).ToList())));

    public Task<Result<SubjectLike>> UpsertAsync(SubjectLike entry, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO subjects(id,name,code,aliases_json,keywords_json) VALUES($id,$name,$code,$al,$kw)
                ON CONFLICT(id) DO UPDATE SET name=$name,code=$code,aliases_json=$al,keywords_json=$kw
                """,
                ("$id", entry.Id), ("$name", entry.Name), ("$code", entry.Code),
                ("$al", Ser(entry.Aliases)), ("$kw", Ser(entry.Keywords)));
            return Result<SubjectLike>.Ok(entry);
        }));

    // ---- IProjectRepository ----

    Task<Result<IReadOnlyList<ProjectLike>>> IProjectRepository.GetAllAsync(CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<ProjectLike>>(() => Result<IReadOnlyList<ProjectLike>>.Ok(
            db.Query("SELECT id,name,client_name,aliases_json,keywords_json FROM projects",
                r => new ProjectLike(r.GetString(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2), DeList(r.GetString(3)), DeList(r.GetString(4)))).ToList())));

    public Task<Result<ProjectLike>> UpsertAsync(ProjectLike entry, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO projects(id,name,client_name,aliases_json,keywords_json) VALUES($id,$name,$client,$al,$kw)
                ON CONFLICT(id) DO UPDATE SET name=$name,client_name=$client,aliases_json=$al,keywords_json=$kw
                """,
                ("$id", entry.Id), ("$name", entry.Name), ("$client", entry.ClientName),
                ("$al", Ser(entry.Aliases)), ("$kw", Ser(entry.Keywords)));
            return Result<ProjectLike>.Ok(entry);
        }));

    // ---- IOperationRepository + IOperationStore ----

    public Task<Result<OperationRecord>> SaveAsync(OperationRecord record, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO operations(operation_id,plan_id,status,items_json,created_at,committed_at,undo_of,error_json)
                VALUES($oid,$pid,$status,$items,$created,$committed,$undo,$err)
                ON CONFLICT(operation_id) DO UPDATE SET status=$status,items_json=$items,
                    committed_at=$committed,undo_of=$undo,error_json=$err
                """,
                ("$oid", record.OperationId), ("$pid", record.PlanId),
                ("$status", record.Status.ToString()), ("$items", Ser(record.Items)),
                ("$created", Iso(record.CreatedAt)),
                ("$committed", record.CommittedAt is { } c ? Iso(c) : null),
                ("$undo", record.UndoOfOperationId),
                ("$err", record.Error is { } e ? Ser(e) : null));
            return Result<OperationRecord>.Ok(record);
        }));

    Task<Result<OperationRecord>> IOperationRepository.GetAsync(string operationId, CancellationToken ct) =>
        Task.FromResult(Try(() =>
        {
            var rows = db.Query("SELECT * FROM operations WHERE operation_id=$id", MapOperation, ("$id", operationId));
            return rows.Count == 0
                ? Result<OperationRecord>.Fail(FileMindError.NotFound($"Operation '{operationId}' not found."))
                : Result<OperationRecord>.Ok(rows[0]);
        }));

    public Task<Result<IReadOnlyList<OperationRecord>>> GetIncompleteAsync(CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<OperationRecord>>(() => Result<IReadOnlyList<OperationRecord>>.Ok(
            db.Query("SELECT * FROM operations WHERE status NOT IN ('Committed','Failed','RolledBack')",
                MapOperation).ToList())));

    public Task<Result<IReadOnlyList<OperationRecord>>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<OperationRecord>>(() => Result<IReadOnlyList<OperationRecord>>.Ok(
            db.Query("SELECT * FROM operations ORDER BY created_at DESC LIMIT $n",
                MapOperation, ("$n", limit)).ToList())));

    Task<Result<OperationRecord>> IOperationStore.LoadAsync(string operationId, CancellationToken ct) =>
        ((IOperationRepository)this).GetAsync(operationId, ct);

    Task<Result<OperationRecord>> IOperationStore.SaveAsync(OperationRecord record, CancellationToken ct) =>
        SaveAsync(record, ct);

    Task<Result<IReadOnlyList<OperationRecord>>> IOperationStore.LoadIncompleteAsync(CancellationToken ct) =>
        GetIncompleteAsync(ct);

    private static OperationRecord MapOperation(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        OperationId = r.GetString(0), PlanId = r.GetString(1),
        Status = Enum.Parse<OperationStatus>(r.GetString(2)),
        Items = De<List<OperationItemState>>(r.GetString(3)),
        CreatedAt = ParseIso(r.GetString(4)),
        CommittedAt = r.IsDBNull(5) ? null : ParseIso(r.GetString(5)),
        UndoOfOperationId = r.IsDBNull(6) ? null : r.GetString(6),
        Error = r.IsDBNull(7) ? null : De<FileMindError>(r.GetString(7))
    };

    // ---- IFileIndexRepository ----

    public Task<Result<bool>> UpsertAsync(FileCandidate candidate, FileIdentity identity, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO file_index(path,stable_id,fingerprint,scan_id,payload_json,scanned_at,content_hash,hash_state)
                VALUES($p,$sid,$fp,$scan,$payload,$at,$ch,$hs)
                ON CONFLICT(path) DO UPDATE SET stable_id=$sid,fingerprint=$fp,scan_id=$scan,payload_json=$payload,scanned_at=$at,content_hash=$ch,hash_state=$hs
                """,
                ("$p", candidate.SourceUri), ("$sid", identity.StableId),
                ("$fp", identity.Fingerprint), ("$scan", candidate.ScanId),
                ("$payload", Ser(candidate)), ("$at", Iso(DateTimeOffset.UtcNow)),
                ("$ch", candidate.ContentHash), ("$hs", candidate.HashState.ToString()));
            return Result<bool>.Ok(true);
        }));

    public Task<Result<IReadOnlyList<FileIdentity>>> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<FileIdentity>>(() => Result<IReadOnlyList<FileIdentity>>.Ok(
            db.Query("SELECT stable_id,path,fingerprint FROM file_index WHERE fingerprint=$fp",
                r => new FileIdentity
                {
                    StableId = r.GetString(0), PathHint = r.GetString(1), Fingerprint = r.GetString(2)
                }, ("$fp", fingerprint)).ToList())));

    public Task<Result<bool>> RemoveAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("DELETE FROM file_index WHERE path=$p", ("$p", path)) > 0)));

    // ---- ISmartInboxRepository ----

    public Task<Result<SmartInboxItem>> AddAsync(SmartInboxItem item, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO smart_inbox(id,path,reason,classification_json,at,resolved)
                VALUES($id,$path,$reason,$cls,$at,$resolved)
                ON CONFLICT(id) DO UPDATE SET resolved=$resolved
                """,
                ("$id", item.Id), ("$path", item.Path), ("$reason", item.Reason),
                ("$cls", Ser(item.Classification)), ("$at", Iso(item.At)),
                ("$resolved", item.Resolved ? 1 : 0));
            return Result<SmartInboxItem>.Ok(item);
        }));

    public Task<Result<IReadOnlyList<SmartInboxItem>>> GetOpenAsync(CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<SmartInboxItem>>(() => Result<IReadOnlyList<SmartInboxItem>>.Ok(
            db.Query("SELECT id,path,reason,classification_json,at,resolved FROM smart_inbox WHERE resolved=0",
                r => new SmartInboxItem(r.GetString(0), r.GetString(1), r.GetString(2),
                    De<ClassificationResult>(r.GetString(3)), ParseIso(r.GetString(4)),
                    r.GetInt32(5) != 0)).ToList())));

    public Task<Result<bool>> ResolveAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("UPDATE smart_inbox SET resolved=1 WHERE id=$id", ("$id", id)) > 0)));

    // ---- IExclusionRepository ----

    private List<string>? _exclusionCache;

    Task<Result<IReadOnlyList<string>>> IExclusionRepository.GetPatternsAsync(CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<string>>(() =>
        {
            _exclusionCache = db.Query("SELECT pattern FROM exclusions", r => r.GetString(0));
            return Result<IReadOnlyList<string>>.Ok(_exclusionCache);
        }));

    bool IExclusionRepository.Matches(string path) =>
        (_exclusionCache ??= db.Query("SELECT pattern FROM exclusions", r => r.GetString(0)))
            .Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));

    // ---- IProtectedPathRepository ----

    public Task<Result<IReadOnlyList<string>>> GetUserPathsAsync(CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<string>>(() => Result<IReadOnlyList<string>>.Ok(
            db.Query("SELECT path FROM protected_paths", r => r.GetString(0)).ToList())));

    public Task<Result<bool>> AddAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("INSERT OR IGNORE INTO protected_paths(path) VALUES($p)", ("$p", path)) > 0)));

    // ---- IWatchedFolderRepository ----

    Task<Result<IReadOnlyList<string>>> IWatchedFolderRepository.GetAllAsync(CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<string>>(() => Result<IReadOnlyList<string>>.Ok(
            db.Query("SELECT path FROM watched_folders", r => r.GetString(0)).ToList())));

    Task<Result<bool>> IWatchedFolderRepository.AddAsync(string path, CancellationToken ct) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("INSERT OR IGNORE INTO watched_folders(path) VALUES($p)", ("$p", path)) > 0)));

    Task<Result<bool>> IWatchedFolderRepository.RemoveAsync(string path, CancellationToken ct) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("DELETE FROM watched_folders WHERE path=$p", ("$p", path)) > 0)));

    // ---- ISettingsRepository ----

    Task<Result<string?>> ISettingsRepository.GetAsync(string key, CancellationToken ct) =>
        Task.FromResult(Try<string?>(() => Result<string?>.Ok(
            db.Scalar<string>("SELECT value FROM settings WHERE key=$k", ("$k", key)))));

    public Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("""
                INSERT INTO settings(key,value) VALUES($k,$v)
                ON CONFLICT(key) DO UPDATE SET value=$v
                """, ("$k", key), ("$v", value));
            return Result<bool>.Ok(true);
        }));

    // ---- IJobRepository ----

    public Task<Result<Job>> EnqueueAsync(JobRequest request, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            var job = new Job(Guid.NewGuid().ToString("N"), request, "pending", DateTimeOffset.UtcNow);
            db.Exec("INSERT INTO jobs(id,type,payload,status,queued_at) VALUES($id,$t,$p,$s,$at)",
                ("$id", job.Id), ("$t", request.JobType), ("$p", request.Payload),
                ("$s", job.Status), ("$at", Iso(job.QueuedAt)));
            return Result<Job>.Ok(job);
        }));

    public Task<Result<IReadOnlyList<Job>>> GetPendingAsync(CancellationToken ct = default) =>
        Task.FromResult(Try<IReadOnlyList<Job>>(() => Result<IReadOnlyList<Job>>.Ok(
            db.Query("SELECT id,type,payload,status,queued_at FROM jobs WHERE status='pending' ORDER BY queued_at",
                r => new Job(r.GetString(0), new JobRequest(r.GetString(1), r.GetString(2)),
                    r.GetString(3), ParseIso(r.GetString(4)))).ToList())));

    public Task<Result<bool>> UpdateStatusAsync(string jobId, string status, CancellationToken ct = default) =>
        Task.FromResult(Try(() => Result<bool>.Ok(
            db.Exec("UPDATE jobs SET status=$s WHERE id=$id", ("$s", status), ("$id", jobId)) > 0)));

    // ---- IAiAuditRepository ----

    public Task<Result<bool>> RecordAsync(string requestHash, string outcome, DateTimeOffset at, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            db.Exec("INSERT INTO ai_audit(request_hash,outcome,at) VALUES($h,$o,$at)",
                ("$h", requestHash), ("$o", outcome), ("$at", Iso(at)));
            return Result<bool>.Ok(true);
        }));

    Task<Result<IReadOnlyList<string>>> IAiAuditRepository.GetRecentAsync(int limit, CancellationToken ct) =>
        Task.FromResult(Try<IReadOnlyList<string>>(() => Result<IReadOnlyList<string>>.Ok(
            db.Query("SELECT request_hash || ' | ' || outcome || ' | ' || at FROM ai_audit ORDER BY at DESC LIMIT $n",
                r => r.GetString(0), ("$n", limit)).ToList())));

    // ---- IProfileRepository ----

    public Task<Result<string>> ExportJsonAsync(CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            var profile = new
            {
                schemaVersion = 1,
                subjects = db.Query("SELECT id,name,code,aliases_json,keywords_json FROM subjects",
                    r => new { id = r.GetString(0), name = r.GetString(1),
                        code = r.IsDBNull(2) ? null : r.GetString(2),
                        aliases = DeList(r.GetString(3)), keywords = DeList(r.GetString(4)) }),
                projects = db.Query("SELECT id,name,client_name,aliases_json,keywords_json FROM projects",
                    r => new { id = r.GetString(0), name = r.GetString(1),
                        clientName = r.IsDBNull(2) ? null : r.GetString(2),
                        aliases = DeList(r.GetString(3)), keywords = DeList(r.GetString(4)) }),
                collections = db.Query("SELECT id,name,destination_template,categories_json FROM collections",
                    r => new { id = r.GetString(0), name = r.GetString(1),
                        destinationTemplate = r.IsDBNull(2) ? null : r.GetString(2),
                        categories = DeList(r.GetString(3)) }),
                rules = db.Query("SELECT * FROM rules", MapRule),
                exclusions = db.Query("SELECT pattern FROM exclusions", r => r.GetString(0)),
                settings = db.Query("SELECT key,value FROM settings", r => (r.GetString(0), r.GetString(1)))
                    .ToDictionary(kv => kv.Item1, kv => kv.Item2)
            };
            return Result<string>.Ok(JsonSerializer.Serialize(profile, Json));
        }));

    public Task<Result<bool>> ImportJsonAsync(string json, CancellationToken ct = default) =>
        Task.FromResult(Try(() =>
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("subjects", out var subjects))
                foreach (var s in subjects.EnumerateArray())
                    db.Exec("""
                        INSERT INTO subjects(id,name,code,aliases_json,keywords_json) VALUES($id,$name,$code,$al,$kw)
                        ON CONFLICT(id) DO UPDATE SET name=$name,code=$code,aliases_json=$al,keywords_json=$kw
                        """,
                        ("$id", s.GetProperty("id").GetString()), ("$name", s.GetProperty("name").GetString()),
                        ("$code", s.TryGetProperty("code", out var c) ? c.GetString() : null),
                        ("$al", s.GetProperty("aliases").GetRawText()), ("$kw", s.GetProperty("keywords").GetRawText()));

            if (root.TryGetProperty("projects", out var projects))
                foreach (var p in projects.EnumerateArray())
                    db.Exec("""
                        INSERT INTO projects(id,name,client_name,aliases_json,keywords_json) VALUES($id,$name,$client,$al,$kw)
                        ON CONFLICT(id) DO UPDATE SET name=$name,client_name=$client,aliases_json=$al,keywords_json=$kw
                        """,
                        ("$id", p.GetProperty("id").GetString()), ("$name", p.GetProperty("name").GetString()),
                        ("$client", p.TryGetProperty("clientName", out var c) ? c.GetString() : null),
                        ("$al", p.GetProperty("aliases").GetRawText()), ("$kw", p.GetProperty("keywords").GetRawText()));

            if (root.TryGetProperty("collections", out var collections))
                foreach (var c in collections.EnumerateArray())
                    db.Exec("""
                        INSERT INTO collections(id,name,destination_template,categories_json) VALUES($id,$name,$tpl,$cats)
                        ON CONFLICT(id) DO UPDATE SET name=$name,destination_template=$tpl,categories_json=$cats
                        """,
                        ("$id", c.GetProperty("id").GetString()), ("$name", c.GetProperty("name").GetString()),
                        ("$tpl", c.TryGetProperty("destinationTemplate", out var t) ? t.GetString() : null),
                        ("$cats", c.GetProperty("categories").GetRawText()));

            return Result<bool>.Ok(true);
        }));
}

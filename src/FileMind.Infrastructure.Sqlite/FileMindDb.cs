using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace FileMind.Infrastructure.Sqlite;

public sealed class FileMindDb : IDisposable
{
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions JsonOptions => Json;

    private readonly SqliteConnection _connection;

    private FileMindDb(SqliteConnection connection) => _connection = connection;

    public static FileMindDb Open(string path)
    {
        if (path != ":memory:")
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        var db = new FileMindDb(connection);
        // WAL + NORMAL: batch writes (indexing, job churn) otherwise pay one
        // fsync per statement. WAL keeps durability across process crashes.
        if (path != ":memory:")
        {
            db.Exec("PRAGMA journal_mode=WAL");
            db.Exec("PRAGMA synchronous=NORMAL");
        }
        db.Migrate();
        return db;
    }

    public SqliteCommand Command(string sql)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    public int Exec(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        var results = new List<T>();
        while (reader.Read()) results.Add(map(reader));
        return results;
    }

    public T? Scalar<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = cmd.ExecuteScalar();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    private void Migrate()
    {
        Exec("CREATE TABLE IF NOT EXISTS schema_migrations(version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL)");
        var applied = Scalar<long>("SELECT COALESCE(MAX(version),0) FROM schema_migrations");

        if (applied < 1)
        {
            foreach (var statement in V1.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Exec(statement);
            Exec("INSERT INTO schema_migrations(version, applied_at) VALUES(1, $at)",
                ("$at", DateTimeOffset.UtcNow.ToString("O")));
        }

        if (applied < 2)
        {
            foreach (var statement in V2.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Exec(statement);
            Exec("INSERT INTO schema_migrations(version, applied_at) VALUES(2, $at)",
                ("$at", DateTimeOffset.UtcNow.ToString("O")));
        }

        if (applied < 3)
        {
            foreach (var statement in V3.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Exec(statement);
            Exec("INSERT INTO schema_migrations(version, applied_at) VALUES(3, $at)",
                ("$at", DateTimeOffset.UtcNow.ToString("O")));
        }

        if (applied < 4)
        {
            foreach (var statement in V4.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Exec(statement);
            Exec("INSERT INTO schema_migrations(version, applied_at) VALUES(4, $at)",
                ("$at", DateTimeOffset.UtcNow.ToString("O")));
        }

        if (applied < 5)
        {
            foreach (var statement in V5.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Exec(statement);
            Exec("INSERT INTO schema_migrations(version, applied_at) VALUES(5, $at)",
                ("$at", DateTimeOffset.UtcNow.ToString("O")));
        }
    }

    private const string V1 = """
        CREATE TABLE rules(
            id TEXT PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL, priority INTEGER NOT NULL,
            conditions_json TEXT NOT NULL, actions_json TEXT NOT NULL, stop_processing INTEGER NOT NULL,
            mode TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL,
            schema_version INTEGER NOT NULL, provenance TEXT NOT NULL);

        CREATE TABLE subjects(
            id TEXT PRIMARY KEY, name TEXT NOT NULL, code TEXT,
            aliases_json TEXT NOT NULL, keywords_json TEXT NOT NULL);

        CREATE TABLE projects(
            id TEXT PRIMARY KEY, name TEXT NOT NULL, client_name TEXT,
            aliases_json TEXT NOT NULL, keywords_json TEXT NOT NULL);

        CREATE TABLE collections(
            id TEXT PRIMARY KEY, name TEXT NOT NULL, destination_template TEXT,
            categories_json TEXT NOT NULL);

        CREATE TABLE exclusions(pattern TEXT PRIMARY KEY);
        CREATE TABLE protected_paths(path TEXT PRIMARY KEY);
        CREATE TABLE watched_folders(path TEXT PRIMARY KEY);

        CREATE TABLE file_index(
            path TEXT PRIMARY KEY, stable_id TEXT NOT NULL, fingerprint TEXT NOT NULL,
            scan_id TEXT NOT NULL, payload_json TEXT NOT NULL, scanned_at TEXT NOT NULL);
        CREATE INDEX idx_file_index_fingerprint ON file_index(fingerprint);

        CREATE TABLE operations(
            operation_id TEXT PRIMARY KEY, plan_id TEXT NOT NULL, status TEXT NOT NULL,
            items_json TEXT NOT NULL, created_at TEXT NOT NULL, committed_at TEXT,
            undo_of TEXT, error_json TEXT);

        CREATE TABLE smart_inbox(
            id TEXT PRIMARY KEY, path TEXT NOT NULL, reason TEXT NOT NULL,
            classification_json TEXT NOT NULL, at TEXT NOT NULL, resolved INTEGER NOT NULL);

        CREATE TABLE settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);

        CREATE TABLE jobs(
            id TEXT PRIMARY KEY, type TEXT NOT NULL, payload TEXT NOT NULL,
            status TEXT NOT NULL, queued_at TEXT NOT NULL);

        CREATE TABLE ai_audit(
            request_hash TEXT NOT NULL, outcome TEXT NOT NULL, at TEXT NOT NULL)
        """;

    private const string V2 = """
        ALTER TABLE file_index ADD COLUMN content_hash TEXT;
        ALTER TABLE file_index ADD COLUMN hash_state TEXT NOT NULL DEFAULT 'NotComputed'
        """;

    // FTS5 external-free content table: every indexed field is stored here so
    // search never needs to touch payloads. `content` holds bounded extracted
    // text only (local, never leaves the DB).
    private const string V3 = """
        CREATE VIRTUAL TABLE file_content_index USING fts5(
            path UNINDEXED,
            display_name,
            collection,
            subject,
            project,
            kind,
            content,
            tokenize='unicode61')
        """;

    // Usage ledger for the Groq budget (PLAN-020): token counts + purpose only,
    // never payload contents.
    private const string V4 = """
        CREATE TABLE ai_usage(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            at TEXT NOT NULL,
            request_hash TEXT NOT NULL DEFAULT '',
            model TEXT NOT NULL,
            purpose TEXT NOT NULL,
            prompt_tokens INTEGER NOT NULL,
            completion_tokens INTEGER NOT NULL,
            outcome TEXT NOT NULL);
        CREATE INDEX idx_ai_usage_at ON ai_usage(at)
        """;

    // PLAN-013 M1/M2: user corrections (undo/manual overrides) feed draft
    // suggestions; per-key bounded confidence calibration. Local-only.
    private const string V5 = """
        CREATE TABLE corrections(
            id TEXT PRIMARY KEY,
            path TEXT NOT NULL,
            original_destination TEXT,
            chosen_destination TEXT NOT NULL,
            source TEXT NOT NULL,
            at TEXT NOT NULL);
        CREATE INDEX idx_corrections_path ON corrections(path);

        CREATE TABLE confidence_calibration(
            key TEXT PRIMARY KEY,
            adjustment REAL NOT NULL,
            updated_at TEXT NOT NULL)
        """;

    public void Dispose() => _connection.Dispose();
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using FileMind.Application.Ports;
using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;
using FileMind.Domain.Ports;

namespace FileMind.Testing;

public sealed class InMemoryOperationStore : IOperationStore
{
    private readonly ConcurrentDictionary<string, OperationRecord> _records = new();

    public Task<Result<OperationRecord>> LoadAsync(string operationId, CancellationToken ct = default) =>
        Task.FromResult(_records.TryGetValue(operationId, out var r)
            ? Result<OperationRecord>.Ok(r)
            : Result<OperationRecord>.Fail(FileMindError.NotFound($"Operation '{operationId}' not found.")));

    public Task<Result<OperationRecord>> SaveAsync(OperationRecord record, CancellationToken ct = default)
    {
        _records[record.OperationId] = record;
        return Task.FromResult(Result<OperationRecord>.Ok(record));
    }

    public Task<Result<IReadOnlyList<OperationRecord>>> LoadIncompleteAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<OperationRecord>>.Ok(
            _records.Values.Where(r => !r.IsTerminal).ToList()));
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-01-15T10:00:00Z");
}

public sealed class StaticProtectedPaths : IProtectedPathProvider
{
    private readonly List<string> _roots;
    public StaticProtectedPaths(params string[] roots) => _roots = roots.ToList();
    public IReadOnlyList<string> ProtectedRoots => _roots;
    public bool IsProtected(string path) =>
        _roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));
}

public sealed class WindowsStylePathNormalizer : IPathNormalizer
{
    public string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');
    public string Combine(params string[] parts) =>
        string.Join('\\', parts.Select(p => p.Replace('/', '\\').Trim('\\')).Where(p => p.Length > 0));
    public bool IsUnder(string path, string root) =>
        Normalize(path).StartsWith(Normalize(root) + "\\", StringComparison.OrdinalIgnoreCase);
    public string FileName(string path) => path.Split('/', '\\').Last();
    public string? DirectoryOf(string path)
    {
        var i = path.LastIndexOfAny(['/', '\\']);
        return i > 0 ? path[..i] : null;
    }
}

public sealed class StubAiProvider : IAIProvider
{
    public bool IsAvailable { get; set; } = true;
    public AiSuggestion? Next { get; set; }

    public Task<Result<AiSuggestion>> SuggestAsync(AiRequestContext context, CancellationToken ct = default) =>
        Task.FromResult(Next is not null
            ? Result<AiSuggestion>.Ok(Next)
            : Result<AiSuggestion>.Fail(new FileMindError(ErrorCode.AiOffline, "Stub AI offline.", true, false)));
}

public sealed class CollectingEventSink : IEventSink
{
    public List<AppEvent> Events { get; } = [];
    public Task EmitAsync(AppEvent appEvent, CancellationToken ct = default)
    {
        Events.Add(appEvent);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryFileIndex : IFileIndexRepository
{
    private readonly ConcurrentDictionary<string, FileCandidate> _rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileIdentity> _ids = new(StringComparer.OrdinalIgnoreCase);

    public Task<Result<bool>> UpsertAsync(FileCandidate candidate, FileIdentity identity, CancellationToken ct = default)
    {
        _rows[candidate.SourceUri] = candidate;
        _ids[candidate.SourceUri] = identity;
        return Task.FromResult(Result<bool>.Ok(true));
    }

    public Task<Result<IReadOnlyList<FileIdentity>>> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<FileIdentity>>.Ok(
            (IReadOnlyList<FileIdentity>)_ids.Values.Where(i => i.Fingerprint == fingerprint).ToList()));

    public Task<Result<bool>> RemoveAsync(string path, CancellationToken ct = default) =>
        Task.FromResult(Result<bool>.Ok(_rows.TryRemove(path, out _) | _ids.TryRemove(path, out _)));

    public Task<Result<IReadOnlyList<FileCandidate>>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult(Result<IReadOnlyList<FileCandidate>>.Ok(
            (IReadOnlyList<FileCandidate>)_rows.Values.ToList()));

    public bool Contains(string path) => _rows.ContainsKey(path);
}

/// <summary>Hashes through an <see cref="IFileSystemAdapter"/> — same wire format as WindowsHashService.</summary>
public sealed class StreamHashService(IFileSystemAdapter fs) : IHashService
{
    private const int PartialBytes = 4 * 1024 * 1024;

    public async Task<Result<string>> HashAsync(string path, bool full, CancellationToken ct = default)
    {
        var opened = await fs.OpenReadAsync(path, ct);
        if (!opened.IsSuccess) return Result<string>.Fail(opened.Error!);
        await using var stream = opened.Value!;
        using var sha = SHA256.Create();
        if (!full && stream.Length > PartialBytes)
        {
            var buffer = new byte[PartialBytes];
            var read = await stream.ReadAsync(buffer, ct);
            return Result<string>.Ok($"sha256-partial:{Convert.ToHexStringLower(sha.ComputeHash(buffer, 0, read))}");
        }
        var fullHash = await sha.ComputeHashAsync(stream, ct);
        return Result<string>.Ok($"sha256:{Convert.ToHexStringLower(fullHash)}");
    }
}

public static class Candidates
{
    public static FileCandidate Create(
        string path,
        string? extension = null,
        long size = 1024,
        SourceScope scope = SourceScope.Downloads)
    {
        var name = path.Split('/', '\\').Last();
        var ext = extension ?? (name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : "");
        return new FileCandidate
        {
            ScanId = $"scan-{path.GetHashCode():x}",
            SourceUri = path,
            DisplayName = name,
            Extension = ext,
            MimeType = ext switch
            {
                "pdf" => "application/pdf",
                "txt" => "text/plain",
                "mp4" => "video/mp4",
                "jpg" or "jpeg" => "image/jpeg",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => null
            },
            SizeBytes = size,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1),
            SourceScope = scope
        };
    }
}

using System.Collections.Concurrent;
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

using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed record Notification(string Title, string Body, string? OperationId = null);

public interface INotificationService
{
    Task<Result<bool>> NotifyAsync(Notification notification, CancellationToken ct = default);
}

public interface ISecretStore
{
    Task<Result<string>> GetAsync(string key, CancellationToken ct = default);
    Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(string key, CancellationToken ct = default);
}

public sealed record WatchEvent(string Path, string ChangeKind, DateTimeOffset At);

public interface IWatcherEventSource
{
    IAsyncEnumerable<WatchEvent> EventsAsync(CancellationToken ct = default);
    Task<Result<bool>> StartWatchingAsync(string path, CancellationToken ct = default);
    Task<Result<bool>> StopWatchingAsync(string path, CancellationToken ct = default);
}

public sealed record DriveEvent(string DriveRoot, string Kind, DateTimeOffset At);

public interface IRemovableStorageEvents
{
    IAsyncEnumerable<DriveEvent> EventsAsync(CancellationToken ct = default);
}

public sealed record JobRequest(string JobType, string Payload, int Priority = 0);
public sealed record Job(string Id, JobRequest Request, string Status, DateTimeOffset QueuedAt);

public interface IJobQueue
{
    Task<Result<Job>> EnqueueAsync(JobRequest request, CancellationToken ct = default);
    Task<Result<Job?>> DequeueAsync(CancellationToken ct = default);
    Task<Result<bool>> CompleteAsync(string jobId, CancellationToken ct = default);
    Task<Result<bool>> FailAsync(string jobId, FileMindError error, CancellationToken ct = default);
}

public sealed record SearchHit(string Path, string? Collection, string? Subject, double Rank);

public interface ISearchIndex
{
    Task<Result<bool>> IndexAsync(FileCandidate candidate, string? extractedText, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(string path, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(string query, int limit, CancellationToken ct = default);
}

public sealed record AiSuggestion(
    string? Collection,
    string? Category,
    string? Subject,
    string? Project,
    string? SuggestedName,
    double ConfidenceHint,
    string Explanation);

public sealed record AiRequestContext(
    string DisplayName,
    string Extension,
    IReadOnlyDictionary<string, string> Metadata,
    string? ExtractedSnippet);

public interface IAiPrivacyGate
{
    Result<AiRequestContext> Prepare(FileCandidate candidate);
}

public interface IAIProvider
{
    Task<Result<AiSuggestion>> SuggestAsync(AiRequestContext context, CancellationToken ct = default);
    bool IsAvailable { get; }
}

/// <summary>
/// Advisory AI escalation: called after deterministic classification when the
/// mode allows AI. Returns a rescored classification with AI evidence merged,
/// or null when AI did not contribute (disabled, gated, unavailable, or the
/// classification was already high-confidence). Never mutates anything itself.
/// </summary>
public interface IAiAdvisor
{
    Task<Result<ClassificationResult?>> AssistAsync(
        FileCandidate candidate, ClassificationResult classification, CancellationToken ct = default);
}

public sealed record AppEvent(string Kind, string EntityId, DateTimeOffset At, string? Detail = null);

public interface IEventSink
{
    Task EmitAsync(AppEvent appEvent, CancellationToken ct = default);
}

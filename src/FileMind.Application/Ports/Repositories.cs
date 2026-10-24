using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Ports;

public interface IRuleRepository
{
    Task<Result<IReadOnlyList<Rule>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<Rule>> GetAsync(string id, CancellationToken ct = default);
    Task<Result<Rule>> UpsertAsync(Rule rule, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed record CollectionEntry(string Id, string Name, string? DestinationTemplate, IReadOnlyList<string> Categories);

public interface ICollectionRepository
{
    Task<Result<IReadOnlyList<CollectionEntry>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<CollectionEntry>> UpsertAsync(CollectionEntry entry, CancellationToken ct = default);
}

public interface ISubjectRepository
{
    Task<Result<IReadOnlyList<SubjectLike>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<SubjectLike>> UpsertAsync(SubjectLike entry, CancellationToken ct = default);
}

public sealed record SubjectLike(string Id, string Name, string? Code, IReadOnlyList<string> Aliases, IReadOnlyList<string> Keywords);

public interface IProjectRepository
{
    Task<Result<IReadOnlyList<ProjectLike>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ProjectLike>> UpsertAsync(ProjectLike entry, CancellationToken ct = default);
}

public sealed record ProjectLike(string Id, string Name, string? ClientName, IReadOnlyList<string> Aliases, IReadOnlyList<string> Keywords);

public interface IFileIndexRepository
{
    Task<Result<bool>> UpsertAsync(FileCandidate candidate, FileIdentity identity, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FileIdentity>>> FindByFingerprintAsync(string fingerprint, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(string path, CancellationToken ct = default);
    /// <summary>All indexed candidates (payload deserialized). Needed by duplicate/storage analysis.</summary>
    Task<Result<IReadOnlyList<FileCandidate>>> GetAllAsync(CancellationToken ct = default);
}

public interface IOperationRepository
{
    Task<Result<OperationRecord>> SaveAsync(OperationRecord record, CancellationToken ct = default);
    Task<Result<OperationRecord>> GetAsync(string operationId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OperationRecord>>> GetIncompleteAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<OperationRecord>>> GetRecentAsync(int limit, CancellationToken ct = default);
}

public sealed record SmartInboxItem(string Id, string Path, string Reason, ClassificationResult Classification, DateTimeOffset At, bool Resolved);

public interface ISmartInboxRepository
{
    Task<Result<SmartInboxItem>> AddAsync(SmartInboxItem item, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SmartInboxItem>>> GetOpenAsync(CancellationToken ct = default);
    Task<Result<bool>> ResolveAsync(string id, CancellationToken ct = default);
}

public interface IExclusionRepository
{
    Task<Result<IReadOnlyList<string>>> GetPatternsAsync(CancellationToken ct = default);
    Task<Result<bool>> AddAsync(string pattern, CancellationToken ct = default);
    bool Matches(string path);
}

public interface IProtectedPathRepository
{
    Task<Result<IReadOnlyList<string>>> GetUserPathsAsync(CancellationToken ct = default);
    Task<Result<bool>> AddAsync(string path, CancellationToken ct = default);
}

public interface IWatchedFolderRepository
{
    Task<Result<IReadOnlyList<string>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<bool>> AddAsync(string path, CancellationToken ct = default);
    Task<Result<bool>> RemoveAsync(string path, CancellationToken ct = default);
}

public interface IProfileRepository
{
    Task<Result<string>> ExportJsonAsync(CancellationToken ct = default);
    Task<Result<bool>> ImportJsonAsync(string json, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<Result<string?>> GetAsync(string key, CancellationToken ct = default);
    Task<Result<bool>> SetAsync(string key, string value, CancellationToken ct = default);
}

public interface IJobRepository
{
    Task<Result<Job>> EnqueueAsync(JobRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<Job>>> GetPendingAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<Job>>> GetByStatusAsync(string status, CancellationToken ct = default);
    Task<Result<bool>> UpdateStatusAsync(string jobId, string status, CancellationToken ct = default);
}

public interface IAiAuditRepository
{
    Task<Result<bool>> RecordAsync(string requestHash, string outcome, DateTimeOffset at, CancellationToken ct = default);
    Task<Result<IReadOnlyList<string>>> GetRecentAsync(int limit, CancellationToken ct = default);
}

/// <summary>Per-call token usage row. Stores counts and metadata only — never payload contents.</summary>
public sealed record AiUsageRecord(
    string RequestHash,
    string Model,
    string Purpose,
    int PromptTokens,
    int CompletionTokens,
    string Outcome,
    DateTimeOffset At);

/// <summary>
/// Persisted AI usage ledger backing the quota gate (PLAN-020). Counters are
/// derived by summing rows within rolling windows.
/// </summary>
public interface IAiUsageLedger
{
    Task<Result<bool>> RecordAsync(AiUsageRecord record, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AiUsageRecord>>> SinceAsync(DateTimeOffset from, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AiUsageRecord>>> GetRecentAsync(int limit, CancellationToken ct = default);
}

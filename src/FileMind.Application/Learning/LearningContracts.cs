using FileMind.Domain.Contracts;
using FileMind.Domain.Errors;

namespace FileMind.Application.Learning;

/// <summary>
/// A user override of a pipeline suggestion: the file was moved somewhere
/// other than planned (undo) or the user recorded a preferred destination.
/// Suggestion-mining input only — never an action.
/// </summary>
public sealed record CorrectionEvent(
    string Id,
    string Path,
    string? OriginalDestination,
    string ChosenDestination,
    string Source,
    DateTimeOffset At);

public interface ICorrectionRepository
{
    Task<Result<CorrectionEvent>> AddAsync(CorrectionEvent e, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CorrectionEvent>>> GetAllAsync(CancellationToken ct = default);
}

public interface IConfidenceCalibrationRepository
{
    Task<Result<double>> GetAdjustmentAsync(string key, CancellationToken ct = default);
    Task<Result<bool>> SetAdjustmentAsync(string key, double adjustment, DateTimeOffset at, CancellationToken ct = default);
    Task<Result<IReadOnlyDictionary<string, double>>> GetAllAsync(CancellationToken ct = default);
}

public enum CalibrationSignal { Accepted, Rejected }

/// <summary>
/// Bounded score adjustment learned from accepted/rejected outcomes.
/// Caps and band thresholds are untouched — calibration shifts the raw
/// score only, and safety/needs-review behavior is never overridden.
/// </summary>
public interface IConfidenceCalibrator
{
    Task<double> AdjustAsync(string key, double rawScore, CancellationToken ct = default);
    Task<Result<bool>> RecordAsync(string key, CalibrationSignal signal, CancellationToken ct = default);
}

/// <summary>A chain of files that look like successive versions of one document.</summary>
public sealed record VersionChain(
    string BaseName,
    IReadOnlyList<string> PathsOrderedOldestToNewest,
    string LatestPath,
    string Evidence);

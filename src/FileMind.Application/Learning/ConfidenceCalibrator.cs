using FileMind.Domain.Errors;

namespace FileMind.Application.Learning;

/// <summary>
/// PLAN-013 M2 — bounded score adjustment from accept/reject outcomes.
/// ±Step per signal, clamped to ±MaxAdjustment, final score stays in
/// [0, 100]. Band thresholds and evidence caps are untouched — a learned
/// adjustment can never bypass NeedsReview floors or safety policy.
/// </summary>
public sealed class ConfidenceCalibrator(IConfidenceCalibrationRepository repo) : IConfidenceCalibrator
{
    public const double Step = 5;
    public const double MaxAdjustment = 20;

    public async Task<double> AdjustAsync(string key, double rawScore, CancellationToken ct = default)
    {
        var delta = await repo.GetAdjustmentAsync(key, ct);
        var applied = delta.IsSuccess ? delta.Value : 0;
        return Math.Clamp(rawScore + applied, 0, 100);
    }

    public async Task<Result<bool>> RecordAsync(string key, CalibrationSignal signal, CancellationToken ct = default)
    {
        var cur = await repo.GetAdjustmentAsync(key, ct);
        if (!cur.IsSuccess) return Result<bool>.Fail(cur.Error!);
        var next = Math.Clamp(
            cur.Value + (signal == CalibrationSignal.Accepted ? Step : -Step),
            -MaxAdjustment, MaxAdjustment);
        return await repo.SetAdjustmentAsync(key, next, DateTimeOffset.UtcNow, ct);
    }
}

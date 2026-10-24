using FileMind.Domain.Errors;

namespace FileMind.WinUI.Services;

/// <summary>Error kinds the UI knows how to surface; populated from
/// FileMindError.Code so the switch stays total in one place.</summary>
public enum FmErrorKind
{
    PermissionDenied, ProtectedPath, DestinationConflict, SourceNotFound,
    Locked, InsufficientSpace, AiOffline, AiRateLimited, Cancelled,
    VerificationFailed, Validation, TransientIo, Persistence, Unknown
}

public enum FmErrorSeverity { Info, Warning, Error }

/// <summary>ux/06 anatomy: title + explanation + recovery; details optional.</summary>
public sealed record FmFriendlyError(
    string Title, string Explanation, string Recovery, FmErrorSeverity Severity,
    string? TechnicalDetails = null);

/// <summary>
/// PLAN-014 M2.2 — error → friendly surface. Never leaks stack
/// traces, OS codes, or secrets; the raw message rides in TechnicalDetails
/// behind disclosure.
/// </summary>
public static class FmErrorPresenter
{
    /// <summary>Domain error → friendly surface (real integration point).</summary>
    public static FmFriendlyError Present(FileMindError e) =>
        Present(FromCode(e.Code), e.Message);

    public static FmErrorKind FromCode(ErrorCode code) => code switch
    {
        ErrorCode.PermissionDenied => FmErrorKind.PermissionDenied,
        ErrorCode.ProtectedPath => FmErrorKind.ProtectedPath,
        ErrorCode.DestinationConflict => FmErrorKind.DestinationConflict,
        ErrorCode.SourceNotFound => FmErrorKind.SourceNotFound,
        ErrorCode.Locked => FmErrorKind.Locked,
        ErrorCode.InsufficientSpace => FmErrorKind.InsufficientSpace,
        ErrorCode.AiOffline => FmErrorKind.AiOffline,
        ErrorCode.AiRateLimited => FmErrorKind.AiRateLimited,
        ErrorCode.Cancelled => FmErrorKind.Cancelled,
        ErrorCode.VerificationFailed => FmErrorKind.VerificationFailed,
        ErrorCode.ValidationFailed => FmErrorKind.Validation,
        ErrorCode.TransientIo => FmErrorKind.TransientIo,
        ErrorCode.PersistenceFailure => FmErrorKind.Persistence,
        _ => FmErrorKind.Unknown
    };

    public static FmFriendlyError Present(FmErrorKind kind, string? technicalDetails = null) =>
        kind switch
    {
        FmErrorKind.PermissionDenied => new(
            "We couldn't access this folder.",
            "FileMind doesn't currently have permission to use it.",
            "Choose another folder or grant access, then try again.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.ProtectedPath => new(
            "This location is protected.",
            "It's a system or user-protected path, so FileMind won't touch it.",
            "Pick a different folder or review your protected-paths list.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.DestinationConflict => new(
            "A file with that name already exists.",
            "The destination already has a file with this name.",
            "Choose Keep Both, skip it, or rename one of the files.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.SourceNotFound => new(
            "The file is no longer there.",
            "It was moved, renamed, or removed before the operation ran.",
            "Rescan the folder to refresh the file list.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.Locked => new(
            "The file is in use.",
            "Another app has it open, so it can't be moved right now.",
            "Close the app using it and retry.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.InsufficientSpace => new(
            "Not enough storage space.",
            "The destination doesn't have room for these files.",
            "Free up space or choose another destination, then try again.",
            FmErrorSeverity.Error, technicalDetails),
        FmErrorKind.AiOffline => new(
            "AI assistance is offline.",
            "No provider key or connection — local results still work.",
            "Use the offline result, or configure a key in Settings.",
            FmErrorSeverity.Info, technicalDetails),
        FmErrorKind.AiRateLimited => new(
            "AI quota reached.",
            "The provider rate-limited this request; your budget is intact.",
            "Wait for the window to reset or continue without AI.",
            FmErrorSeverity.Info, technicalDetails),
        FmErrorKind.Cancelled => new(
            "Operation stopped.",
            "It was cancelled or interrupted before finishing.",
            "Review the state in Activity — nothing is silently changed.",
            FmErrorSeverity.Info, technicalDetails),
        FmErrorKind.VerificationFailed => new(
            "We couldn't verify the result.",
            "A file may have changed during the operation.",
            "Check it in Activity before retrying.",
            FmErrorSeverity.Error, technicalDetails),
        FmErrorKind.Validation => new(
            "A field needs attention.",
            technicalDetails ?? "Check the fields and try again.",
            "Fix it, then run the operation again.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.TransientIo => new(
            "The storage hit a hiccup.",
            "A file or folder operation failed temporarily.",
            "Try again — if it repeats, check the folder isn't syncing or locked.",
            FmErrorSeverity.Warning, technicalDetails),
        FmErrorKind.Persistence => new(
            "We couldn't save that.",
            "FileMind's local store rejected the write.",
            "Try again; if it repeats, restart the app.",
            FmErrorSeverity.Error, technicalDetails),
        _ => new(
            "Something went wrong.",
            "The operation didn't complete as expected.",
            "Check Activity for details, or try again.",
            FmErrorSeverity.Error, technicalDetails)
    };
}

using FileMind.Domain.Errors;

namespace FileMind.WinUI.Services;

public enum FmErrorSeverity { Info, Warning, Error }

/// <summary>ux/06 anatomy: title + explanation + recovery; details optional.</summary>
public sealed record FmFriendlyError(
    string Title, string Explanation, string Recovery, FmErrorSeverity Severity,
    string? TechnicalDetails = null);

/// <summary>
/// PLAN-014 M2.2 — FileMindError → friendly surface. Never leaks stack
/// traces, OS codes, or secrets; the raw message rides in TechnicalDetails
/// behind disclosure.
/// </summary>
public static class FmErrorPresenter
{
    public static FmFriendlyError Present(FileMindError e) => e.Code switch
    {
        ErrorCode.PermissionDenied => new(
            "We couldn't access this folder.",
            "FileMind doesn't currently have permission to use it.",
            "Choose another folder or grant access, then try again.",
            FmErrorSeverity.Warning, e.Message),
        ErrorCode.ProtectedPath => new(
            "This location is protected.",
            "It's a system or user-protected path, so FileMind won't touch it.",
            "Pick a different folder or review your protected-paths list.",
            FmErrorSeverity.Warning, e.Message),
        ErrorCode.DestinationConflict => new(
            "A file with that name already exists.",
            "The destination already has a file with this name.",
            "Choose Keep Both, skip it, or rename one of the files.",
            FmErrorSeverity.Warning, e.Message),
        ErrorCode.SourceNotFound => new(
            "The file is no longer there.",
            "It was moved, renamed, or removed before the operation ran.",
            "Rescan the folder to refresh the file list.",
            FmErrorSeverity.Warning, e.Message),
        ErrorCode.Locked => new(
            "The file is in use.",
            "Another app has it open, so it can't be moved right now.",
            "Close the app using it and retry.",
            FmErrorSeverity.Warning, e.Message),
        ErrorCode.InsufficientSpace => new(
            "Not enough storage space.",
            "The destination doesn't have room for these files.",
            "Free up space or choose another destination, then try again.",
            FmErrorSeverity.Error, e.Message),
        ErrorCode.AiOffline => new(
            "AI assistance is offline.",
            "No provider key or connection — local results still work.",
            "Use the offline result, or configure a key in Settings.",
            FmErrorSeverity.Info, e.Message),
        ErrorCode.AiRateLimited => new(
            "AI quota reached.",
            "The provider rate-limited this request; your budget is intact.",
            "Wait for the window to reset or continue without AI.",
            FmErrorSeverity.Info, e.Message),
        ErrorCode.Cancelled => new(
            "Operation stopped.",
            "It was cancelled or interrupted before finishing.",
            "Review the state in Activity — nothing is silently changed.",
            FmErrorSeverity.Info, e.Message),
        ErrorCode.VerificationFailed => new(
            "We couldn't verify the result.",
            "A file may have changed during the operation.",
            "Check it in Activity before retrying.",
            FmErrorSeverity.Error, e.Message),
        _ => new(
            "Something went wrong.",
            "The operation didn't complete as expected.",
            "Check Activity for details, or try again.",
            FmErrorSeverity.Error, e.Message)
    };
}

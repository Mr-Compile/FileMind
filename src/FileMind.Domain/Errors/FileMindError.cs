namespace FileMind.Domain.Errors;

public sealed record FileMindError(
    ErrorCode Code,
    string Message,
    bool RetrySafe,
    bool RequiresReview)
{
    public static FileMindError Validation(string message) =>
        new(ErrorCode.ValidationFailed, message, RetrySafe: false, RequiresReview: false);

    public static FileMindError Protected(string message) =>
        new(ErrorCode.ProtectedPath, message, RetrySafe: false, RequiresReview: true);

    public static FileMindError Conflict(string message) =>
        new(ErrorCode.DestinationConflict, message, RetrySafe: false, RequiresReview: true);

    public static FileMindError NotFound(string message) =>
        new(ErrorCode.SourceNotFound, message, RetrySafe: false, RequiresReview: false);

    public static FileMindError FileLocked(string message) =>
        new(ErrorCode.Locked, message, RetrySafe: true, RequiresReview: false);

    public static FileMindError NoSpace(string message) =>
        new(ErrorCode.InsufficientSpace, message, RetrySafe: false, RequiresReview: true);

    public static FileMindError Io(string message) =>
        new(ErrorCode.TransientIo, message, RetrySafe: true, RequiresReview: false);

    public static FileMindError Persistence(string message) =>
        new(ErrorCode.PersistenceFailure, message, RetrySafe: true, RequiresReview: true);

    public static FileMindError Verification(string message) =>
        new(ErrorCode.VerificationFailed, message, RetrySafe: false, RequiresReview: true);

    public static FileMindError Cancelled() =>
        new(ErrorCode.Cancelled, "Operation cancelled.", RetrySafe: true, RequiresReview: false);
}

namespace FileMind.Domain.Errors;

public enum ErrorCode
{
    ValidationFailed,
    PermissionDenied,
    ProtectedPath,
    DestinationConflict,
    SourceNotFound,
    Locked,
    InsufficientSpace,
    UnsupportedOperation,
    TransientIo,
    PersistenceFailure,
    VerificationFailed,
    RollbackFailed,
    AiOffline,
    AiRateLimited,
    AiInvalidResponse,
    Cancelled
}

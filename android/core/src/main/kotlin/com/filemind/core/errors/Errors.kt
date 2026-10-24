package com.filemind.core.errors

/**
 * Kotlin port of `FileMind.Domain/Errors`. Kept member-for-member identical so
 * the shared fixture suite sees the same codes/messages on both platforms.
 */
enum class ErrorCode {
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

@kotlinx.serialization.Serializable
data class FileMindError(
    val code: ErrorCode,
    val message: String,
    val retrySafe: Boolean,
    val requiresReview: Boolean
) {
    companion object {
        fun validation(message: String) =
            FileMindError(ErrorCode.ValidationFailed, message, retrySafe = false, requiresReview = false)

        fun protectedPath(message: String) =
            FileMindError(ErrorCode.ProtectedPath, message, retrySafe = false, requiresReview = true)

        fun conflict(message: String) =
            FileMindError(ErrorCode.DestinationConflict, message, retrySafe = false, requiresReview = true)

        fun notFound(message: String) =
            FileMindError(ErrorCode.SourceNotFound, message, retrySafe = false, requiresReview = false)

        fun fileLocked(message: String) =
            FileMindError(ErrorCode.Locked, message, retrySafe = true, requiresReview = false)

        fun noSpace(message: String) =
            FileMindError(ErrorCode.InsufficientSpace, message, retrySafe = false, requiresReview = true)

        fun io(message: String) =
            FileMindError(ErrorCode.TransientIo, message, retrySafe = true, requiresReview = false)

        fun persistence(message: String) =
            FileMindError(ErrorCode.PersistenceFailure, message, retrySafe = true, requiresReview = true)

        fun verification(message: String) =
            FileMindError(ErrorCode.VerificationFailed, message, retrySafe = false, requiresReview = true)

        fun cancelled() =
            FileMindError(ErrorCode.Cancelled, "Operation cancelled.", retrySafe = true, requiresReview = false)
    }
}

/**
 * Port of `Result<T>` (readonly record struct). `isSuccess` implies `value`
 * non-null and `error` null — mirrors the C# MemberNotNullWhen contract.
 */
class Result<out T> private constructor(
    val value: T?,
    val error: FileMindError?
) {
    val isSuccess: Boolean get() = error == null

    companion object {
        fun <T> ok(value: T): Result<T> = Result(value, null)
        fun <T> fail(error: FileMindError): Result<T> = Result(null, error)
    }
}

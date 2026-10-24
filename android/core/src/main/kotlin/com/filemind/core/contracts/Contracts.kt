@file:UseSerializers(InstantSerializer::class)

package com.filemind.core.contracts

import com.filemind.core.errors.FileMindError
import java.time.Instant
import kotlinx.serialization.Serializable
import kotlinx.serialization.UseSerializers

/** Number of .NET ticks (100ns) between 0001-01-01 and the Unix epoch. */
private const val TicksAtUnixEpoch = 621355968000000000L

/** .NET `DateTimeOffset.UtcTicks` equivalent so fingerprints match Windows. */
fun Instant.utcTicks(): Long =
    (epochSecond * 10_000_000L) + (nano / 100L) + TicksAtUnixEpoch

@Serializable
data class PermissionCapabilities(
    val canRead: Boolean,
    val canWrite: Boolean,
    val canMove: Boolean,
    val canCopy: Boolean,
    val canRename: Boolean,
    val canCreateFolder: Boolean
) {
    companion object {
        val Default = PermissionCapabilities(
            canRead = true, canWrite = true, canMove = true,
            canCopy = true, canRename = true, canCreateFolder = true
        )
    }
}

@Serializable
data class FileCandidate(
    val scanId: String,
    val sourceUri: String,
    val displayName: String,
    val extension: String,
    val mimeType: String? = null,
    val sizeBytes: Long,
    val createdAt: Instant,
    val modifiedAt: Instant,
    val sourceScope: SourceScope,
    val metadata: Map<String, String> = emptyMap(),
    val extractedTextRef: String? = null,
    val hashState: HashState = HashState.NotComputed,
    val contentHash: String? = null,
    val capabilities: PermissionCapabilities = PermissionCapabilities.Default,
    val platformHints: Map<String, String> = emptyMap()
)

@Serializable
data class Evidence(
    val kind: String,
    val detail: String,
    val scoreContribution: Double,
    val source: ClassifierSource
)

@Serializable
data class ClassificationResult(
    val fileKind: FileKind,
    val evidence: List<Evidence>,
    val confidence: Double,
    val sources: List<ClassifierSource>,
    val explanation: String,
    val collection: String? = null,
    val category: String? = null,
    val subcategory: String? = null,
    val subject: String? = null,
    val project: String? = null,
    val warnings: List<String> = emptyList(),
    val needsReview: Boolean = false
) {
    companion object {
        fun unknown(explanation: String) = ClassificationResult(
            fileKind = FileKind.Other,
            evidence = emptyList(),
            confidence = 0.0,
            sources = listOf(ClassifierSource.Local),
            explanation = explanation
        )
    }
}

@Serializable
data class ActionPlan(
    val planId: String,
    val createdAt: Instant,
    val sourceDecision: String,
    val items: List<ActionItem>,
    val requiresReview: Boolean = false,
    val dryRun: Boolean = false,
    val reversibleEstimate: Boolean = false
)

@Serializable
data class ActionItem(
    val operation: OperationType,
    val source: String,
    val destination: String? = null,
    val conflictPolicy: ConflictPolicy = ConflictPolicy.KeepBoth,
    val preconditions: List<String> = emptyList(),
    val reason: String,
    val undoRecipe: UndoRecipe? = null
) {
    val requiresDestination: Boolean
        get() = operation == OperationType.Move || operation == OperationType.Copy ||
                operation == OperationType.Rename || operation == OperationType.CreateFolder

    val isValid: Boolean
        get() = !requiresDestination || !destination.isNullOrBlank()
}

@Serializable
data class UndoRecipe(
    val reverseOperation: OperationType,
    val reverseSource: String,
    val reverseDestination: String,
    val fingerprint: String,
    val preconditions: List<String>
)

@Serializable
data class SafetyResult(
    val allowed: Boolean,
    val blockers: List<String> = emptyList(),
    val warnings: List<String> = emptyList(),
    val requiredConfirmation: String? = null
) {
    companion object {
        fun allow(vararg warnings: String) = SafetyResult(true, warnings = warnings.toList())
        fun block(vararg blockers: String) = SafetyResult(false, blockers = blockers.toList())
        fun needsConfirmation(confirmation: String, vararg warnings: String) =
            SafetyResult(true, requiredConfirmation = confirmation, warnings = warnings.toList())
    }
}

@Serializable
data class OperationRecord(
    val operationId: String,
    val planId: String,
    val status: OperationStatus,
    val items: List<OperationItemState>,
    val createdAt: Instant,
    val committedAt: Instant? = null,
    val undoOfOperationId: String? = null,
    val error: FileMindError? = null
) {
    val isTerminal: Boolean
        get() = status == OperationStatus.Committed || status == OperationStatus.Failed ||
                status == OperationStatus.RolledBack
}

@Serializable
data class OperationItemState(
    val itemId: String,
    val operation: OperationType,
    val originalLocation: String,
    val resultingLocation: String? = null,
    val status: OperationStatus,
    val verification: String? = null,
    val error: FileMindError? = null,
    val undoRecipe: UndoRecipe? = null
)

@Serializable
data class FileIdentity(
    val stableId: String,
    val pathHint: String,
    val fingerprint: String
) {
    companion object {
        fun computeFingerprint(sizeBytes: Long, modifiedAt: Instant): String =
            "${sizeBytes.toString(16)}:${modifiedAt.utcTicks().toString(16)}"
    }
}

// --- Duplicates & storage contracts (PLAN-008) ---

@Serializable
data class DuplicateFile(val path: String, val sizeBytes: Long, val modifiedAt: Instant)

@Serializable
data class DuplicateGroup(
    val contentHash: String,
    val sizeBytes: Long,
    val reclaimableBytes: Long,
    val files: List<DuplicateFile>,
    val confirmed: Boolean
)

@Serializable
data class SimilarityCandidate(
    val pathA: String,
    val pathB: String,
    val score: Double,
    val reasons: List<String>
)

@Serializable
data class StorageBucket(val label: String, val bytes: Long, val count: Long)

@Serializable
data class StorageFileEntry(
    val path: String,
    val sizeBytes: Long,
    val modifiedAt: Instant,
    val ageDays: Int
)

@Serializable
data class StorageReport(
    val totalBytes: Long,
    val fileCount: Long,
    val byKind: List<StorageBucket>,
    val byAgeBand: List<StorageBucket>,
    val largest: List<StorageFileEntry>,
    val oldest: List<StorageFileEntry>,
    val duplicateReclaimableBytes: Long
)

@Serializable
data class DuplicatesReport(
    val groups: List<DuplicateGroup>,
    val similar: List<SimilarityCandidate>,
    val warnings: List<String>
) {
    val totalReclaimable: Long get() = groups.sumOf { it.reclaimableBytes }
}

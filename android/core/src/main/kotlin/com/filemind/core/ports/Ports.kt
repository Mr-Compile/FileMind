@file:kotlinx.serialization.UseSerializers(com.filemind.core.contracts.InstantSerializer::class)

package com.filemind.core.ports

import com.filemind.core.contracts.Evidence
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileKind
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.PermissionCapabilities
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import java.io.InputStream
import java.time.Instant
import kotlinx.coroutines.flow.Flow

@kotlinx.serialization.Serializable
data class FileStat(
    val path: String,
    val sizeBytes: Long,
    val createdAt: Instant,
    val modifiedAt: Instant,
    val isDirectory: Boolean,
    val fileIndexId: String? = null
)

@kotlinx.serialization.Serializable
data class FileEntry(
    val path: String,
    val sizeBytes: Long,
    val createdAt: Instant,
    val modifiedAt: Instant,
    /** Provider-reported display name; preferred over URI-derived names
     *  (SAF/MediaStore docIds may be opaque, e.g. "msf:1234"). */
    val displayName: String? = null,
    /** Provider-reported MIME type; preferred over extension-derived mime. */
    val mimeType: String? = null
)

interface FileSystemAdapter {
    fun enumerate(root: String, recursive: Boolean): Flow<FileEntry>
    suspend fun stat(path: String): Result<FileStat>
    suspend fun openRead(path: String): Result<InputStream>
    suspend fun exists(path: String): Result<Boolean>
    suspend fun createDirectory(path: String): Result<Boolean>
    suspend fun move(source: String, destination: String, conflict: ConflictPolicy): Result<String>
    suspend fun copy(source: String, destination: String, conflict: ConflictPolicy): Result<String>
    suspend fun rename(source: String, newName: String, conflict: ConflictPolicy): Result<String>
    suspend fun checkCapabilities(path: String): Result<PermissionCapabilities>
    suspend fun getFreeSpace(path: String): Result<Long>
    suspend fun verify(path: String, expectedFingerprint: String): Result<Boolean>
    fun getProtectedPaths(): List<String>
}

interface ContentExtractor {
    suspend fun extractText(candidate: FileCandidate, maxChars: Int): Result<String>
}

interface MetadataExtractor {
    suspend fun extract(candidate: FileCandidate): Result<Map<String, String>>
}

/** Extraction output merged onto a FileCandidate before classification. */
@kotlinx.serialization.Serializable
data class CandidateEnrichment(
    val metadata: Map<String, String>,
    val extractedText: String?,
    val evidence: List<Evidence>,
    val warnings: List<String>,
    val reliable: Boolean
)

/**
 * Local (offline) content/metadata enrichment. Implementations must be
 * bounded by size/time budgets and fail-safe per file — a corrupt or
 * unsupported file yields warnings, never an abort of the whole scan.
 */
interface CandidateEnricher {
    suspend fun enrich(candidate: FileCandidate, fs: FileSystemAdapter): Result<CandidateEnrichment>
}

interface HashService {
    suspend fun hash(path: String, full: Boolean): Result<String>
}

interface PathNormalizer {
    fun normalize(path: String): String
    fun combine(vararg parts: String): String
    fun isUnder(path: String, root: String): Boolean
    /**
     * Normalized path of [path] relative to [root]; "" when equal,
     * null when not under root.
     */
    fun relativeTo(path: String, root: String): String?
    fun fileName(path: String): String
    fun directoryOf(path: String): String?
}

interface ProtectedPathProvider {
    fun isProtected(path: String): Boolean
    val protectedRoots: List<String>
}

interface OperationStore {
    suspend fun load(operationId: String): Result<OperationRecord>
    suspend fun save(record: OperationRecord): Result<OperationRecord>
    suspend fun loadIncomplete(): Result<List<OperationRecord>>
}

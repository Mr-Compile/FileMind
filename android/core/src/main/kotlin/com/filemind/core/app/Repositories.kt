@file:kotlinx.serialization.UseSerializers(com.filemind.core.contracts.InstantSerializer::class)

package com.filemind.core.app

import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.Rule
import com.filemind.core.errors.Result
import java.time.Instant

interface RuleRepository {
    suspend fun getAll(): Result<List<Rule>>
    suspend fun get(id: String): Result<Rule>
    suspend fun upsert(rule: Rule): Result<Rule>
    suspend fun delete(id: String): Result<Boolean>
}

interface CollectionRepository {
    suspend fun getAll(): Result<List<CollectionEntry>>
    suspend fun upsert(entry: CollectionEntry): Result<CollectionEntry>
}

interface SubjectRepository {
    suspend fun getAll(): Result<List<SubjectLike>>
    suspend fun upsert(entry: SubjectLike): Result<SubjectLike>
    suspend fun remove(id: String): Result<Boolean>
}

interface ProjectRepository {
    suspend fun getAll(): Result<List<ProjectLike>>
    suspend fun upsert(entry: ProjectLike): Result<ProjectLike>
}

interface FileIndexRepository {
    suspend fun upsert(candidate: FileCandidate, identity: FileIdentity): Result<Boolean>
    suspend fun findByFingerprint(fingerprint: String): Result<List<FileIdentity>>
    suspend fun remove(path: String): Result<Boolean>
    /** All indexed candidates (payload deserialized). Needed by duplicate/storage analysis. */
    suspend fun getAll(): Result<List<FileCandidate>>
}

interface OperationRepository {
    suspend fun save(record: OperationRecord): Result<OperationRecord>
    suspend fun get(operationId: String): Result<OperationRecord>
    suspend fun getIncomplete(): Result<List<OperationRecord>>
    suspend fun getRecent(limit: Int): Result<List<OperationRecord>>
}

interface SmartInboxRepository {
    suspend fun add(item: SmartInboxItem): Result<SmartInboxItem>
    suspend fun getOpen(): Result<List<SmartInboxItem>>
    suspend fun resolve(id: String): Result<Boolean>
}

interface ExclusionRepository {
    suspend fun getPatterns(): Result<List<String>>
    suspend fun add(pattern: String): Result<Boolean>
    fun matches(path: String): Boolean
}

interface ProtectedPathRepository {
    suspend fun getUserPaths(): Result<List<String>>
    suspend fun add(path: String): Result<Boolean>
}

interface WatchedFolderRepository {
    suspend fun getAll(): Result<List<String>>
    suspend fun add(path: String): Result<Boolean>
    suspend fun remove(path: String): Result<Boolean>
}

interface ProfileRepository {
    suspend fun exportJson(): Result<String>
    suspend fun importJson(json: String): Result<Boolean>
}

interface SettingsRepository {
    suspend fun get(key: String): Result<String?>
    suspend fun set(key: String, value: String): Result<Boolean>
}

interface JobRepository {
    suspend fun enqueue(request: JobRequest): Result<Job>
    suspend fun getPending(): Result<List<Job>>
    suspend fun getByStatus(status: String): Result<List<Job>>
    suspend fun updateStatus(jobId: String, status: String): Result<Boolean>
}

interface AiAuditRepository {
    suspend fun record(requestHash: String, outcome: String, at: Instant): Result<Boolean>
    suspend fun getRecent(limit: Int): Result<List<String>>
}

/** Per-call token usage row. Counts and metadata only — never payload contents. */
@kotlinx.serialization.Serializable
data class AiUsageRecord(
    val requestHash: String,
    val model: String,
    val purpose: String,
    val promptTokens: Int,
    val completionTokens: Int,
    val outcome: String,
    val at: Instant
)

/** Persisted AI usage ledger backing the quota gate (PLAN-020). */
interface AiUsageLedger {
    suspend fun record(record: AiUsageRecord): Result<Boolean>
    suspend fun since(from: Instant): Result<List<AiUsageRecord>>
    suspend fun getRecent(limit: Int): Result<List<AiUsageRecord>>
}

package com.filemind.platform.db

import com.filemind.core.app.AiAuditRepository
import com.filemind.core.app.AiUsageLedger
import com.filemind.core.app.AiUsageRecord
import com.filemind.core.app.CollectionEntry
import com.filemind.core.app.CollectionRepository
import com.filemind.core.app.ExclusionRepository
import com.filemind.core.app.FileIndexRepository
import com.filemind.core.app.Job
import com.filemind.core.app.JobRepository
import com.filemind.core.app.JobRequest
import com.filemind.core.app.OperationRepository
import com.filemind.core.app.ProjectLike
import com.filemind.core.app.ProjectRepository
import com.filemind.core.app.ProtectedPathRepository
import com.filemind.core.app.RuleRepository
import com.filemind.core.app.SettingsRepository
import com.filemind.core.app.SmartInboxItem
import com.filemind.core.app.SmartInboxRepository
import com.filemind.core.app.SubjectLike
import com.filemind.core.app.SubjectRepository
import com.filemind.core.app.WatchedFolderRepository
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.FileMindJson
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.Rule
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.OperationStore
import java.time.Instant
import java.util.UUID
import kotlinx.serialization.encodeToString

private fun <T> io(block: () -> T): Result<T> = try {
    Result.ok(block())
} catch (e: Exception) {
    Result.fail(FileMindError.persistence("Room: ${e.message ?: e.javaClass.simpleName}"))
}

private suspend fun <T> suspendIo(block: suspend () -> T): Result<T> = try {
    Result.ok(block())
} catch (e: Exception) {
    Result.fail(FileMindError.persistence("Room: ${e.message ?: e.javaClass.simpleName}"))
}

class RoomRuleRepository(private val dao: RuleDao) : RuleRepository {
    override suspend fun getAll() = suspendIo {
        dao.all().map { FileMindJson.decodeFromString<Rule>(it.payload) }
    }

    override suspend fun get(id: String) = suspendIo {
        dao.byId(id)?.let { FileMindJson.decodeFromString<Rule>(it.payload) }
            ?: throw NoSuchElementException("rule $id")
    }

    override suspend fun upsert(rule: Rule) = suspendIo {
        dao.upsert(RuleEntity(
            id = rule.id, name = rule.name, enabled = rule.enabled,
            priority = rule.priority, mode = rule.mode,
            schemaVersion = rule.schemaVersion,
            payload = FileMindJson.encodeToString(rule)))
        rule
    }

    override suspend fun delete(id: String) = suspendIo { dao.delete(id) > 0 }
}

class RoomCollectionRepository(private val dao: CollectionDao) : CollectionRepository {
    override suspend fun getAll() = suspendIo {
        dao.all().map { FileMindJson.decodeFromString<CollectionEntry>(it.payload) }
    }

    override suspend fun upsert(entry: CollectionEntry) = suspendIo {
        dao.upsert(CollectionEntity(entry.id, entry.name, FileMindJson.encodeToString(entry)))
        entry
    }
}

class RoomSubjectRepository(private val dao: SubjectDao) : SubjectRepository {
    override suspend fun getAll() = suspendIo {
        dao.all().map { FileMindJson.decodeFromString<SubjectLike>(it.payload) }
    }

    override suspend fun upsert(entry: SubjectLike) = suspendIo {
        dao.upsert(SubjectEntity(entry.id, entry.name, entry.code,
            FileMindJson.encodeToString(entry)))
        entry
    }
}

class RoomProjectRepository(private val dao: ProjectDao) : ProjectRepository {
    override suspend fun getAll() = suspendIo {
        dao.all().map { FileMindJson.decodeFromString<ProjectLike>(it.payload) }
    }

    override suspend fun upsert(entry: ProjectLike) = suspendIo {
        dao.upsert(ProjectEntity(entry.id, entry.name, entry.clientName,
            FileMindJson.encodeToString(entry)))
        entry
    }
}

class RoomFileIndexRepository(private val dao: FileIndexDao) : FileIndexRepository {
    override suspend fun upsert(candidate: FileCandidate, identity: FileIdentity) = suspendIo {
        dao.upsert(FileIndexEntity(
            path = candidate.sourceUri,
            stableId = identity.stableId,
            fingerprint = identity.fingerprint,
            indexedAt = Instant.now().toEpochMilli(),
            payload = FileMindJson.encodeToString(candidate)))
        true
    }

    override suspend fun findByFingerprint(fingerprint: String) = suspendIo {
        dao.byFingerprint(fingerprint).map {
            FileIdentity(it.stableId, it.path, it.fingerprint)
        }
    }

    override suspend fun remove(path: String) = suspendIo { dao.remove(path) > 0 }

    override suspend fun getAll() = suspendIo {
        dao.all().map { FileMindJson.decodeFromString<FileCandidate>(it.payload) }
    }
}

class RoomOperationRepository(private val dao: OperationDao) : OperationRepository {
    override suspend fun save(record: OperationRecord) = suspendIo {
        dao.upsert(toEntity(record))
        record
    }

    override suspend fun get(operationId: String) = suspendIo {
        dao.byId(operationId)?.let { FileMindJson.decodeFromString<OperationRecord>(it.payload) }
            ?: throw NoSuchElementException("operation $operationId")
    }

    override suspend fun getIncomplete() = suspendIo {
        dao.incomplete().map { FileMindJson.decodeFromString<OperationRecord>(it.payload) }
    }

    override suspend fun getRecent(limit: Int) = suspendIo {
        dao.recent(limit).map { FileMindJson.decodeFromString<OperationRecord>(it.payload) }
    }

    internal fun toEntity(r: OperationRecord) = OperationEntity(
        operationId = r.operationId, planId = r.planId, status = r.status.name,
        createdAt = r.createdAt.toEpochMilli(),
        committedAt = r.committedAt?.toEpochMilli(),
        undoOfOperationId = r.undoOfOperationId,
        payload = FileMindJson.encodeToString(r))
}

/** Core OperationStore port over the same table — used by TransactionEngine. */
class RoomOperationStore(private val repo: RoomOperationRepository) : OperationStore {
    override suspend fun load(operationId: String) = repo.get(operationId)
    override suspend fun save(record: OperationRecord) = repo.save(record)
    override suspend fun loadIncomplete() = repo.getIncomplete()
}

class RoomSmartInboxRepository(private val dao: SmartInboxDao) : SmartInboxRepository {
    override suspend fun add(item: SmartInboxItem) = suspendIo {
        dao.upsert(SmartInboxEntity(item.id, item.path, item.reason,
            item.at.toEpochMilli(), item.resolved, FileMindJson.encodeToString(item)))
        item
    }

    override suspend fun getOpen() = suspendIo {
        dao.open().map { FileMindJson.decodeFromString<SmartInboxItem>(it.payload) }
    }

    override suspend fun resolve(id: String) = suspendIo { dao.resolve(id) > 0 }
}

class RoomExclusionRepository(
    private val dao: ExclusionDao,
    defaults: List<String>
) : ExclusionRepository {
    @Volatile private var cached: List<String>? = null
    private val defaults = defaults

    override suspend fun getPatterns() = suspendIo {
        (defaults + dao.all()).distinct().also { cached = it }
    }

    override suspend fun add(pattern: String) = suspendIo {
        cached = null
        dao.add(ExclusionEntity(pattern)) >= 0
    }

    override fun matches(path: String): Boolean {
        val patterns = cached ?: return false
        val matcher = com.filemind.core.app.ExclusionMatcher(patterns)
        return matcher.isExcluded(path)
    }
}

class RoomProtectedPathRepository(private val dao: ProtectedPathDao) : ProtectedPathRepository {
    override suspend fun getUserPaths() = suspendIo { dao.all() }
    override suspend fun add(path: String) = suspendIo { dao.add(ProtectedPathEntity(path)) >= 0 }
}

class RoomWatchedFolderRepository(private val dao: WatchedFolderDao) : WatchedFolderRepository {
    override suspend fun getAll() = suspendIo { dao.all() }
    override suspend fun add(path: String) = suspendIo { dao.add(WatchedFolderEntity(path)) >= 0 }
    override suspend fun remove(path: String) = suspendIo { dao.remove(path) > 0 }
}

class RoomSettingsRepository(private val dao: SettingsDao) : SettingsRepository {
    override suspend fun get(key: String) = suspendIo { dao.get(key) }
    override suspend fun set(key: String, value: String) = suspendIo {
        dao.set(SettingsEntity(key, value)); true
    }
}

class RoomJobRepository(private val dao: JobDao) : JobRepository {
    override suspend fun enqueue(request: JobRequest) = suspendIo {
        val job = Job(
            id = UUID.randomUUID().toString().replace("-", ""),
            request = request, status = "pending", queuedAt = Instant.now())
        dao.upsert(JobEntity(job.id, request.jobType, job.status, request.priority,
            job.queuedAt.toEpochMilli(), request.payload))
        job
    }

    override suspend fun getPending() = suspendIo {
        dao.pending().map { Job(it.id, JobRequest(it.jobType, it.payload, it.priority),
            it.status, Instant.ofEpochMilli(it.queuedAt)) }
    }

    override suspend fun getByStatus(status: String) = suspendIo {
        dao.byStatus(status).map { Job(it.id, JobRequest(it.jobType, it.payload, it.priority),
            it.status, Instant.ofEpochMilli(it.queuedAt)) }
    }

    override suspend fun updateStatus(jobId: String, status: String) =
        suspendIo { dao.updateStatus(jobId, status) > 0 }
}

class RoomAiAuditRepository(private val dao: AiAuditDao) : AiAuditRepository {
    override suspend fun record(requestHash: String, outcome: String, at: Instant) =
        suspendIo { dao.insert(AiAuditEntity(requestHash, outcome, at.toEpochMilli())); true }

    override suspend fun getRecent(limit: Int) = suspendIo { dao.recent(limit) }
}

class RoomAiUsageLedger(private val dao: AiUsageDao) : AiUsageLedger {
    override suspend fun record(record: AiUsageRecord) = suspendIo {
        dao.insert(AiUsageEntity(record.requestHash, record.model, record.purpose,
            record.promptTokens, record.completionTokens, record.outcome,
            record.at.toEpochMilli()))
        true
    }

    override suspend fun since(from: Instant) = suspendIo {
        dao.since(from.toEpochMilli()).map {
            AiUsageRecord(it.requestHash, it.model, it.purpose, it.promptTokens,
                it.completionTokens, it.outcome, Instant.ofEpochMilli(it.at))
        }
    }

    override suspend fun getRecent(limit: Int) = suspendIo {
        dao.recent(limit).map {
            AiUsageRecord(it.requestHash, it.model, it.purpose, it.promptTokens,
                it.completionTokens, it.outcome, Instant.ofEpochMilli(it.at))
        }
    }
}

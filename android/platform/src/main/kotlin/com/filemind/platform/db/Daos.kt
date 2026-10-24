package com.filemind.platform.db

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface RuleDao {
    @Query("SELECT * FROM rules")
    suspend fun all(): List<RuleEntity>

    @Query("SELECT * FROM rules WHERE id = :id")
    suspend fun byId(id: String): RuleEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: RuleEntity)

    @Query("DELETE FROM rules WHERE id = :id")
    suspend fun delete(id: String): Int
}

@Dao
interface CollectionDao {
    @Query("SELECT * FROM collections")
    suspend fun all(): List<CollectionEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: CollectionEntity)
}

@Dao
interface SubjectDao {
    @Query("SELECT * FROM subjects")
    suspend fun all(): List<SubjectEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: SubjectEntity)
}

@Dao
interface ProjectDao {
    @Query("SELECT * FROM projects")
    suspend fun all(): List<ProjectEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: ProjectEntity)
}

@Dao
interface FileIndexDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: FileIndexEntity)

    @Query("SELECT * FROM file_index WHERE fingerprint = :fingerprint")
    suspend fun byFingerprint(fingerprint: String): List<FileIndexEntity>

    @Query("DELETE FROM file_index WHERE path = :path")
    suspend fun remove(path: String): Int

    @Query("SELECT * FROM file_index")
    suspend fun all(): List<FileIndexEntity>
}

@Dao
interface OperationDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: OperationEntity)

    @Query("SELECT * FROM operations WHERE operationId = :id")
    suspend fun byId(id: String): OperationEntity?

    @Query("SELECT * FROM operations WHERE status NOT IN ('Committed','Failed','RolledBack')")
    suspend fun incomplete(): List<OperationEntity>

    @Query("SELECT * FROM operations ORDER BY createdAt DESC LIMIT :limit")
    suspend fun recent(limit: Int): List<OperationEntity>
}

@Dao
interface SmartInboxDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: SmartInboxEntity)

    @Query("SELECT * FROM smart_inbox WHERE resolved = 0 ORDER BY at DESC")
    suspend fun open(): List<SmartInboxEntity>

    @Query("UPDATE smart_inbox SET resolved = 1 WHERE id = :id")
    suspend fun resolve(id: String): Int
}

@Dao
interface ExclusionDao {
    @Query("SELECT pattern FROM exclusions")
    suspend fun all(): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun add(entity: ExclusionEntity): Long
}

@Dao
interface ProtectedPathDao {
    @Query("SELECT path FROM protected_paths")
    suspend fun all(): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun add(entity: ProtectedPathEntity): Long
}

@Dao
interface WatchedFolderDao {
    @Query("SELECT path FROM watched_folders")
    suspend fun all(): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun add(entity: WatchedFolderEntity): Long

    @Query("DELETE FROM watched_folders WHERE path = :path")
    suspend fun remove(path: String): Int
}

@Dao
interface SettingsDao {
    @Query("SELECT value FROM settings WHERE `key` = :key")
    suspend fun get(key: String): String?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun set(entity: SettingsEntity)
}

@Dao
interface JobDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: JobEntity)

    @Query("SELECT * FROM jobs WHERE status = 'pending' ORDER BY priority DESC, queuedAt ASC")
    suspend fun pending(): List<JobEntity>

    @Query("SELECT * FROM jobs WHERE status = :status")
    suspend fun byStatus(status: String): List<JobEntity>

    @Query("UPDATE jobs SET status = :status WHERE id = :jobId")
    suspend fun updateStatus(jobId: String, status: String): Int
}

@Dao
interface UriGrantDao {
    @Query("SELECT * FROM uri_grants")
    suspend fun all(): List<UriGrantEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: UriGrantEntity)

    @Query("DELETE FROM uri_grants WHERE treeUri = :treeUri")
    suspend fun remove(treeUri: String): Int
}

@Dao
interface AiAuditDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: AiAuditEntity)

    @Query("SELECT outcome FROM ai_audit ORDER BY at DESC LIMIT :limit")
    suspend fun recent(limit: Int): List<String>
}

@Dao
interface AiUsageDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: AiUsageEntity)

    @Query("SELECT * FROM ai_usage WHERE at >= :fromEpochMs ORDER BY at ASC")
    suspend fun since(fromEpochMs: Long): List<AiUsageEntity>

    @Query("SELECT * FROM ai_usage ORDER BY at DESC LIMIT :limit")
    suspend fun recent(limit: Int): List<AiUsageEntity>
}

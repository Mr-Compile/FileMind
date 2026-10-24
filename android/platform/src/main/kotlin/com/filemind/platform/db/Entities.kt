package com.filemind.platform.db

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(tableName = "rules")
data class RuleEntity(
    @PrimaryKey val id: String,
    val name: String,
    val enabled: Boolean,
    val priority: Int,
    val mode: String,
    val schemaVersion: Int,
    val payload: String // Rule JSON
)

@Entity(tableName = "collections")
data class CollectionEntity(
    @PrimaryKey val id: String,
    val name: String,
    val payload: String // CollectionEntry JSON
)

@Entity(tableName = "subjects")
data class SubjectEntity(
    @PrimaryKey val id: String,
    val name: String,
    val code: String?,
    val payload: String
)

@Entity(tableName = "projects")
data class ProjectEntity(
    @PrimaryKey val id: String,
    val name: String,
    val clientName: String?,
    val payload: String
)

@Entity(
    tableName = "file_index",
    indices = [Index("fingerprint")]
)
data class FileIndexEntity(
    @PrimaryKey val path: String,
    val stableId: String,
    val fingerprint: String,
    val indexedAt: Long,
    val payload: String // FileCandidate JSON
)

@Entity(
    tableName = "operations",
    indices = [Index("status"), Index("committedAt")]
)
data class OperationEntity(
    @PrimaryKey val operationId: String,
    val planId: String,
    val status: String,
    val createdAt: Long,
    val committedAt: Long?,
    val undoOfOperationId: String?,
    val payload: String // OperationRecord JSON
)

@Entity(tableName = "smart_inbox", indices = [Index("resolved")])
data class SmartInboxEntity(
    @PrimaryKey val id: String,
    val path: String,
    val reason: String,
    val at: Long,
    val resolved: Boolean,
    val payload: String // SmartInboxItem JSON
)

@Entity(tableName = "exclusions")
data class ExclusionEntity(@PrimaryKey val pattern: String)

@Entity(tableName = "protected_paths")
data class ProtectedPathEntity(@PrimaryKey val path: String)

@Entity(tableName = "watched_folders")
data class WatchedFolderEntity(@PrimaryKey val path: String)

@Entity(tableName = "settings")
data class SettingsEntity(@PrimaryKey val key: String, val value: String)

@Entity(tableName = "jobs", indices = [Index("status")])
data class JobEntity(
    @PrimaryKey val id: String,
    val jobType: String,
    val status: String,
    val priority: Int,
    val queuedAt: Long,
    val payload: String
)

@Entity(tableName = "uri_grants")
data class UriGrantEntity(
    @PrimaryKey val treeUri: String,
    val authority: String,
    val docId: String,
    val canRead: Boolean,
    val canWrite: Boolean,
    val persisted: Boolean,
    val grantedAt: Long
)

@Entity(tableName = "ai_audit")
data class AiAuditEntity(
    @PrimaryKey val requestHash: String,
    val outcome: String,
    val at: Long
)

/** Added by MIGRATION_1_2 — PLAN-020 usage ledger. */
@Entity(tableName = "ai_usage", indices = [Index("at")])
data class AiUsageEntity(
    @PrimaryKey val requestHash: String,
    val model: String,
    val purpose: String,
    val promptTokens: Int,
    val completionTokens: Int,
    val outcome: String,
    val at: Long
)

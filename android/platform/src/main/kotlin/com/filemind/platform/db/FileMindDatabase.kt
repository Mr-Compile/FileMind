package com.filemind.platform.db

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

/**
 * Room mirror of the Windows SqliteStore repositories.
 *
 * v1 — baseline schema (rules/catalogs/index/operations/inbox/policies/jobs/
 *      grants/audit).
 * v2 — adds ai_usage ledger table (PLAN-020 parity).
 */
@Database(
    entities = [
        RuleEntity::class, CollectionEntity::class, SubjectEntity::class,
        ProjectEntity::class, FileIndexEntity::class, OperationEntity::class,
        SmartInboxEntity::class, ExclusionEntity::class, ProtectedPathEntity::class,
        WatchedFolderEntity::class, SettingsEntity::class, JobEntity::class,
        UriGrantEntity::class, AiAuditEntity::class, AiUsageEntity::class
    ],
    version = 2,
    exportSchema = true
)
abstract class FileMindDatabase : RoomDatabase() {
    abstract fun rules(): RuleDao
    abstract fun collections(): CollectionDao
    abstract fun subjects(): SubjectDao
    abstract fun projects(): ProjectDao
    abstract fun fileIndex(): FileIndexDao
    abstract fun operations(): OperationDao
    abstract fun smartInbox(): SmartInboxDao
    abstract fun exclusions(): ExclusionDao
    abstract fun protectedPaths(): ProtectedPathDao
    abstract fun watchedFolders(): WatchedFolderDao
    abstract fun settings(): SettingsDao
    abstract fun jobs(): JobDao
    abstract fun uriGrants(): UriGrantDao
    abstract fun aiAudit(): AiAuditDao
    abstract fun aiUsage(): AiUsageDao

    companion object {
        /** v1 → v2: add the ai_usage ledger table. */
        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS `ai_usage` (
                        `requestHash` TEXT NOT NULL PRIMARY KEY,
                        `model` TEXT NOT NULL,
                        `purpose` TEXT NOT NULL,
                        `promptTokens` INTEGER NOT NULL,
                        `completionTokens` INTEGER NOT NULL,
                        `outcome` TEXT NOT NULL,
                        `at` INTEGER NOT NULL
                    )
                    """.trimIndent()
                )
                db.execSQL("CREATE INDEX IF NOT EXISTS `index_ai_usage_at` ON `ai_usage` (`at`)")
            }
        }

        fun build(context: Context, name: String = "filemind.db"): FileMindDatabase =
            Room.databaseBuilder(context, FileMindDatabase::class.java, name)
                .addMigrations(MIGRATION_1_2)
                .build()
    }
}

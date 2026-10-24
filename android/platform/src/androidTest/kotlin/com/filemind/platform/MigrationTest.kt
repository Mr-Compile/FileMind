package com.filemind.platform

import androidx.room.Room
import androidx.room.testing.MigrationTestHelper
import androidx.sqlite.db.framework.FrameworkSQLiteOpenHelperFactory
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.filemind.platform.db.FileMindDatabase
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumented: proves the v1 schema opens, MIGRATION_1_2 upgrades it, and the
 * ai_usage ledger round-trips. Requires a device/emulator — documented as
 * runnable evidence, not run in CI-less environments.
 */
@RunWith(AndroidJUnit4::class)
class MigrationTest {

    private val dbName = "migration-test"

    @get:Rule
    val helper = MigrationTestHelper(
        InstrumentationRegistry.getInstrumentation(),
        FileMindDatabase::class.java,
        emptyList(),
        FrameworkSQLiteOpenHelperFactory())

    @Test
    fun migrate1To2_createsAiUsageTable() {
        // Create the v1 schema (all baseline tables, no ai_usage).
        helper.createDatabase(dbName, 1).apply {
            execSQL("INSERT INTO rules (id, name, enabled, priority, mode, schemaVersion, payload) " +
                "VALUES ('r1', 'r', 1, 0, 'all', 1, '{}')")
            close()
        }

        val db = helper.runMigrationsAndValidate(dbName, 2, true,
            FileMindDatabase.MIGRATION_1_2)

        // ai_usage exists and accepts rows.
        db.execSQL("INSERT INTO ai_usage (requestHash, model, purpose, promptTokens, " +
            "completionTokens, outcome, at) VALUES ('h1', 'm', 'p', 1, 2, 'ok', 0)")
        db.query("SELECT requestHash FROM ai_usage").use { c ->
            assertTrue(c.moveToFirst())
            assertEquals("h1", c.getString(0))
        }

        // Baseline data survived the migration.
        db.query("SELECT id FROM rules").use { c ->
            assertTrue(c.moveToFirst())
            assertEquals("r1", c.getString(0))
        }
        db.close()
    }

    @Test
    fun currentSchema_opensAndWritesOperation() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val db = Room.inMemoryDatabaseBuilder(context, FileMindDatabase::class.java).build()
        runBlockingIo {
            db.operations().upsert(
                com.filemind.platform.db.OperationEntity(
                    operationId = "op1", planId = "p1", status = "Planned",
                    createdAt = 0, committedAt = null, undoOfOperationId = null,
                    payload = "{}"))
            assertEquals("op1", db.operations().byId("op1")!!.operationId)
        }
        db.close()
    }

    private fun runBlockingIo(block: suspend () -> Unit) =
        kotlinx.coroutines.runBlocking { block() }
}

package com.filemind.platform

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.filemind.core.contracts.OperationStatus
import com.filemind.platform.db.FileMindDatabase
import com.filemind.platform.fs.AndroidFileSystemAdapter
import com.filemind.platform.grants.UriGrantStore
import com.filemind.platform.uri.AndroidUris.UriKind
import com.filemind.platform.uri.AndroidUris
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * Instrumented adapter checks that are real on-device: app-private file://
 * paths exercise enumerate/stat/mutate/verify end-to-end without a SAF grant,
 * and denied-by-default behavior is asserted for ungranted SAF URIs.
 */
@RunWith(AndroidJUnit4::class)
class GrantAndAdapterTest {

    private val context get() = InstrumentationRegistry.getInstrumentation().targetContext

    private fun services() = FileMindDatabase.Companion
        .let { androidx.room.Room.inMemoryDatabaseBuilder(context, FileMindDatabase::class.java).build() }
        .let { db -> db to UriGrantStore(context, db) }

    @Test
    fun fileScheme_enumerateStatMutateVerify_roundTrip() = runBlocking {
        val (db, grants) = services()
        val fs = AndroidFileSystemAdapter(context, grants)
        val dir = File(context.cacheDir, "fm-parity-${System.nanoTime()}")
        dir.mkdirs()
        File(dir, "a.txt").writeText("hello")
        File(dir, "b.bin").writeBytes(byteArrayOf(1, 2, 3))

        val entries = fs.enumerate("file://${dir.absolutePath}", recursive = true).toList()
        assertEquals(2, entries.size)

        val src = "file://${dir.absolutePath}/a.txt"
        val dst = "file://${dir.absolutePath}/sub/a-renamed.txt"
        val stat = fs.stat(src)
        assertTrue(stat.isSuccess)
        assertFalse(stat.value!!.isDirectory)

        val moved = fs.move(src, dst, com.filemind.core.contracts.ConflictPolicy.KeepBoth)
        assertTrue(moved.isSuccess)
        assertTrue(fs.exists(dst).value == true)
        assertTrue(fs.exists(src).value == false)

        // Grant-less SAF URI is denied by default.
        val safStat = fs.stat(
            "content://com.android.externalstorage.documents/document/primary%3ADocuments%2Fx")
        assertFalse(safStat.isSuccess)
        assertEquals(com.filemind.core.errors.ErrorCode.PermissionDenied, safStat.error!!.code)

        dir.deleteRecursively()
        db.close()
    }

    @Test
    fun ungrantedSaf_enumerateFails() = runBlocking {
        val (db, grants) = services()
        val fs = AndroidFileSystemAdapter(context, grants)
        val uri = "content://com.android.externalstorage.documents/tree/primary%3ADocuments"
        val entries = runCatching {
            fs.enumerate(uri, true).toList()
        }
        assertTrue(entries.isFailure || entries.getOrNull()!!.isEmpty())
        db.close()
    }
}

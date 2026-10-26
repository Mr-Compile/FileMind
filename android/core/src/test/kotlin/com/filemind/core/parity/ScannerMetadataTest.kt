package com.filemind.core.parity

import com.filemind.core.app.FileScanner
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.runBlocking
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

/**
 * FileEntry.displayName/mimeType (provider metadata) must win over
 * URI-derived names — SAF/MediaStore docIds can be opaque ("msf:1234"),
 * which previously produced extension "" + mime null → every file NoOp
 * → "Plan has no items". Mirrors ScannerMetadataTests.cs.
 */
class ScannerMetadataTest {

    @Test
    fun `provider displayName and mime beat opaque uri`() = runBlocking {
        val fs = FakeFileSystemAdapter()
        fs.seedFile("root/msf:1000001234",
            displayName = "photo.jpg", mimeType = "image/heic")
        val scanner = FileScanner(fs, FixturePathNormalizer())

        val candidates = scanner.scan("root").toList()

        assertEquals(1, candidates.size)
        val file = candidates.single()
        assertEquals("photo.jpg", file.displayName)
        assertEquals("jpg", file.extension)
        assertEquals("image/heic", file.mimeType)
    }

    @Test
    fun `uri-derived name still used when provider metadata absent`() = runBlocking {
        val fs = FakeFileSystemAdapter()
        fs.seedFile("root/report.pdf")
        val scanner = FileScanner(fs, FixturePathNormalizer())

        val candidates = scanner.scan("root").toList()

        val file = candidates.single()
        assertEquals("report.pdf", file.displayName)
        assertEquals("pdf", file.extension)
        assertEquals("application/pdf", file.mimeType)
    }
}

package com.filemind.platform

import com.filemind.platform.uri.AndroidCapabilityModel
import com.filemind.platform.uri.AndroidUris
import com.filemind.platform.uri.AndroidUris.UriKind
import com.filemind.platform.uri.GrantCoverage
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class UriModelTest {

    private val tree = "content://com.android.externalstorage.documents/tree/primary%3ADocuments"
    private val doc = "content://com.android.externalstorage.documents/document/primary%3ADocuments%2Freport.pdf"
    private val mediaRow = "content://media/external/file/1234"
    private val mediaFolder = "mediastore://external/Download"

    @Test
    fun `classify SAF tree and document URIs`() {
        val t = AndroidUris.classify(tree)
        assertEquals(UriKind.SafTree, t.kind)
        assertEquals("com.android.externalstorage.documents", t.authority)
        assertEquals("primary:Documents", t.docId)
        assertEquals("primary", t.volume)
        assertEquals("Documents", t.relPath)

        val d = AndroidUris.classify(doc)
        assertEquals(UriKind.SafDocument, d.kind)
        assertEquals("primary:Documents/report.pdf", d.docId)
        assertEquals("Documents/report.pdf", d.relPath)
    }

    @Test
    fun `classify media store row and folder`() {
        val r = AndroidUris.classify(mediaRow)
        assertEquals(UriKind.MediaStoreRow, r.kind)
        assertEquals("external", r.volume)

        val f = AndroidUris.classify(mediaFolder)
        assertEquals(UriKind.MediaStoreFolder, f.kind)
        assertEquals("external", f.volume)
        assertEquals("Download", f.relPath)
    }

    @Test
    fun `classify app file and unknown provider`() {
        val f = AndroidUris.classify("file:///data/data/com.filemind/files/a.txt")
        assertEquals(UriKind.AppFile, f.kind)
        assertEquals("/data/data/com.filemind/files/a.txt", f.relPath)

        val o = AndroidUris.classify("content://com.other.provider/x/1")
        assertEquals(UriKind.Other, o.kind)
        assertEquals("com.other.provider", o.authority)
    }

    @Test
    fun `capabilities reflect provider model`() {
        val grantedSaf = AndroidCapabilityModel.capabilities(doc, grantHeld = true)
        assertTrue(grantedSaf.canMove && grantedSaf.canCreateFolder && grantedSaf.canRename)

        val deniedSaf = AndroidCapabilityModel.capabilities(doc, grantHeld = false)
        assertFalse(deniedSaf.canRead)

        val media = AndroidCapabilityModel.capabilities(mediaRow, grantHeld = true)
        assertTrue(media.canMove && media.canRename && media.canRead)
        assertFalse(media.canCreateFolder) // RELATIVE_PATH implicit — no mkdir

        val other = AndroidCapabilityModel.capabilities("content://com.other/x/1", grantHeld = true)
        assertTrue(other.canRead)
        assertFalse(other.canMove || other.canRename)
    }

    @Test
    fun `cross provider moves are refused`() {
        // same-provider SAF move allowed
        val doc2 = "content://com.android.externalstorage.documents/document/primary%3AArchive%2Fr.pdf"
        assertTrue(AndroidCapabilityModel.canMoveBetween(doc, doc2))
        // SAF -> MediaStore refused
        assertFalse(AndroidCapabilityModel.canMoveBetween(doc, mediaFolder))
        // SAF across different providers refused
        val otherProvider = "content://com.other.documents/document/v%3Ax%2Ff.pdf"
        assertFalse(AndroidCapabilityModel.canMoveBetween(doc, otherProvider))
        // media row -> media folder allowed (RELATIVE_PATH update)
        assertTrue(AndroidCapabilityModel.canMoveBetween(mediaRow, mediaFolder))
    }

    @Test
    fun `tree grant covers subtree documents`() {
        val grants = listOf(GrantCoverage.Grant(
            "com.android.externalstorage.documents", "primary:Documents"))
        assertTrue(GrantCoverage.isCovered(doc, grants))
        assertTrue(GrantCoverage.isCovered(
            "content://com.android.externalstorage.documents/document/primary%3ADocuments%2Fsub%2Fa.txt",
            grants))
        // Sibling root is NOT covered.
        assertFalse(GrantCoverage.isCovered(
            "content://com.android.externalstorage.documents/document/primary%3AMusic%2Fs.mp3",
            grants))
        // Other volume not covered.
        assertFalse(GrantCoverage.isCovered(
            "content://com.android.externalstorage.documents/document/abcd-1234%3ADocuments%2Fx",
            grants))
    }

    @Test
    fun `docId grammar`() {
        assertEquals("primary:Docs/a.txt",
            AndroidUris.childDocId("primary:Docs", "a.txt"))
        assertEquals("primary:Docs", AndroidUris.childDocId("primary:", "Docs"))
        assertNull(AndroidUris.docIdOf(mediaRow))
    }
}

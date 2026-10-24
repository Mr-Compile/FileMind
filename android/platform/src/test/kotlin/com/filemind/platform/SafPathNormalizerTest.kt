package com.filemind.platform

import com.filemind.platform.fs.SafPathNormalizer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SafPathNormalizerTest {

    private val paths = SafPathNormalizer()
    private val auth = "com.android.externalstorage.documents"
    private val tree = "content://$auth/tree/primary%3ADocuments"

    @Test
    fun `combine under a tree produces document URIs`() {
        val combined = paths.combine(tree, "School/IT421", "lecture.mp4")
        val expected = "content://$auth/document/primary%3ADocuments%2FSchool%2FIT421%2Flecture.mp4"
        assertEquals(expected, combined)
    }

    @Test
    fun `combine under mediastore folder keeps virtual scheme`() {
        val combined = paths.combine("mediastore://external/Download", "Work", "a.pdf")
        assertEquals("mediastore://external/Download/Work/a.pdf", combined)
    }

    @Test
    fun `fileName and directoryOf over docIds`() {
        val doc = "content://$auth/document/primary%3ADocuments%2Fsub%2Fx.txt"
        assertEquals("x.txt", paths.fileName(doc))
        assertEquals("content://$auth/document/primary%3ADocuments%2Fsub",
            paths.directoryOf(doc))
    }

    @Test
    fun `isUnder and relativeTo respect tree grants`() {
        val doc = "content://$auth/document/primary%3ADocuments%2Fsub%2Fx.txt"
        assertTrue(paths.isUnder(doc, tree))
        assertEquals("sub/x.txt", paths.relativeTo(doc, tree))

        val outside = "content://$auth/document/primary%3AMusic%2Fs.mp3"
        assertFalse(paths.isUnder(outside, tree))
        assertNull(paths.relativeTo(outside, tree))
    }

    @Test
    fun `volume root tree covers whole volume`() {
        val volTree = "content://$auth/tree/primary%3A"
        val doc = "content://$auth/document/primary%3AAnywhere%2Fdeep%2Ff.bin"
        assertTrue(paths.isUnder(doc, volTree))
        assertEquals("Anywhere/deep/f.bin", paths.relativeTo(doc, volTree))
    }

    @Test
    fun `normalize collapses duplicate separators in docIds`() {
        val doc = "content://$auth/document/primary%3ADocuments%2F%2Fa.txt"
        assertEquals("content://$auth/document/primary%3ADocuments%2Fa.txt",
            paths.normalize(doc))
    }
}

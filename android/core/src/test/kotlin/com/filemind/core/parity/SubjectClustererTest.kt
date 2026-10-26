package com.filemind.core.parity

import com.filemind.core.classification.SubjectClusterer
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class SubjectClustererTest {
    private val sut = SubjectClusterer()

    @Test
    fun sharedCourseCodeClustersAcrossNames() {
        val found = sut.detect(
            listOf("IT421_Lab3.pdf", "IT-421 notes.docx", "IT 421 lecture.mp4", "random_photo.jpg")
        )
        assertEquals(3, found["IT-421"])
        assertEquals(1, found.size)
    }

    @Test
    fun separatorVariantsNormalizeToOneGroup() {
        val found = sut.detect(listOf("CS301_a.pdf", "cs 301_b.pdf", "CS-301_c.pdf"))
        assertEquals(setOf("CS-301"), found.keys)
    }

    @Test
    fun singleOccurrenceNotACluster() {
        assertTrue(sut.detect(listOf("IT421_only.pdf", "readme.txt", "todo.docx")).isEmpty())
    }

    @Test
    fun denyListTokensDoNotCluster() {
        assertTrue(sut.detect(listOf("IMG_2024_1.jpg", "IMG_2024_2.jpg", "IMG_2024_3.jpg")).isEmpty())
    }

    @Test
    fun codeInsideLongerWordNotDetected() {
        assertTrue(sut.detect(listOf("Acceptance-2026.pdf", "Acceptance-2026b.pdf")).isEmpty())
    }

    @Test
    fun codeInReturnsDetectedCodeOrNull() {
        val detected = setOf("IT-421")
        assertEquals("IT-421", sut.codeIn("IT_421_final.pdf", detected))
        assertNull(sut.codeIn("IT_422_final.pdf", detected))
        assertNull(sut.codeIn("plain_document.pdf", detected))
    }
}

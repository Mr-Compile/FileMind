package com.filemind.core.parity

import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.ClassifierSource
import com.filemind.core.contracts.Decision
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileKind
import com.filemind.core.contracts.SourceScope
import com.filemind.core.operations.ActionPlanner
import com.filemind.core.operations.PlanContext
import java.time.Instant
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** Mirrors PlannerTests.cs — {SubKind}/{KindFolder} tokens + idempotent skip. */
class PlannerTokensTest {

    private val paths = FixturePathNormalizer()
    private val now = Instant.parse("2026-01-15T10:00:00Z")
    private val planner = ActionPlanner()

    private fun candidate(path: String): FileCandidate {
        val name = path.substringAfterLast('/')
        return FileCandidate(
            scanId = "t-${path.hashCode().toString(16)}",
            sourceUri = path, displayName = name,
            extension = name.substringAfterLast('.', ""),
            mimeType = null, sizeBytes = 1024,
            createdAt = now, modifiedAt = now,
            sourceScope = SourceScope.Other)
    }

    private fun classified(subject: String? = null, kind: FileKind = FileKind.Document) =
        ClassificationResult(
            fileKind = kind, subject = subject,
            collection = if (subject != null) "School" else null,
            category = "General", subcategory = null, project = null,
            evidence = emptyList(), confidence = 80.0,
            sources = listOf(ClassifierSource.Local),
            explanation = "test", warnings = emptyList(), needsReview = false)

    private fun decide(c: FileCandidate, r: ClassificationResult, dest: String) =
        Decision(DecisionKind.Organize, c, r, destinationTemplate = dest)

    @Test
    fun `subKind buckets from stem tokens`() {
        mapOf(
            "IT421_hw1.pdf" to "Assignments",
            "IT421_assignment2.pdf" to "Assignments",
            "IT421_lab3.pdf" to "Labs",
            "IT421_quiz_1.docx" to "Activities",
            "IT421_reviewer.pdf" to "Notes",
            "IT421_capstone.docx" to "Projects",
            "IT421_random.pdf" to "Documents"
        ).forEach { (name, expected) ->
            val plan = planner.plan(
                decide(candidate("dl/$name"), classified("IT421"),
                    "School/{Subject}/{SubKind}"),
                PlanContext(paths, now = now))
            assertTrue(plan.isSuccess, plan.error?.message)
            assertEquals("School\\IT421\\$expected\\$name",
                plan.value!!.items.single().destination)
        }
    }

    @Test
    fun `kindFolder plural map`() {
        mapOf(
            FileKind.Document to "Documents", FileKind.Spreadsheet to "Documents",
            FileKind.Image to "Images", FileKind.Video to "Videos",
            FileKind.Audio to "Music", FileKind.Archive to "Archives",
            FileKind.Code to "Other"
        ).forEach { (kind, expected) ->
            val plan = planner.plan(
                decide(candidate("dl/x.bin"), classified(kind = kind), "{KindFolder}"),
                PlanContext(paths, now = now))
            assertEquals("$expected\\x.bin", plan.value!!.items.single().destination)
        }
    }

    @Test
    fun `already at destination emits no item`() {
        val ctx = PlanContext(paths,
            destinationBase = { rel -> paths.combine("dl", rel) }, now = now)
        val plan = planner.plan(
            decide(candidate("dl/Documents/x.pdf"), classified(), "{KindFolder}"), ctx)
        assertTrue(plan.isSuccess)
        assertTrue(plan.value!!.items.isEmpty())
    }
}

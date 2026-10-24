package com.filemind.core.parity

import com.filemind.core.app.ExclusionMatcher
import com.filemind.core.app.MimeMap
import com.filemind.core.classification.ClassifierEngine
import com.filemind.core.classification.ConfidenceEngine
import com.filemind.core.classification.FileKindMap
import com.filemind.core.classification.InMemoryProjectCatalog
import com.filemind.core.classification.InMemorySubjectCatalog
import com.filemind.core.classification.ProjectEntry
import com.filemind.core.classification.ProjectMatcher
import com.filemind.core.classification.SubjectEntry
import com.filemind.core.classification.SubjectMatcher
import com.filemind.core.contracts.ConfidenceBand
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileKind
import com.filemind.core.contracts.SourceScope
import com.filemind.core.contracts.utcTicks
import com.filemind.core.operations.ActionPlanner
import com.filemind.core.operations.PlanContext
import java.time.Instant
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.fail

/**
 * Parity suite over tests/fixtures/manifest.json — the same expectations are
 * asserted on Windows by FileMind.Domain.Tests/ManifestParityTests.cs.
 */
class ManifestParityTest {

    private val manifest: ManifestDoc = ParityJson.load("manifest.json")

    private val classifier = ClassifierEngine(
        SubjectMatcher(InMemorySubjectCatalog(manifest.catalog.subjects.map {
            SubjectEntry(it.id, it.name, it.code, it.aliases, it.keywords)
        })),
        ProjectMatcher(InMemoryProjectCatalog(manifest.catalog.projects.map {
            ProjectEntry(it.id, it.name, it.clientName, it.aliases, it.keywords)
        })),
        ConfidenceEngine()
    )

    private val exclusions = ExclusionMatcher(manifest.exclusions)
    private val paths = FixturePathNormalizer()
    private val planner = ActionPlanner()
    private val fixedTime: Instant = Instant.parse("2026-01-15T10:00:00Z")

    private fun candidateFor(case: ManifestCase): FileCandidate {
        val name = case.path.split('/', '\\').last()
        val ext = if (name.contains('.')) name.substringAfterLast('.') else ""
        return FileCandidate(
            scanId = "parity-${case.path.hashCode().toString(16)}",
            sourceUri = case.path,
            displayName = name,
            extension = ext,
            mimeType = MimeMap.fromExtension(ext),
            sizeBytes = 1024,
            createdAt = fixedTime,
            modifiedAt = fixedTime,
            sourceScope = SourceScope.Other
        )
    }

    @Test
    fun `fixture loads and has cases`() {
        assertTrue(manifest.cases.isNotEmpty(), "manifest.json has no cases")
        assertTrue(manifest.exclusions.isNotEmpty(), "manifest.json has no exclusions")
    }

    @Test
    fun `excluded cases are excluded by shared patterns`() {
        val expected = manifest.cases.filter { it.expectedExcluded == true }
        assertTrue(expected.isNotEmpty(), "no expectedExcluded cases in manifest")
        for (case in expected) {
            val name = case.path.split('/', '\\').last()
            val normalized = case.path.replace('\\', '/')
            assertTrue(
                exclusions.isExcluded(name) || exclusions.isExcluded(normalized),
                "expected excluded but was not: ${case.path}"
            )
        }
    }

    @Test
    fun `included cases are not excluded`() {
        for (case in manifest.cases.filter { it.expectedExcluded != true }) {
            val name = case.path.split('/', '\\').last()
            val normalized = case.path.replace('\\', '/')
            assertTrue(
                !exclusions.isExcluded(name) && !exclusions.isExcluded(normalized),
                "unexpectedly excluded: ${case.path}"
            )
        }
    }

    @Test
    fun `classifications match manifest expectations`() {
        for (case in manifest.cases.filter { it.expectedExcluded != true }) {
            val result = classifier.classify(candidateFor(case))
            if (case.expectedFileKind != null) {
                assertEquals(
                    FileKind.valueOf(case.expectedFileKind), result.fileKind,
                    "fileKind mismatch for ${case.path}"
                )
            }
            if (case.expectedSubject != null) {
                assertEquals(case.expectedSubject, result.subject, "subject mismatch for ${case.path}")
            }
            if (case.expectedProject != null) {
                assertEquals(case.expectedProject, result.project, "project mismatch for ${case.path}")
            }
            if (case.expectedBand != null) {
                val band = ConfidenceEngine().band(result.confidence)
                assertEquals(
                    ConfidenceBand.valueOf(case.expectedBand), band,
                    "band mismatch for ${case.path} (confidence=${result.confidence})"
                )
            }
        }
    }

    @Test
    fun `routed cases land under expected route prefix`() {
        // expectedRoute encodes collection+subject intent ("context beats type");
        // the leaf directory comes from rule templates outside manifest scope,
        // so parity asserts the first two segments only.
        for (case in manifest.cases.filter { it.expectedRoute != null }) {
            val candidate = candidateFor(case)
            val classification = classifier.classify(candidate)
            val expectedPrefix = case.expectedRoute!!.split('/').take(2).joinToString("/")
            val template = if (classification.subject != null) "School/{Subject}"
                else if (classification.project != null) "Work/{Project}"
                else "Files/{FileKind}"
            val decision = com.filemind.core.contracts.Decision(
                kind = com.filemind.core.contracts.DecisionKind.Organize,
                candidate = candidate,
                classification = classification,
                destinationTemplate = template
            )
            val plan = planner.plan(decision, PlanContext(paths, now = fixedTime))
            if (!plan.isSuccess) fail("plan failed for ${case.path}: ${plan.error!!.message}")
            val dest = plan.value!!.items.single().destination!!.replace('\\', '/')
            assertTrue(
                dest.startsWith(expectedPrefix),
                "route mismatch for ${case.path}: '$dest' does not start with '$expectedPrefix'"
            )
        }
    }

    @Test
    fun `fingerprint format matches dotnet ticks contract`() {
        // .NET ticks = 100ns since 0001-01-01; Instant.utcTicks mirrors it.
        val t = Instant.parse("2026-01-15T10:00:00Z")
        val fp = com.filemind.core.contracts.FileIdentity.computeFingerprint(1024, t)
        // Golden value verified against C# FileIdentity.ComputeFingerprint
        // (UtcTicks of 2026-01-15T10:00:00Z = 0x8de541cd86c9000).
        assertEquals("400:8de541cd86c9000", fp)
        assertTrue(fp.matches(Regex("^[0-9a-f]+:[0-9a-f]+$")), "fingerprint must be hex:hex")
    }
}

package com.filemind.core.parity

import com.filemind.core.app.DecisionMaker
import com.filemind.core.app.MimeMap
import com.filemind.core.classification.ClassifierEngine
import com.filemind.core.classification.ConfidenceEngine
import com.filemind.core.classification.InMemoryProjectCatalog
import com.filemind.core.classification.InMemorySubjectCatalog
import com.filemind.core.classification.ProjectEntry
import com.filemind.core.classification.ProjectMatcher
import com.filemind.core.classification.SubjectEntry
import com.filemind.core.classification.SubjectMatcher
import com.filemind.core.contracts.ConditionField
import com.filemind.core.contracts.ConditionGroup
import com.filemind.core.contracts.ConditionOperator
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.LogicalOperator
import com.filemind.core.contracts.Rule
import com.filemind.core.contracts.RuleAction
import com.filemind.core.contracts.RuleActionKind
import com.filemind.core.contracts.RuleCondition
import com.filemind.core.contracts.RuleProvenance
import com.filemind.core.contracts.SourceScope
import com.filemind.core.rules.RuleEngine
import com.filemind.core.rules.RuleValidator
import java.time.Instant
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.fail

/**
 * Rule-engine parity over tests/fixtures/rules-parity.json. Mirrors
 * FileMind.Domain.Tests/RuleParityTests.cs — same JSON, same expectations.
 */
class RuleParityTest {

    private val doc: RuleParityDoc = ParityJson.load("rules-parity.json")
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
    private val engine = RuleEngine()
    private val decisionMaker = DecisionMaker(ConfidenceEngine())
    private val fixedTime = Instant.parse("2026-01-15T10:00:00Z")

    private fun toRule(r: RuleJson): Rule = Rule(
        id = r.id,
        name = r.name,
        enabled = r.enabled,
        priority = r.priority,
        conditionRoot = toGroup(r.conditionRoot),
        actions = r.actions.map { a ->
            RuleAction(
                kind = RuleActionKind.valueOf(a.kind),
                destinationTemplate = a.destinationTemplate,
                namingTemplate = a.namingTemplate,
                collection = a.collection,
                tag = a.tag,
                conflictPolicy = ConflictPolicy.valueOf(a.conflictPolicy)
            )
        },
        stopProcessing = r.stopProcessing,
        mode = r.mode,
        schemaVersion = r.schemaVersion,
        provenance = RuleProvenance.valueOf(r.provenance)
    )

    private fun toGroup(g: ConditionGroupJson): ConditionGroup = ConditionGroup(
        operator = LogicalOperator.valueOf(g.operator),
        conditions = g.conditions.map {
            RuleCondition(
                field = ConditionField.valueOf(it.field),
                operator = ConditionOperator.valueOf(it.operator),
                value = it.value,
                metadataKey = it.metadataKey
            )
        },
        groups = g.groups.map { toGroup(it) }
    )

    private fun candidateFor(case: RuleParityCase): FileCandidate {
        val name = case.path.split('/', '\\').last()
        val ext = if (name.contains('.')) name.substringAfterLast('.') else ""
        return FileCandidate(
            scanId = "parity-${case.path.hashCode().toString(16)}",
            sourceUri = case.path,
            displayName = name,
            extension = ext,
            mimeType = MimeMap.fromExtension(ext),
            sizeBytes = case.sizeBytes ?: 1024,
            createdAt = fixedTime,
            modifiedAt = fixedTime,
            sourceScope = SourceScope.Other
        )
    }

    @Test
    fun `fixture rules deserialize and validate`() {
        assertTrue(doc.rules.isNotEmpty())
        for (r in doc.rules) {
            val rule = toRule(r)
            val result = RuleValidator.validate(rule)
            assertTrue(result.isSuccess, "fixture rule '${r.id}' failed validation: ${result.error?.message}")
        }
    }

    @Test
    fun `rule evaluations match expectations`() {
        val rules = doc.rules.map { toRule(it) }
        for (case in doc.cases) {
            val candidate = candidateFor(case)
            val classification = classifier.classify(candidate)
            val eval = engine.evaluate(candidate, classification, rules)

            assertEquals(
                case.expectedMatched.toSet(), eval.matchedRuleIds.toSet(),
                "matched rules differ for ${case.path}: ${eval.trace.joinToString()}"
            )
            assertEquals(
                case.expectedWinningRule, eval.winningRuleId,
                "winning rule differs for ${case.path}"
            )
            assertEquals(case.expectedConflicts, eval.conflicts.size, "conflicts differ for ${case.path}")

            if (case.expectedDecision != null) {
                val decision = decisionMaker.decide(candidate, classification, eval, emptyList())
                assertEquals(
                    DecisionKind.valueOf(case.expectedDecision), decision.kind,
                    "decision differs for ${case.path} (confidence=${classification.confidence}, reasons=${decision.reasons})"
                )
            }
        }
    }
}

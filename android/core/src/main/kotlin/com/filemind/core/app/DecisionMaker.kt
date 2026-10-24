package com.filemind.core.app

import com.filemind.core.classification.ConfidenceEngine
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.ConfidenceBand
import com.filemind.core.contracts.Decision
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.RuleActionKind
import com.filemind.core.rules.RuleEvaluation

class DecisionMaker(private val confidence: ConfidenceEngine) {
    fun decide(
        candidate: FileCandidate,
        classification: ClassificationResult,
        evaluation: RuleEvaluation,
        collections: List<CollectionEntry>
    ): Decision {
        if (evaluation.conflicts.isNotEmpty()) {
            return review(candidate, classification,
                listOf("Rule destination conflicts: " + evaluation.conflicts.joinToString("; ")))
        }

        val organize = evaluation.proposedActions.firstOrNull {
            it.kind == RuleActionKind.Move || it.kind == RuleActionKind.RouteToCollection ||
                it.kind == RuleActionKind.Rename || it.kind == RuleActionKind.Copy
        }

        if (evaluation.proposedActions.any { it.kind == RuleActionKind.Ignore }) {
            return Decision(
                kind = DecisionKind.Ignore,
                candidate = candidate,
                classification = classification,
                reasons = listOf("Matched an ignore rule.")
            )
        }

        if (evaluation.proposedActions.any { it.kind == RuleActionKind.Ask }) {
            return review(candidate, classification, listOf("Rule requires user confirmation."))
        }

        if (organize != null) {
            val destinationTemplate = organize.destinationTemplate
                ?: collectionTemplate(organize.collection, collections)
                ?: defaultTemplate(classification)
            return Decision(
                kind = DecisionKind.Organize,
                candidate = candidate,
                classification = classification,
                destinationTemplate = destinationTemplate,
                namingTemplate = organize.namingTemplate,
                conflictPolicy = organize.conflictPolicy,
                reasons = listOf("Rule '${evaluation.winningRuleId}' matched.")
            )
        }

        val band = confidence.band(classification.confidence)

        if (band == ConfidenceBand.Low) {
            return Decision(
                kind = DecisionKind.NoOp,
                candidate = candidate,
                classification = classification,
                reasons = listOf(
                    "Confidence ${"%.0f".format(classification.confidence)} below Medium threshold; left untouched.")
            )
        }

        if (band == ConfidenceBand.Medium || classification.needsReview) {
            return review(candidate, classification, listOf(
                "Confidence ${"%.0f".format(classification.confidence)} requires Smart Inbox review."))
        }

        return Decision(
            kind = DecisionKind.Organize,
            candidate = candidate,
            classification = classification,
            destinationTemplate = defaultTemplate(classification),
            reasons = listOf("High-confidence classification (${"%.0f".format(classification.confidence)}).")
        )
    }

    private fun collectionTemplate(collectionId: String?, collections: List<CollectionEntry>): String? =
        collectionId?.let { id ->
            collections.firstOrNull {
                it.id.equals(id, ignoreCase = true) || it.name.equals(id, ignoreCase = true)
            }?.destinationTemplate
        }

    private fun defaultTemplate(c: ClassificationResult): String =
        when {
            c.subject != null -> "School/{Subject}"
            c.project != null -> "Work/{Project}"
            else -> "Files/{FileKind}"
        }

    private fun review(candidate: FileCandidate, classification: ClassificationResult, reasons: List<String>) =
        Decision(
            kind = DecisionKind.Review,
            candidate = candidate,
            classification = classification,
            reasons = reasons
        )
}

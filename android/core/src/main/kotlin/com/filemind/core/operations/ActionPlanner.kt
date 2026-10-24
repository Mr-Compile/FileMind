package com.filemind.core.operations

import com.filemind.core.contracts.ActionItem
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.Decision
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.OperationType
import com.filemind.core.contracts.UndoRecipe
import com.filemind.core.errors.Result
import com.filemind.core.ports.PathNormalizer
import java.time.Instant
import java.time.ZoneOffset
import java.util.UUID

data class PlanContext(
    val paths: PathNormalizer,
    val destinationBase: ((String) -> String)? = null,
    val now: Instant? = null,
    val sourceRoot: String? = null,
    val preserveTree: Boolean = false
)

class ActionPlanner {
    fun plan(decision: Decision, context: PlanContext): Result<ActionPlan> {
        if (decision.kind == DecisionKind.NoOp || decision.kind == DecisionKind.Ignore) {
            return Result.ok(ActionPlan(
                planId = newId(),
                createdAt = context.now ?: Instant.now(),
                sourceDecision = decision.kind.name,
                items = emptyList(),
                requiresReview = false,
                dryRun = false,
                reversibleEstimate = true
            ))
        }

        if (decision.kind == DecisionKind.Review) {
            return Result.ok(ActionPlan(
                planId = newId(),
                createdAt = context.now ?: Instant.now(),
                sourceDecision = "Review",
                items = listOf(ActionItem(
                    operation = OperationType.Ask,
                    source = decision.candidate.sourceUri,
                    reason = decision.reasons.ifEmpty { listOf("Low confidence or conflicts") }.joinToString("; "),
                    conflictPolicy = ConflictPolicy.Ask
                )),
                requiresReview = true,
                dryRun = false,
                reversibleEstimate = true
            ))
        }

        val relDir = relativeDirOf(decision.candidate, context)
        var destination = resolveTemplate(
            decision.destinationTemplate ?: "{Collection}/{FileKind}",
            decision.candidate, decision.classification, context, relDir)
        // Category-then-tree: source-relative directory appended inside the
        // resolved category template, before destinationBase prepends the root.
        if (context.preserveTree && relDir.isNotEmpty()) {
            destination = context.paths.combine(destination, relDir)
        }
        context.destinationBase?.let { destination = it(destination) }
        val newName = if (decision.namingTemplate != null) {
            resolveTemplate(decision.namingTemplate, decision.candidate, decision.classification, context, "")
        } else {
            decision.candidate.displayName
        }

        val destPath = context.paths.combine(destination, newName)
        val reason = decision.reasons.ifEmpty { listOf(decision.classification.explanation) }.joinToString("; ")

        val item = ActionItem(
            operation = OperationType.Move,
            source = decision.candidate.sourceUri,
            destination = destPath,
            conflictPolicy = decision.conflictPolicy,
            reason = reason,
            preconditions = listOf(
                "source-exists:${decision.candidate.sourceUri}",
                "fingerprint:${FileIdentity.computeFingerprint(decision.candidate.sizeBytes, decision.candidate.modifiedAt)}"
            ),
            undoRecipe = UndoRecipe(
                OperationType.Move,
                destPath,
                decision.candidate.sourceUri,
                FileIdentity.computeFingerprint(decision.candidate.sizeBytes, decision.candidate.modifiedAt),
                listOf("exists:$destPath")
            )
        )

        return Result.ok(ActionPlan(
            planId = newId(),
            createdAt = context.now ?: Instant.now(),
            sourceDecision = decision.kind.name,
            items = listOf(item),
            requiresReview = decision.classification.needsReview,
            dryRun = false,
            reversibleEstimate = true
        ))
    }

    fun applyTemplates(plan: ActionPlan, context: PlanContext): ActionPlan = plan

    /** Source directory relative to sourceRoot; "" when unknown/unset. */
    private fun relativeDirOf(c: FileCandidate, ctx: PlanContext): String {
        if (ctx.sourceRoot == null) return ""
        val dir = ctx.paths.directoryOf(c.sourceUri) ?: return ""
        val rel = ctx.paths.relativeTo(dir, ctx.sourceRoot)
        return if (rel == null || rel == "" || rel == ".") "" else rel
    }

    private fun resolveTemplate(
        template: String, c: FileCandidate, r: ClassificationResult, ctx: PlanContext, relativeDir: String
    ): String {
        val now = (ctx.now ?: Instant.now()).atOffset(ZoneOffset.UTC)
        return template
            .replace("{Collection}", r.collection ?: "Unsorted")
            .replace("{Category}", r.category ?: "General")
            .replace("{Subcategory}", r.subcategory ?: "General")
            .replace("{Subject}", r.subject ?: "Unsorted")
            .replace("{Project}", r.project ?: "General")
            .replace("{FileKind}", r.fileKind.name)
            .replace("{Name}", stemOf(ctx.paths.fileName(c.displayName)))
            .replace("{Ext}", c.extension.trimStart('.'))
            .replace("{YYYY}", "%04d".format(now.year))
            .replace("{MM}", "%02d".format(now.monthValue))
            .replace("{DD}", "%02d".format(now.dayOfMonth))
            // Directory token: naming templates pass "" — embedding path
            // separators in a file name is never valid.
            .replace("{RelativeDir}", relativeDir)
            // Extraction tokens: populated only when local intelligence wrote
            // reliable metadata — absence falls back to neutral values.
            .replace("{Author}", metaOr(c, "author", "Unknown"))
            .replace("{Title}", metaOr(c, "title", stemOf(ctx.paths.fileName(c.displayName))))
            .replace("{Year}", metaOr(c, "year", "%04d".format(now.year)))
    }

    private fun metaOr(c: FileCandidate, key: String, fallback: String): String =
        c.metadata[key]?.takeIf { it.isNotBlank() } ?: fallback

    private fun stemOf(fileName: String): String {
        val dot = fileName.lastIndexOf('.')
        return if (dot > 0) fileName.substring(0, dot) else fileName
    }

    private fun newId(): String = UUID.randomUUID().toString().replace("-", "")
}

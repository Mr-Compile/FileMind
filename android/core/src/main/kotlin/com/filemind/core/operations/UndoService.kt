package com.filemind.core.operations

import com.filemind.core.contracts.ActionItem
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OperationStatus
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.FileSystemAdapter
import java.time.Instant

class UndoService(private val fs: FileSystemAdapter) {
    suspend fun buildReversePlan(record: OperationRecord): Result<ActionPlan> {
        // Undo what actually moved. NeedsAttention/Failed runs can still hold
        // committed items — partial results must be reversible too.
        val committed = record.items.filter {
            it.status == OperationStatus.Committed && it.undoRecipe != null
        }
        if (committed.isEmpty()) {
            return Result.fail(FileMindError.validation(
                "Operation ${record.operationId} has no committed items to undo (status=${record.status})."))
        }

        val items = mutableListOf<ActionItem>()
        val blockers = mutableListOf<String>()
        var needsReview = false

        for (item in committed) {
            if (item.undoRecipe == null) {
                needsReview = true
                blockers.add("Item '${item.itemId}' has no undo recipe; manual review required.")
                continue
            }

            val recipe = item.undoRecipe
            val current = fs.stat(recipe.reverseSource)
            if (!current.isSuccess) {
                needsReview = true
                blockers.add("'${recipe.reverseSource}': ${current.error!!.message}")
                continue
            }

            val fingerprint = FileIdentity.computeFingerprint(current.value!!.sizeBytes, current.value.modifiedAt)
            if (fingerprint != recipe.fingerprint) {
                needsReview = true
                blockers.add("'${recipe.reverseSource}' changed since the operation; refusing blind overwrite.")
                continue
            }

            items.add(ActionItem(
                operation = recipe.reverseOperation,
                source = recipe.reverseSource,
                destination = recipe.reverseDestination,
                conflictPolicy = ConflictPolicy.Ask,
                reason = "Undo of ${record.operationId} item ${item.itemId}",
                preconditions = recipe.preconditions
            ))
        }

        if (items.isEmpty()) {
            return Result.fail(FileMindError.validation(
                "No reversible items in operation ${record.operationId}. ${blockers.joinToString("; ")}"))
        }

        return Result.ok(ActionPlan(
            planId = "undo-${record.operationId}",
            createdAt = Instant.now(),
            sourceDecision = "Undo:${record.operationId}",
            items = items,
            requiresReview = needsReview,
            dryRun = false,
            reversibleEstimate = false
        ))
    }
}

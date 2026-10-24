package com.filemind.core.operations

import com.filemind.core.contracts.ActionItem
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.OperationItemState
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OperationStatus
import com.filemind.core.contracts.OperationType
import com.filemind.core.errors.ErrorCode
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.OperationStore
import com.filemind.core.safety.SafetyContext
import com.filemind.core.safety.SafetyEngine
import java.time.Instant
import java.util.UUID
import kotlin.coroutines.coroutineContext
import kotlinx.coroutines.ensureActive

class TransactionEngine(
    private val fs: FileSystemAdapter,
    private val store: OperationStore,
    private val safety: SafetyEngine
) {
    suspend fun execute(plan: ActionPlan, context: SafetyContext): Result<OperationRecord> {
        var record = OperationRecord(
            operationId = UUID.randomUUID().toString().replace("-", ""),
            planId = plan.planId,
            status = OperationStatus.Planned,
            createdAt = Instant.now(),
            items = plan.items.mapIndexed { i, item ->
                OperationItemState(
                    itemId = "${plan.planId}-$i",
                    operation = item.operation,
                    originalLocation = item.source,
                    status = OperationStatus.Planned,
                    undoRecipe = item.undoRecipe
                )
            }
        )

        val saved = store.save(record)
        if (!saved.isSuccess) return Result.fail(saved.error!!)
        record = saved.value!!

        record = transition(record, OperationStatus.Validating)

        val safetyResult = safety.validate(plan, context)
        if (!safetyResult.allowed) {
            record = failRecord(record,
                FileMindError.validation("Safety blocked: ${safetyResult.blockers.joinToString("; ")}"))
            return Result.ok(record)
        }

        record = transition(record, OperationStatus.Executing)
        val items = record.items.toMutableList()

        for (i in plan.items.indices) {
            coroutineContext.ensureActive()
            val item = plan.items[i]
            val itemState = items[i]
            val executed = executeItem(item)

            items[i] = if (executed.isSuccess) {
                itemState.copy(status = OperationStatus.Committed, resultingLocation = executed.value)
            } else {
                itemState.copy(status = OperationStatus.Failed, error = executed.error)
            }

            record = record.copy(items = items)
            store.save(record)
        }

        record = transition(record, OperationStatus.Verifying)

        for (i in items.indices) {
            val state = items[i]
            val planItem = plan.items[i]
            if (state.status != OperationStatus.Committed || state.resultingLocation == null) continue

            val fingerprint = planItem.undoRecipe?.fingerprint
            val verify = if (fingerprint != null) {
                fs.verify(state.resultingLocation, fingerprint)
            } else {
                fs.exists(state.resultingLocation)
            }

            items[i] = if (verify.isSuccess && verify.value == true) {
                state.copy(verification = "verified:${fingerprint ?: "exists"}")
            } else {
                state.copy(
                    status = OperationStatus.NeedsAttention,
                    verification = "failed",
                    error = verify.error ?: FileMindError.verification("Post-operation verification failed.")
                )
            }
        }

        val anyFailed = items.any { it.status == OperationStatus.Failed || it.status == OperationStatus.NeedsAttention }
        record = record.copy(items = items)
        record = if (anyFailed) {
            transition(record, OperationStatus.NeedsAttention)
        } else {
            transition(record, OperationStatus.Committed) { it.copy(committedAt = Instant.now()) }
        }

        return Result.ok(record)
    }

    private suspend fun executeItem(item: ActionItem): Result<String> =
        when (item.operation) {
            OperationType.Move -> fs.move(item.source, item.destination!!, item.conflictPolicy)
            OperationType.Copy -> fs.copy(item.source, item.destination!!, item.conflictPolicy)
            OperationType.Rename -> fs.rename(item.source, item.destination!!, item.conflictPolicy)
            OperationType.CreateFolder -> toPathResult(fs.createDirectory(item.destination!!), item.destination!!)
            OperationType.Ignore, OperationType.Ask, OperationType.Notify, OperationType.Tag ->
                Result.ok(item.source)
            else -> Result.fail(FileMindError(ErrorCode.UnsupportedOperation,
                "Operation ${item.operation} not supported.", retrySafe = false, requiresReview = true))
        }

    private fun toPathResult(result: Result<Boolean>, path: String): Result<String> =
        if (result.isSuccess) Result.ok(path) else Result.fail(result.error!!)

    private suspend fun transition(
        record: OperationRecord, status: OperationStatus,
        mutate: (OperationRecord) -> OperationRecord = { it }
    ): OperationRecord {
        val next = mutate(record.copy(status = status))
        val saved = store.save(next)
        return if (saved.isSuccess) saved.value!! else next
    }

    private suspend fun failRecord(record: OperationRecord, error: FileMindError): OperationRecord {
        val failed = record.copy(status = OperationStatus.Failed, error = error)
        val saved = store.save(failed)
        return if (saved.isSuccess) saved.value!! else failed
    }
}

package com.filemind.core.operations

import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.OperationItemState
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OperationStatus
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.OperationStore

class RollbackService(
    private val fs: FileSystemAdapter,
    private val store: OperationStore
) {
    suspend fun recover(): Result<List<OperationRecord>> {
        val incomplete = store.loadIncomplete()
        if (!incomplete.isSuccess) return Result.fail(incomplete.error!!)

        val results = mutableListOf<OperationRecord>()
        for (record in incomplete.value!!) {
            results.add(recoverOne(record))
        }
        return Result.ok(results)
    }

    private suspend fun recoverOne(record: OperationRecord): OperationRecord {
        val reconciled = reconcile(record)
        val saved = store.save(reconciled)
        return if (saved.isSuccess) saved.value!! else reconciled
    }

    private suspend fun reconcile(record: OperationRecord): OperationRecord {
        val items = mutableListOf<OperationItemState>()
        var anyAttention = false

        for (item in record.items) {
            if (item.status == OperationStatus.Committed) {
                items.add(item)
                continue
            }

            val resulting = item.resultingLocation
            if (resulting != null) {
                val destExists = fs.exists(resulting)
                if (destExists.isSuccess && destExists.value == true) {
                    val stat = fs.stat(resulting)
                    val fingerprint = item.undoRecipe?.fingerprint
                    val actual = if (stat.isSuccess) {
                        FileIdentity.computeFingerprint(stat.value!!.sizeBytes, stat.value.modifiedAt)
                    } else null

                    if (fingerprint != null && actual == fingerprint) {
                        items.add(item.copy(status = OperationStatus.Committed, verification = "recovered:verified"))
                        continue
                    }

                    items.add(item.copy(
                        status = OperationStatus.NeedsAttention,
                        error = FileMindError.verification("Recovered result exists but fingerprint differs.")
                    ))
                    anyAttention = true
                    continue
                }
            }

            val sourceExists = fs.exists(item.originalLocation)
            if (sourceExists.isSuccess && sourceExists.value == true) {
                items.add(item.copy(status = OperationStatus.RolledBack, verification = "recovered:source-intact"))
                continue
            }

            items.add(item.copy(
                status = OperationStatus.NeedsAttention,
                error = FileMindError.verification("Neither source nor result verifiable after crash.")
            ))
            anyAttention = true
        }

        val status = if (anyAttention) {
            OperationStatus.NeedsAttention
        } else if (items.all { it.status == OperationStatus.Committed }) {
            OperationStatus.Committed
        } else {
            OperationStatus.RolledBack
        }

        return record.copy(items = items, status = status)
    }
}

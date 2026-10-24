package com.filemind.core.safety

import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.OperationType
import com.filemind.core.contracts.PermissionCapabilities
import com.filemind.core.contracts.SafetyResult
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.PathNormalizer
import com.filemind.core.ports.ProtectedPathProvider

data class SafetyContext(
    val protectedPaths: ProtectedPathProvider,
    val capabilityOverrides: Map<String, PermissionCapabilities>? = null,
    val freeSpaceOverrides: Map<String, Long>? = null
)

class SafetyEngine(
    private val fs: FileSystemAdapter,
    private val paths: PathNormalizer
) {
    suspend fun validate(plan: ActionPlan, context: SafetyContext): SafetyResult {
        val blockers = mutableListOf<String>()
        val warnings = mutableListOf<String>()
        var confirmation: String? = null

        if (plan.items.isEmpty()) blockers.add("Plan has no items.")

        val itemsNeedingConfirmation = mutableListOf<String>()

        for (item in plan.items) {
            if (!item.isValid) {
                blockers.add("Item '${item.source}': ${item.operation} requires a destination.")
                continue
            }

            val caps = context.capabilityOverrides?.get(item.source)

            if (context.protectedPaths.isProtected(paths.normalize(item.source))) {
                blockers.add("'${item.source}': source is a protected path.")
            }
            if (item.destination != null && context.protectedPaths.isProtected(paths.normalize(item.destination))) {
                blockers.add("'${item.destination}': destination is a protected path.")
            }

            if (item.operation == OperationType.Move || item.operation == OperationType.Copy ||
                item.operation == OperationType.Rename || item.operation == OperationType.CreateFolder
            ) {
                val stat = fs.stat(item.source)
                if (!stat.isSuccess && item.operation != OperationType.CreateFolder) {
                    blockers.add("'${item.source}': ${stat.error!!.message}")
                }
            }

            if (item.destination != null && item.operation != OperationType.CreateFolder) {
                val destExists = fs.exists(item.destination)
                if (destExists.isSuccess && destExists.value == true) {
                    when (item.conflictPolicy) {
                        ConflictPolicy.KeepBoth ->
                            warnings.add("'${item.destination}' exists; will keep both with deterministic suffix.")
                        ConflictPolicy.Skip ->
                            warnings.add("'${item.destination}' exists; item will be skipped.")
                        ConflictPolicy.Ask ->
                            itemsNeedingConfirmation.add("'${item.destination}' exists; overwrite decision required.")
                        ConflictPolicy.ReplaceWithConfirmation ->
                            itemsNeedingConfirmation.add("'${item.destination}' will be replaced; confirmation required.")
                    }
                }

                val free = context.freeSpaceOverrides?.get(item.destination)
                val stat2 = fs.stat(item.source)
                if (free != null && stat2.isSuccess && free < stat2.value!!.sizeBytes) {
                    blockers.add("'${item.destination}': insufficient free space ($free < ${stat2.value.sizeBytes}).")
                }
            }

            if (caps != null) {
                if ((item.operation == OperationType.Move || item.operation == OperationType.Rename) && !caps.canMove) {
                    blockers.add("'${item.source}': move not permitted by capabilities.")
                }
                if (item.operation == OperationType.Copy && !caps.canCopy) {
                    blockers.add("'${item.source}': copy not permitted by capabilities.")
                }
            }
        }

        if (itemsNeedingConfirmation.isNotEmpty()) {
            confirmation = itemsNeedingConfirmation.joinToString(" ")
        }

        return SafetyResult(
            allowed = blockers.isEmpty(),
            blockers = blockers,
            warnings = warnings,
            requiredConfirmation = confirmation
        )
    }
}

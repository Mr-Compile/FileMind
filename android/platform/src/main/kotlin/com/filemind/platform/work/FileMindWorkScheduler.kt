package com.filemind.platform.work

import android.content.Context
import androidx.work.Constraints
import androidx.work.Data
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.FileMindJson
import kotlinx.serialization.encodeToString

/**
 * Enqueues FileMind work. Unique-work names give idempotency — a second
 * scan request for the same root replaces (KEEP = don't restart a healthy
 * run), and organize plans are one-shot executions.
 */
class FileMindWorkScheduler(context: Context) {

    private val wm = WorkManager.getInstance(context)

    private val constraints = Constraints.Builder()
        .setRequiresBatteryNotLow(true)
        .setRequiresStorageNotLow(true)
        .setRequiredNetworkType(NetworkType.NOT_REQUIRED) // fully offline
        .build()

    fun enqueueScan(rootUri: String, recursive: Boolean = true) {
        val request = OneTimeWorkRequestBuilder<ScanWorker>()
            .setInputData(Data.Builder()
                .putString(WorkContract.IN_ROOT_URI, rootUri)
                .putBoolean(WorkContract.IN_RECURSIVE, recursive)
                .build())
            .setConstraints(constraints)
            .addTag(WorkContract.TAG_SCAN)
            .build()
        wm.enqueueUniqueWork("$WORK_SCAN_PREFIX$rootUri", ExistingWorkPolicy.KEEP, request)
    }

    fun enqueueOrganize(plan: ActionPlan, dryRun: Boolean = false) {
        val request = OneTimeWorkRequestBuilder<OrganizeWorker>()
            .setInputData(Data.Builder()
                .putString(WorkContract.IN_PLAN_JSON, FileMindJson.encodeToString(plan))
                .putBoolean(WorkContract.IN_DRY_RUN, dryRun)
                .build())
            .setConstraints(constraints)
            .addTag(WorkContract.TAG_ORGANIZE)
            .build()
        wm.enqueueUniqueWork("$WORK_ORGANIZE_PREFIX${plan.planId}",
            ExistingWorkPolicy.REPLACE, request)
    }

    /** Cancellation is cooperative: workers check ensureActive/isStopped. */
    fun cancelScan(rootUri: String) = wm.cancelUniqueWork("$WORK_SCAN_PREFIX$rootUri")
    fun cancelOrganize(planId: String) = wm.cancelUniqueWork("$WORK_ORGANIZE_PREFIX$planId")
    fun cancelAll() = wm.cancelAllWork()

    companion object {
        private const val WORK_SCAN_PREFIX = "scan:"
        private const val WORK_ORGANIZE_PREFIX = "organize:"
    }
}

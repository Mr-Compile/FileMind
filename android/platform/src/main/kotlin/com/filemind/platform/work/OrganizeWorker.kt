package com.filemind.platform.work

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.Data
import androidx.work.ForegroundInfo
import androidx.work.WorkerParameters
import com.filemind.core.app.Notification
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.FileMindJson
import com.filemind.core.contracts.OperationStatus
import com.filemind.platform.FileMindAndroid
import kotlinx.coroutines.CancellationException

/**
 * Executes a serialized ActionPlan through the SafetyEngine +
 * TransactionEngine. Cancellation between items is honored by the engine's
 * ensureActive checks; the OperationRecord reflects exactly which items
 * committed, so a cancelled run leaves a NeedsAttention record, not a lie.
 */
class OrganizeWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    override suspend fun getForegroundInfo(): ForegroundInfo {
        val n = FileMindAndroid.from(applicationContext).notifications
            .progress("FileMind organize", "Organizing files…", id.toString())
        return ForegroundInfo(id.hashCode(), n)
    }

    override suspend fun doWork(): Result { return try {
        val planJson = inputData.getString(WorkContract.IN_PLAN_JSON)
            ?: return failure("missing ${WorkContract.IN_PLAN_JSON}")
        val plan = FileMindJson.decodeFromString(ActionPlan.serializer(), planJson)

        val services = FileMindAndroid.from(applicationContext)
        services.grantStore.revalidate()
        val pipeline = services.pipeline()

        val outcome = if (inputData.getBoolean(WorkContract.IN_DRY_RUN, false)) {
            val check = pipeline.dryRun(plan)
            if (!check.isSuccess) return failure(check.error!!.message)
            val r = check.value!!
            Result.success(Data.Builder()
                .putString(WorkContract.OUT_STATUS,
                    if (r.allowed) WorkContract.STATUS_COMMITTED else WorkContract.STATUS_NEEDS_ATTENTION)
                .putString(WorkContract.OUT_MESSAGE,
                    (r.blockers + r.warnings).joinToString("; "))
                .build())
        } else {
            val executed = pipeline.execute(plan)
            if (!executed.isSuccess) return failure(executed.error!!.message)
            val record = executed.value!!
            val committed = record.items.count { it.status == OperationStatus.Committed }
            val failed = record.items.size - committed

            if (record.status == OperationStatus.NeedsAttention || failed > 0) {
                services.notifications.notify(Notification(
                    title = "FileMind needs attention",
                    body = "${record.items.size - committed} item(s) need review.",
                    operationId = record.operationId))
            }

            Result.success(Data.Builder()
                .putString(WorkContract.OUT_STATUS, when (record.status) {
                    OperationStatus.Committed -> WorkContract.STATUS_COMMITTED
                    OperationStatus.Failed -> WorkContract.STATUS_FAILED
                    else -> WorkContract.STATUS_NEEDS_ATTENTION
                })
                .putString(WorkContract.OUT_OPERATION_ID, record.operationId)
                .putInt(WorkContract.OUT_ITEMS_COMMITTED, committed)
                .putInt(WorkContract.OUT_ITEMS_FAILED, failed)
                .build())
        }
        outcome
    } catch (e: CancellationException) {
        Result.failure(Data.Builder()
            .putString(WorkContract.OUT_STATUS, WorkContract.STATUS_CANCELLED)
            .build())
    } catch (e: Exception) {
        if (runAttemptCount < 3) Result.retry() else failure(e.message ?: "organize failed")
    } }

    private fun failure(message: String): Result = Result.failure(
        Data.Builder()
            .putString(WorkContract.OUT_STATUS, WorkContract.STATUS_FAILED)
            .putString(WorkContract.OUT_MESSAGE, message)
            .build())
}

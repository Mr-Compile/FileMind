package com.filemind.platform.work

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.Data
import androidx.work.ForegroundInfo
import androidx.work.WorkerParameters
import com.filemind.core.app.Notification
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.errors.FileMindError
import com.filemind.platform.FileMindAndroid
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.toList

/**
 * Scans a granted root (SAF tree / mediastore:// folder / file:// sandbox),
 * classifies, updates the file index, and routes Review decisions into the
 * Smart Inbox. Cancelling stops enumeration; state persisted per-file by
 * the repository layer, so partial scans leave a consistent index.
 */
class ScanWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    override suspend fun getForegroundInfo(): ForegroundInfo {
        val n = FileMindAndroid.from(applicationContext).notifications
            .progress("FileMind scan", "Scanning files…", id.toString())
        return ForegroundInfo(id.hashCode(), n)
    }

    override suspend fun doWork(): Result { return try {
        val root = inputData.getString(WorkContract.IN_ROOT_URI)
            ?: return failure("missing ${WorkContract.IN_ROOT_URI}")
        val recursive = inputData.getBoolean(WorkContract.IN_RECURSIVE, true)

        val services = FileMindAndroid.from(applicationContext)
        services.grantStore.revalidate() // drop revoked grants up front
        val pipeline = services.pipeline()

        val candidates = pipeline.scan(root, recursive).toList()
        var indexed = 0
        var needsReview = 0

        val analyzed = pipeline.analyze(candidates)
        if (!analyzed.isSuccess) return failure(analyzed.error!!.message)

        for (a in analyzed.value!!) {
            val identity = FileIdentity(
                stableId = a.candidate.scanId,
                pathHint = a.candidate.sourceUri,
                fingerprint = FileIdentity.computeFingerprint(
                    a.candidate.sizeBytes, a.candidate.modifiedAt))
            services.fileIndex.upsert(a.candidate, identity)
            indexed++

            if (a.decision.kind == DecisionKind.Review) {
                services.smartInbox.add(com.filemind.core.app.SmartInboxItem(
                    id = java.util.UUID.randomUUID().toString().replace("-", ""),
                    path = a.candidate.sourceUri,
                    reason = a.decision.reasons.joinToString("; "),
                    classification = a.classification,
                    at = java.time.Instant.now(),
                    resolved = false))
                needsReview++
            }
        }

        if (needsReview > 0) {
            services.notifications.notify(Notification(
                title = "FileMind review",
                body = "$needsReview file(s) need your decision."))
        }

        Result.success(Data.Builder()
            .putString(WorkContract.OUT_STATUS, WorkContract.STATUS_COMMITTED)
            .putInt(WorkContract.OUT_SCANNED, candidates.size)
            .putInt(WorkContract.OUT_INDEXED, indexed)
            .putInt(WorkContract.OUT_REVIEWED, needsReview)
            .build())
    } catch (e: CancellationException) {
        Data.Builder().putString(WorkContract.OUT_STATUS, WorkContract.STATUS_CANCELLED).build()
            .let { Result.failure(it) }
    } catch (e: Exception) {
        if (runAttemptCount < 3) Result.retry() else failure(e.message ?: "scan failed")
    } }

    private fun failure(message: String): Result = Result.failure(
        Data.Builder()
            .putString(WorkContract.OUT_STATUS, WorkContract.STATUS_FAILED)
            .putString(WorkContract.OUT_MESSAGE, message)
            .build())
}

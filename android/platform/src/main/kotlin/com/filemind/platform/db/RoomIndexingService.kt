package com.filemind.platform.db

import com.filemind.core.app.FileIndexRepository
import com.filemind.core.app.IndexResult
import com.filemind.core.app.IndexingService
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.errors.Result

/**
 * IndexingService over Room's file_index — the same per-file upsert
 * ScanWorker performs, exposed to the interactive pipeline so
 * scan → analyze → index stays consistent between foreground and
 * background paths.
 */
class RoomIndexingService(
    private val fileIndex: FileIndexRepository
) : IndexingService {

    override suspend fun index(candidates: List<FileCandidate>): Result<IndexResult> {
        var indexed = 0
        var skipped = 0
        val warnings = mutableListOf<String>()
        for (candidate in candidates) {
            val identity = FileIdentity(
                stableId = candidate.scanId,
                pathHint = candidate.sourceUri,
                fingerprint = FileIdentity.computeFingerprint(
                    candidate.sizeBytes, candidate.modifiedAt))
            val saved = fileIndex.upsert(candidate, identity)
            if (saved.isSuccess) indexed++ else {
                skipped++
                warnings.add("${candidate.displayName}: ${saved.error!!.message}")
            }
        }
        return Result.ok(IndexResult(indexed, skipped, warnings))
    }
}

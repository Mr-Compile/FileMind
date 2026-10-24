package com.filemind.platform.grants

import android.content.Context
import android.content.Intent
import android.net.Uri
import com.filemind.platform.db.FileMindDatabase
import com.filemind.platform.db.UriGrantEntity
import com.filemind.platform.uri.AndroidUris
import com.filemind.platform.uri.GrantCoverage
import java.time.Instant

/**
 * Bookkeeping over SAF persisted URI permissions. The OS-level grant lives in
 * ContentResolver.persistedUriPermissions; uri_grants mirrors it so FileMind
 * can answer "what do we hold" without a binder round-trip and detect
 * revoked/partial state.
 *
 * takePersistableUriPermission is only valid on URIs returned by
 * ACTION_OPEN_DOCUMENT_TREE / ACTION_OPEN_DOCUMENT — callers must pass the
 * intent's flags so we only request what was actually granted.
 */
class UriGrantStore(
    private val context: Context,
    private val db: FileMindDatabase
) {

    /** Persist read/write grants offered by [flags] for [treeUri]. */
    suspend fun take(treeUri: Uri, flags: Int): Boolean {
        val resolver = context.contentResolver
        val want = flags and
            (Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
        var held = 0
        if (want and Intent.FLAG_GRANT_READ_URI_PERMISSION != 0) {
            runCatching {
                resolver.takePersistableUriPermission(
                    treeUri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }.onSuccess { held = held or Intent.FLAG_GRANT_READ_URI_PERMISSION }
        }
        if (want and Intent.FLAG_GRANT_WRITE_URI_PERMISSION != 0) {
            runCatching {
                resolver.takePersistableUriPermission(
                    treeUri, Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
            }.onSuccess { held = held or Intent.FLAG_GRANT_WRITE_URI_PERMISSION }
        }
        if (held == 0) return false

        val info = AndroidUris.classify(treeUri.toString())
        db.uriGrants().upsert(UriGrantEntity(
            treeUri = treeUri.toString(),
            authority = info.authority ?: "",
            docId = info.docId ?: "",
            canRead = held and Intent.FLAG_GRANT_READ_URI_PERMISSION != 0,
            canWrite = held and Intent.FLAG_GRANT_WRITE_URI_PERMISSION != 0,
            persisted = true,
            grantedAt = Instant.now().toEpochMilli()))
        return true
    }

    /** Release a persisted grant and drop its mirror row. */
    suspend fun release(treeUri: Uri) {
        runCatching {
            context.contentResolver.releasePersistableUriPermission(
                treeUri, Intent.FLAG_GRANT_READ_URI_PERMISSION or
                    Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
        }
        db.uriGrants().remove(treeUri.toString())
    }

    /** Currently held OS-level persisted permissions. */
    fun osGrants(): List<Uri> =
        context.contentResolver.persistedUriPermissions
            .filter { it.isReadPermission }
            .map { it.uri }

    /** Mirrored grant rows. */
    suspend fun grants(): List<UriGrantEntity> = db.uriGrants().all()

    /** Reconcile mirror vs OS state; drops rows for revoked grants. */
    suspend fun revalidate(): List<UriGrantEntity> {
        val held = context.contentResolver.persistedUriPermissions
            .associateBy({ it.uri.toString() }, { it })
        val rows = db.uriGrants().all()
        for (row in rows) {
            val perm = held[row.treeUri]
            if (perm == null) {
                db.uriGrants().remove(row.treeUri) // revoked at OS level
            } else {
                db.uriGrants().upsert(row.copy(
                    canRead = perm.isReadPermission,
                    canWrite = perm.isWritePermission))
            }
        }
        return db.uriGrants().all()
    }

    /** True when [documentUri] is covered by a held grant with [needWrite]. */
    suspend fun covers(documentUri: String, needWrite: Boolean): Boolean {
        val rows = db.uriGrants().all()
        return rows.any { row ->
            (!needWrite || row.canWrite) &&
                GrantCoverage.isCovered(
                    documentUri, listOf(GrantCoverage.Grant(row.authority, row.docId)))
        }
    }
}

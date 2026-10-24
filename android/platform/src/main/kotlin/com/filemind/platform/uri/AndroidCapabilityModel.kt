package com.filemind.platform.uri

import com.filemind.core.contracts.PermissionCapabilities
import com.filemind.platform.uri.AndroidUris.UriKind

/**
 * Maps a URI to the operations FileMind may attempt on it. This is the
 * capability model M2 requires: Android does not offer uniform path access,
 * so every operation is gated by what the underlying provider supports.
 *
 * SAF (tree grant, ExternalStorage-style path docIds):
 *   read/write/rename/createFolder/move — all supported while the grant lives.
 *   Cross-provider moves are NOT a real move — the adapter refuses them.
 * SAF document without a grant: nothing.
 * MediaStore: read/write own rows, rename via DISPLAY_NAME, move via
 *   RELATIVE_PATH update, copy via insert+pump. No explicit mkdir —
 *   folders are implicit in RELATIVE_PATH, so canCreateFolder=false.
 * App-private file:// — everything (it is a real path).
 * Other content:// — read if a grant exists; nothing else.
 */
object AndroidCapabilityModel {

    fun capabilities(uri: String, grantHeld: Boolean): PermissionCapabilities =
        when (AndroidUris.classify(uri).kind) {
            UriKind.SafDocument, UriKind.SafTree ->
                if (grantHeld) PermissionCapabilities(
                    canRead = true, canWrite = true, canMove = true,
                    canCopy = true, canRename = true, canCreateFolder = true
                ) else None
            UriKind.MediaStoreRow, UriKind.MediaStoreFolder -> PermissionCapabilities(
                canRead = true, canWrite = true, canMove = true,
                canCopy = true, canRename = true, canCreateFolder = false
            )
            UriKind.AppFile -> PermissionCapabilities.Default
            UriKind.Other ->
                if (grantHeld) PermissionCapabilities(
                    canRead = true, canWrite = false, canMove = false,
                    canCopy = false, canRename = false, canCreateFolder = false
                ) else None
        }

    /** True when a move between two URIs is supported (same SAF provider). */
    fun canMoveBetween(source: String, destination: String): Boolean {
        val s = AndroidUris.classify(source)
        val d = AndroidUris.classify(destination)
        return when {
            s.kind == UriKind.AppFile && d.kind == UriKind.AppFile -> true
            s.kind == UriKind.MediaStoreRow || s.kind == UriKind.MediaStoreFolder ->
                d.kind == UriKind.MediaStoreFolder || d.kind == UriKind.MediaStoreRow
            (s.kind == UriKind.SafDocument || s.kind == UriKind.SafTree) &&
                (d.kind == UriKind.SafDocument || d.kind == UriKind.SafTree) ->
                s.authority == d.authority
            else -> false
        }
    }

    val None = PermissionCapabilities(
        canRead = false, canWrite = false, canMove = false,
        canCopy = false, canRename = false, canCreateFolder = false
    )
}

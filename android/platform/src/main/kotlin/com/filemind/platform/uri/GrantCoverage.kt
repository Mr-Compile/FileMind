package com.filemind.platform.uri

import com.filemind.platform.uri.AndroidUris.UriKind

/**
 * Pure-logic check of whether a URI falls under a persisted SAF tree grant.
 * A tree grant covers its whole subtree: "tree/primary%3ADocs" covers
 * "document/primary%3ADocs%2Fa.txt". Matching is done on decoded docIds.
 */
object GrantCoverage {

    /** DocId prefixes granted: list of (authority, docId) from persisted trees. */
    data class Grant(val authority: String, val docId: String)

    fun fromTreeUri(treeUri: String): Grant? {
        val info = AndroidUris.classify(treeUri)
        return if (info.kind == UriKind.SafTree && info.authority != null && info.docId != null)
            Grant(info.authority, info.docId) else null
    }

    fun isCovered(uri: String, grants: Collection<Grant>): Boolean {
        val info = AndroidUris.classify(uri)
        val docId = info.docId ?: return false
        return grants.any { g ->
            g.authority == info.authority &&
                (docId == g.docId || docId.startsWith(g.docId.trimEnd('/') + "/") ||
                    docId.startsWith(g.docId.trimEnd(':') + ":"))
        }
    }

    fun isCoveredByTreeUris(uri: String, treeUris: Collection<String>): Boolean {
        val grants = treeUris.mapNotNull { fromTreeUri(it) }
        return isCovered(uri, grants)
    }
}

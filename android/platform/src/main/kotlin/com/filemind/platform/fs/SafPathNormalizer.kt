package com.filemind.platform.fs

import com.filemind.core.ports.PathNormalizer
import com.filemind.platform.uri.AndroidUris
import com.filemind.platform.uri.AndroidUris.UriKind

/**
 * PathNormalizer over FileMind's Android URI model. Path math happens on the
 * path-ish portion (SAF docId path, RELATIVE_PATH, file path); the URI prefix
 * (scheme/authority/volume) is preserved verbatim.
 */
class SafPathNormalizer : PathNormalizer {

    override fun normalize(path: String): String {
        val info = AndroidUris.classify(path)
        return when (info.kind) {
            UriKind.SafDocument -> AndroidUris.documentUri(
                info.authority!!, normalizeRel(info.docId!!))
            UriKind.SafTree -> AndroidUris.treeUri(
                info.authority!!, normalizeRel(info.docId!!))
            UriKind.MediaStoreFolder -> AndroidUris.mediaStoreFolder(
                info.volume!!, normalizeRel(info.relPath ?: ""))
            UriKind.AppFile -> "file://${normalizeRel(info.relPath ?: "")}"
            else -> path
        }
    }

    override fun combine(vararg parts: String): String {
        if (parts.isEmpty()) return ""
        var result = parts.first()
        for (part in parts.drop(1)) {
            result = combineTwo(result, part)
        }
        return normalize(result)
    }

    private fun combineTwo(base: String, child: String): String {
        if (child.isEmpty()) return base
        val info = AndroidUris.classify(base)
        return when (info.kind) {
            // Combine operates on the docId path; emit a document URI (a child
            // of a tree is always a document).
            UriKind.SafTree, UriKind.SafDocument -> AndroidUris.documentUri(
                info.authority!!, joinRel(info.docId!!, child))
            UriKind.MediaStoreFolder -> AndroidUris.mediaStoreFolder(
                info.volume!!, joinRel(info.relPath ?: "", child))
            UriKind.AppFile -> "file://${joinRel(info.relPath ?: "", child)}"
            // MediaStore rows are leaves; combining under one is invalid, and
            // other providers have opaque docIds — surface both as failures
            // downstream (the adapter reports UnsupportedOperation).
            else -> throw IllegalArgumentException(
                "Cannot combine a child path under unsupported URI: $base")
        }
    }

    override fun isUnder(path: String, root: String): Boolean {
        val p = AndroidUris.classify(path)
        val r = AndroidUris.classify(root)
        if (p.authority != r.authority) return false
        val pp = pathPart(p) ?: return false
        val rp = pathPart(r) ?: return false
        if (p.volume != r.volume) return false
        if (rp.isEmpty()) return true // volume-root grant covers the whole volume
        return pp == rp || pp.startsWith(rp.trimEnd('/') + "/")
    }

    override fun relativeTo(path: String, root: String): String? {
        if (!isUnder(path, root)) return null
        val pp = pathPart(AndroidUris.classify(path)) ?: return null
        val rp = pathPart(AndroidUris.classify(root)) ?: return null
        if (rp.isEmpty()) return pp
        if (pp == rp) return ""
        return pp.removePrefix(rp.trimEnd('/') + "/")
    }

    override fun fileName(path: String): String {
        val info = AndroidUris.classify(path)
        val rel = info.relPath ?: info.docId?.let { pathOfDocId(it) } ?: path
        return rel.trimEnd('/').substringAfterLast('/')
    }

    override fun directoryOf(path: String): String? {
        val info = AndroidUris.classify(path)
        return when (info.kind) {
            UriKind.SafDocument -> {
                val docId = info.docId ?: return null
                val rel = pathOfDocId(docId) ?: return null
                val vol = docId.substringBefore(':')
                val parentRel = rel.substringBeforeLast('/', "")
                AndroidUris.documentUri(info.authority!!,
                    if (parentRel.isEmpty()) "$vol:" else "$vol:$parentRel")
            }
            UriKind.SafTree -> {
                val docId = info.docId ?: return null
                val rel = pathOfDocId(docId) ?: return null
                if (rel.isEmpty()) return null
                val vol = docId.substringBefore(':')
                val parentRel = rel.substringBeforeLast('/', "")
                AndroidUris.treeUri(info.authority!!,
                    if (parentRel.isEmpty()) "$vol:" else "$vol:$parentRel")
            }
            UriKind.MediaStoreFolder -> {
                val rel = info.relPath ?: return null
                if (rel.isEmpty()) return null
                AndroidUris.mediaStoreFolder(info.volume!!,
                    rel.substringBeforeLast('/', ""))
            }
            UriKind.AppFile -> {
                val rel = info.relPath ?: return null
                val parent = rel.trimEnd('/').substringBeforeLast('/', "")
                if (parent.isEmpty()) null else "file://$parent"
            }
            else -> null
        }
    }

    private fun pathPart(info: AndroidUris.UriInfo): String? =
        info.relPath ?: info.docId?.let { pathOfDocId(it) }

    private fun pathOfDocId(docId: String): String? =
        if (docId.contains(':')) docId.substringAfter(':').trim('/') else null

    private fun joinRel(parent: String, child: String): String {
        val p = parent.trimEnd('/')
        val c = child.trim('/')
        return if (p.endsWith(':') || p.isEmpty()) "$p$c" else "$p/$c"
    }

    private fun normalizeRel(rel: String): String =
        rel.replace('\\', '/').replace(Regex("/+"), "/")
}

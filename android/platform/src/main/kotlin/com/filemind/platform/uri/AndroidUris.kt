package com.filemind.platform.uri

import java.net.URLDecoder
import java.net.URLEncoder

/**
 * FileMind's canonical "path" model on Android. The engine treats paths as
 * opaque strings; on Android they are URIs, not filesystem paths:
 *
 *  - SAF document URIs:   content://<provider>/document/<docId>
 *  - SAF tree grants:     content://<provider>/tree/<docId>
 *  - MediaStore rows:     content://media/<volume>/<bucket>/<id>
 *  - MediaStore folders:  mediastore://<volume>/<relativePath>  (virtual scheme;
 *                         RELATIVE_PATH has no URI form — the adapter
 *                         translates it to MediaStore columns)
 *  - App-private files:   file://<absolute-path>
 *
 * Everything else (generic content:// providers) is read-only at best and is
 * surfaced through capabilities — never silently treated as a path.
 */
object AndroidUris {

    const val MediaStoreScheme = "mediastore"
    const val ExternalStorageAuthority = "com.android.externalstorage.documents"
    const val MediaAuthority = "media"

    enum class UriKind { SafDocument, SafTree, MediaStoreRow, MediaStoreFolder, AppFile, Other }

    data class UriInfo(
        val raw: String,
        val kind: UriKind,
        /** content:// authority (or volume name for mediastore://). */
        val authority: String?,
        /** Decoded provider document id (e.g. "primary:Docs/a.txt") — SAF only. */
        val docId: String?,
        /** Volume id for SAF ("primary", sdcard uuid…) / MediaStore ("external"…). */
        val volume: String?,
        /** Path-ish remainder: docId path part, RELATIVE_PATH, or file path. */
        val relPath: String?
    )

    fun classify(uri: String): UriInfo {
        if (uri.startsWith("$MediaStoreScheme://")) {
            // mediastore://<volume>/<relative/path>
            val rest = uri.removePrefix("$MediaStoreScheme://")
            val volume = rest.substringBefore('/')
            val rel = rest.substringAfter('/', "")
            return UriInfo(uri, UriKind.MediaStoreFolder, volume, null, volume, rel.trimEnd('/'))
        }
        if (!uri.contains("://")) {
            // bare path — treat as app-private file path
            return UriInfo(uri, UriKind.AppFile, null, null, null, uri)
        }
        val scheme = uri.substringBefore("://")
        if (scheme == "file") {
            val path = uri.removePrefix("file://")
            return UriInfo(uri, UriKind.AppFile, null, null, null, path)
        }
        if (scheme != "content") {
            return UriInfo(uri, UriKind.Other, null, null, null, null)
        }
        val rest = uri.removePrefix("content://")
        val authority = rest.substringBefore('/')
        val segments = rest.substringAfter('/', "").split('/')
        return when {
            authority == MediaAuthority -> {
                // content://media/<volume>/<bucket>/<id>
                val volume = segments.getOrNull(0)
                UriInfo(uri, UriKind.MediaStoreRow, authority, null, volume,
                    segments.drop(1).joinToString("/"))
            }
            segments.getOrNull(0) == "tree" &&
                segments.getOrNull(2) == "document" -> {
                // Tree-scoped document: tree/<grantDocId>/document/<leafDocId>.
                // Emitted by DocumentFile children and buildDocumentUriUsingTree;
                // the leaf docId is what identifies the file.
                val docId = decode(segments.getOrNull(3) ?: "")
                UriInfo(uri, UriKind.SafDocument, authority, docId, volumeOf(docId), pathOf(docId))
            }
            segments.getOrNull(0) == "tree" -> {
                val docId = decode(segments.getOrNull(1) ?: "")
                UriInfo(uri, UriKind.SafTree, authority, docId, volumeOf(docId), pathOf(docId))
            }
            segments.getOrNull(0) == "document" -> {
                val docId = decode(segments.getOrNull(1) ?: "")
                UriInfo(uri, UriKind.SafDocument, authority, docId, volumeOf(docId), pathOf(docId))
            }
            else -> UriInfo(uri, UriKind.Other, authority, null, null, null)
        }
    }

    /** SAF docId grammar for path-capable providers: "<volume>:<rel/path>". */
    private fun volumeOf(docId: String): String? =
        if (docId.contains(':')) docId.substringBefore(':').ifEmpty { null } else null

    private fun pathOf(docId: String): String? =
        if (docId.contains(':')) docId.substringAfter(':').trim('/') else null

    /** Build a document URI under [authority] for [docId]. */
    fun documentUri(authority: String, docId: String): String =
        "content://$authority/document/${encodeDocId(docId)}"

    /** Build a tree URI under [authority] for [docId]. */
    fun treeUri(authority: String, docId: String): String =
        "content://$authority/tree/${encodeDocId(docId)}"

    /** Build a mediastore folder URI: mediastore://<volume>/<relPath>. */
    fun mediaStoreFolder(volume: String, relPath: String): String =
        "$MediaStoreScheme://$volume/${relPath.trim('/')}"

    /** docId for a tree/document uri, decoded. */
    fun docIdOf(uri: String): String? = classify(uri).docId

    /** Child docId: "<vol>:<parent>/<child>" — provider-agnostic path grammar. */
    fun childDocId(parentDocId: String, childName: String): String {
        val base = parentDocId.trimEnd('/')
        return if (base.endsWith(':')) "$base$childName" else "$base/$childName"
    }

    fun decode(s: String): String = URLDecoder.decode(s, Charsets.UTF_8)

    /**
     * Percent-encode a docId for embedding in a URI path segment.
     * ':' and '/' inside docIds must be encoded (%3A, %2F).
     */
    fun encodeDocId(docId: String): String =
        URLEncoder.encode(docId, Charsets.UTF_8)
            .replace("+", "%20") // URLEncoder encodes space as '+'; URIs want %20
}

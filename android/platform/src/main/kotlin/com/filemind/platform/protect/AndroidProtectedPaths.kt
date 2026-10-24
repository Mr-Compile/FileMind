package com.filemind.platform.protect

import com.filemind.core.ports.ProtectedPathProvider
import com.filemind.platform.uri.AndroidUris
import com.filemind.platform.uri.AndroidUris.UriKind

/**
 * Android protected-path policy. Mirrors the Windows ProtectedPaths engine
 * concept: protected/system/application paths are denied by default.
 *
 * On Android the "paths" are URI/docId spaces:
 *  - Android/data + Android/obb on every volume (app-private dirs of other apps;
 *    SAF blocks them on API 30+ anyway — belt and suspenders).
 *  - Other apps' internal storage is unreachable by definition (no URI exists).
 *  - Provider/system docIds: "root" trees and hidden dot-prefix roots.
 *  - User-added protected prefixes are combined on top.
 */
class AndroidProtectedPathProvider(
    userPaths: List<String> = emptyList()
) : ProtectedPathProvider {

    /** Rel-path prefixes (lowercase, '/'-separated) denied on any volume. */
    private val deniedRelPrefixes = listOf(
        "android/data", "android/obb", "android/sandbox", ".android_secure"
    )

    private val userPrefixes = userPaths.map { it.trimEnd('/').lowercase() }

    override val protectedRoots: List<String>
        get() = deniedRelPrefixes.map { "*/$it" } + userPrefixes

    override fun isProtected(path: String): Boolean {
        val info = AndroidUris.classify(path)
        val rel = when (info.kind) {
            UriKind.SafDocument, UriKind.SafTree ->
                info.docId?.substringAfter(':') ?: return false
            UriKind.MediaStoreFolder -> info.relPath ?: return false
            UriKind.AppFile -> info.relPath ?: return false
            // MediaStore rows are never mutation targets; foreign providers are
            // already capability-gated, not "protected".
            else -> return false
        }.trim('/').lowercase()

        if (deniedRelPrefixes.any { rel == it || rel.startsWith("$it/") }) return true
        return userPrefixes.any { rel == it || rel.startsWith("$it/") }
    }
}

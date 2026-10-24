package com.filemind.core.app

import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.SourceScope
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.PathNormalizer
import java.security.MessageDigest
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow

class FileScanner(
    private val fs: FileSystemAdapter,
    private val paths: PathNormalizer,
    private val exclusions: ExclusionMatcher? = null
) {
    fun scan(root: String, recursive: Boolean = true): Flow<FileCandidate> = flow {
        val scope = inferScope(root)
        fs.enumerate(root, recursive).collect { entry ->
            val name = paths.fileName(entry.path)
            val normalized = paths.normalize(entry.path)
            if (exclusions?.isExcluded(name) == true || exclusions?.isExcluded(normalized) == true) {
                return@collect
            }

            val ext = if (name.contains('.')) name.substring(name.lastIndexOf('.') + 1) else ""
            emit(FileCandidate(
                scanId = stableScanId(normalized),
                sourceUri = entry.path,
                displayName = name,
                extension = ext,
                mimeType = MimeMap.fromExtension(ext),
                sizeBytes = entry.sizeBytes,
                createdAt = entry.createdAt,
                modifiedAt = entry.modifiedAt,
                sourceScope = scope
            ))
        }
    }

    private fun inferScope(root: String): SourceScope {
        val r = root.replace('/', '\\')
        fun containsIgnoreCase(needle: String) = r.contains(needle, ignoreCase = true)
        if (containsIgnoreCase("\\Downloads")) return SourceScope.Downloads
        if (containsIgnoreCase("\\Documents")) return SourceScope.Documents
        if (containsIgnoreCase("\\Desktop")) return SourceScope.Desktop
        if (containsIgnoreCase("\\Pictures") || containsIgnoreCase("\\Videos") || containsIgnoreCase("\\Music")) {
            return SourceScope.Media
        }
        return SourceScope.Other
    }

    private fun stableScanId(normalizedPath: String): String {
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(normalizedPath.toByteArray(Charsets.UTF_8))
        return digest.joinToString("") { "%02x".format(it) }.substring(0, 16)
    }
}

package com.filemind.platform.fs

import android.content.ContentValues
import android.content.Context
import android.net.Uri
import android.os.StatFs
import android.provider.DocumentsContract
import android.provider.MediaStore
import androidx.documentfile.provider.DocumentFile
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.PermissionCapabilities
import com.filemind.core.errors.ErrorCode
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.FileEntry
import com.filemind.core.ports.FileStat
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.platform.db.UriGrantEntity
import com.filemind.platform.grants.UriGrantStore
import com.filemind.platform.protect.AndroidProtectedPathProvider
import com.filemind.platform.uri.AndroidCapabilityModel
import com.filemind.platform.uri.AndroidUris
import com.filemind.platform.uri.GrantCoverage
import com.filemind.platform.uri.AndroidUris.UriKind
import java.io.File
import java.io.FileInputStream
import java.io.InputStream
import java.time.Instant
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import kotlinx.coroutines.withContext

/**
 * FileSystemAdapter over Android's authorized storage mechanisms only:
 * SAF document/tree URIs under persisted grants, MediaStore rows/folders,
 * and the app's own file:// sandbox. There is no unrestricted path access —
 * anything else is surfaced as a capability/permission error, never faked.
 */
class AndroidFileSystemAdapter(
    private val context: Context,
    private val grantStore: UriGrantStore,
    private val userProtectedPaths: List<String> = emptyList()
) : FileSystemAdapter {

    private val resolver get() = context.contentResolver
    private val normalizer = SafPathNormalizer()
    private val protectedPaths = AndroidProtectedPathProvider(userProtectedPaths)

    // ------------------------------------------------------------ enumerate

    override fun enumerate(root: String, recursive: Boolean): Flow<FileEntry> = flow {
        when (AndroidUris.classify(root).kind) {
            UriKind.SafTree -> emitSafChildren(Uri.parse(root), recursive)
            UriKind.MediaStoreFolder, UriKind.MediaStoreRow -> emitMediaChildren(root, recursive)
            UriKind.AppFile -> emitFileChildren(File(AndroidUris.classify(root).relPath!!), recursive)
            else -> throw UnsupportedOperationException("Cannot enumerate $root")
        }
    }.flowOn(Dispatchers.IO)

    private suspend fun kotlinx.coroutines.flow.FlowCollector<FileEntry>.emitSafChildren(
        tree: Uri, recursive: Boolean
    ) {
        val root = DocumentFile.fromTreeUri(context, tree)
            ?: throw IllegalStateException("Tree grant not readable: $tree")
        val stack = ArrayDeque<DocumentFile>()
        stack.add(root)
        while (stack.isNotEmpty()) {
            val dir = stack.removeLast()
            for (child in dir.listFiles()) {
                if (child.isDirectory) {
                    if (recursive) stack.add(child)
                } else if (child.isFile) {
                    emit(FileEntry(
                        path = child.uri.toString(),
                        sizeBytes = child.length(),
                        createdAt = Instant.ofEpochMilli(child.lastModified()),
                        modifiedAt = Instant.ofEpochMilli(child.lastModified()),
                        // Provider metadata — opaque docIds (msf:NNN, numeric
                        // ids) carry no usable name in the URI itself.
                        displayName = child.name,
                        mimeType = child.type))
                }
            }
        }
    }

    private suspend fun kotlinx.coroutines.flow.FlowCollector<FileEntry>.emitMediaChildren(
        root: String, recursive: Boolean
    ) {
        val info = AndroidUris.classify(root)
        val volume = info.volume ?: MediaStore.VOLUME_EXTERNAL
        val collection = MediaStore.Files.getContentUri(volume)
        val projection = arrayOf(
            MediaStore.MediaColumns._ID,
            MediaStore.MediaColumns.DISPLAY_NAME,
            MediaStore.MediaColumns.MIME_TYPE,
            MediaStore.MediaColumns.SIZE,
            MediaStore.MediaColumns.DATE_MODIFIED,
            MediaStore.MediaColumns.RELATIVE_PATH)
        val selection: String?
        val args: Array<String>?
        if (info.kind == UriKind.MediaStoreFolder && !info.relPath.isNullOrEmpty()) {
            val rel = info.relPath.trim('/') + "/"
            selection = "${MediaStore.MediaColumns.RELATIVE_PATH} LIKE ?"
            args = if (recursive) arrayOf("$rel%") else arrayOf(rel)
        } else {
            selection = null; args = null
        }
        resolver.query(collection, projection, selection, args, null)?.use { c ->
            val idCol = c.getColumnIndexOrThrow(MediaStore.MediaColumns._ID)
            val nameCol = c.getColumnIndexOrThrow(MediaStore.MediaColumns.DISPLAY_NAME)
            val mimeCol = c.getColumnIndexOrThrow(MediaStore.MediaColumns.MIME_TYPE)
            val sizeCol = c.getColumnIndexOrThrow(MediaStore.MediaColumns.SIZE)
            val modCol = c.getColumnIndexOrThrow(MediaStore.MediaColumns.DATE_MODIFIED)
            while (c.moveToNext()) {
                val id = c.getLong(idCol)
                val name = c.getString(nameCol) ?: continue
                val modified = Instant.ofEpochSecond(c.getLong(modCol))
                emit(FileEntry(
                    path = android.content.ContentUris.withAppendedId(collection, id).toString(),
                    sizeBytes = c.getLong(sizeCol),
                    createdAt = modified,
                    modifiedAt = modified,
                    // Row URIs are numeric ids — the real name/mime only exist
                    // in the cursor columns.
                    displayName = name,
                    mimeType = c.getString(mimeCol)))
            }
        }
    }

    private suspend fun kotlinx.coroutines.flow.FlowCollector<FileEntry>.emitFileChildren(
        root: File, recursive: Boolean
    ) {
        val stack = ArrayDeque<File>()
        stack.add(root)
        while (stack.isNotEmpty()) {
            val dir = stack.removeLast()
            dir.listFiles()?.forEach { child ->
                if (child.isDirectory) {
                    if (recursive) stack.add(child)
                } else {
                    emit(FileEntry(
                        path = "file://${child.absolutePath}",
                        sizeBytes = child.length(),
                        createdAt = Instant.ofEpochMilli(child.lastModified()),
                        modifiedAt = Instant.ofEpochMilli(child.lastModified()),
                        displayName = child.name))
                }
            }
        }
    }

    // ------------------------------------------------------------ stat / io

    override suspend fun stat(path: String): Result<FileStat> = withContext(Dispatchers.IO) {
        runCatching {
            val info = AndroidUris.classify(path)
            when (info.kind) {
                UriKind.SafDocument, UriKind.SafTree -> {
                    if (!grantStore.covers(path, needWrite = false)) {
                        return@runCatching Result.fail<FileStat>(FileMindError(
                            ErrorCode.PermissionDenied,
                            "No persisted grant covers $path",
                            retrySafe = false, requiresReview = true))
                    }
                    val doc = docFor(path) ?: return@runCatching notFound(path)
                    Result.ok(FileStat(path, doc.length(),
                        Instant.ofEpochMilli(doc.lastModified()),
                        Instant.ofEpochMilli(doc.lastModified()),
                        doc.isDirectory))
                }
                UriKind.MediaStoreRow -> statMedia(path)
                UriKind.MediaStoreFolder -> Result.ok(FileStat(
                    path, 0, Instant.EPOCH, Instant.EPOCH, isDirectory = true))
                UriKind.AppFile -> {
                    val f = File(info.relPath!!)
                    if (!f.exists()) return@runCatching notFound(path)
                    Result.ok(FileStat(path, f.length(),
                        Instant.ofEpochMilli(f.lastModified()),
                        Instant.ofEpochMilli(f.lastModified()), f.isDirectory))
                }
                UriKind.Other -> Result.fail<FileStat>(FileMindError(
                    ErrorCode.UnsupportedOperation,
                    "Unsupported storage URI: $path",
                    retrySafe = false, requiresReview = true))
            }
        }.getOrElse {
            Result.fail<FileStat>(FileMindError.io("stat failed for $path: ${it.message}"))
        }
    }

    private fun statMedia(path: String): Result<FileStat> {
        val projection = arrayOf(
            MediaStore.MediaColumns.SIZE, MediaStore.MediaColumns.DATE_MODIFIED)
        resolver.query(Uri.parse(path), projection, null, null, null)?.use { c ->
            if (!c.moveToFirst()) return notFound(path)
            val modified = Instant.ofEpochSecond(c.getLong(1))
            return Result.ok(FileStat(path, c.getLong(0), modified, modified, false))
        }
        return notFound(path)
    }

    override suspend fun openRead(path: String): Result<InputStream> =
        withContext(Dispatchers.IO) {
            runCatching {
                val info = AndroidUris.classify(path)
                val stream: InputStream? = when (info.kind) {
                    UriKind.AppFile -> FileInputStream(File(info.relPath!!))
                    else -> resolver.openInputStream(Uri.parse(path))
                }
                if (stream == null) notFound(path) else Result.ok(stream)
            }.getOrElse {
                Result.fail<InputStream>(FileMindError.io("openRead failed for $path: ${it.message}"))
            }
        }

    override suspend fun exists(path: String): Result<Boolean> = withContext(Dispatchers.IO) {
        runCatching {
            val info = AndroidUris.classify(path)
            when (info.kind) {
                UriKind.SafDocument, UriKind.SafTree -> Result.ok(docFor(path)?.exists() == true)
                UriKind.MediaStoreRow -> {
                    var found = false
                    resolver.query(Uri.parse(path),
                        arrayOf(MediaStore.MediaColumns._ID), null, null, null)?.use {
                        found = it.moveToFirst()
                    }
                    Result.ok(found)
                }
                UriKind.MediaStoreFolder -> Result.ok(true) // RELATIVE_PATH is virtual
                UriKind.AppFile -> Result.ok(File(info.relPath!!).exists())
                UriKind.Other -> Result.ok(false)
            }
        }.getOrElse {
            Result.fail<Boolean>(FileMindError.io("exists failed for $path: ${it.message}"))
        }
    }

    // ------------------------------------------------------------ mutations

    override suspend fun createDirectory(path: String): Result<Boolean> =
        withContext(Dispatchers.IO) {
            val info = AndroidUris.classify(path)
            when (info.kind) {
                UriKind.SafTree, UriKind.SafDocument -> ensureSafDirs(info)
                UriKind.AppFile -> runCatching {
                    Result.ok(File(info.relPath!!).mkdirs() || File(info.relPath!!).isDirectory)
                }.getOrElse {
                    Result.fail(FileMindError.io("mkdir failed for $path: ${it.message}"))
                }
                UriKind.MediaStoreFolder -> Result.fail(FileMindError(
                    ErrorCode.UnsupportedOperation,
                    "MediaStore has no explicit mkdir; RELATIVE_PATH creates folders implicitly.",
                    retrySafe = false, requiresReview = true))
                else -> Result.fail(FileMindError(
                    ErrorCode.UnsupportedOperation,
                    "Cannot create a directory under $path",
                    retrySafe = false, requiresReview = true))
            }
        }

    /**
     * Ensure each rel-path segment of [info]'s docId exists as a directory,
     * creating missing ones under the held tree grant.
     */
    /** Deepest held grant covering [uri] with [needWrite] permission, or null. */
    private suspend fun coveringGrant(uri: String, needWrite: Boolean): UriGrantEntity? =
        grantStore.grants()
            .filter { (!needWrite || it.canWrite) && GrantCoverage.isCovered(
                uri, listOf(GrantCoverage.Grant(it.authority, it.docId))) }
            .maxByOrNull { it.docId.length }

    /**
     * Document URI scoped under the covering tree grant —
     * "tree/<grant>/document/<docId>". Persisted tree permissions only cover
     * URIs in this form; plain "document/<docId>" URIs are denied.
     */
    private suspend fun scopedDocUri(authority: String, docId: String,
                                     needWrite: Boolean): Uri? {
        val grant = coveringGrant(AndroidUris.documentUri(authority, docId), needWrite)
            ?: return null
        return DocumentsContract.buildDocumentUriUsingTree(Uri.parse(grant.treeUri), docId)
    }

    private suspend fun ensureSafDirs(info: AndroidUris.UriInfo): Result<Boolean> {
        val docId = info.docId ?: return Result.fail(FileMindError.validation(
            "Not a document URI: ${info.raw}"))
        // Deepest held grant covering the target — walking must start at the
        // granted tree root: segments above it are outside the grant, so
        // findFile/createDirectory on them is denied by the provider.
        val grant = coveringGrant(info.raw, needWrite = true)
            ?: return Result.fail(FileMindError(
                ErrorCode.PermissionDenied, "No write grant covers ${info.raw}",
                retrySafe = false, requiresReview = true))
        val grantPrefix = grant.docId.trimEnd('/', ':')
        val below = docId.removePrefix(grantPrefix).trimStart('/', ':')
        val segments = below.split('/').filter { it.isNotEmpty() }
        var current = DocumentFile.fromTreeUri(context, Uri.parse(grant.treeUri))
        var curDocId = grant.docId
        for (seg in segments) {
            if (current == null) return Result.fail(FileMindError.io(
                "Lost directory handle while ensuring $curDocId"))
            var next = current.findFile(seg)
            if (next == null) {
                next = current.createDirectory(seg)
                    ?: return Result.fail(FileMindError.io(
                        "createDirectory failed for '$seg' under $curDocId"))
            }
            if (!next.isDirectory) return Result.fail(FileMindError.conflict(
                "'$seg' exists under $curDocId and is not a directory."))
            curDocId = AndroidUris.childDocId(curDocId, seg)
            current = next
        }
        return Result.ok(true)
    }

    override suspend fun move(source: String, destination: String, conflict: ConflictPolicy)
            : Result<String> = withContext(Dispatchers.IO) {
        val src = AndroidUris.classify(source)
        val dst = AndroidUris.classify(destination)
        if (!AndroidCapabilityModel.canMoveBetween(source, destination)) {
            return@withContext Result.fail(FileMindError(
                ErrorCode.UnsupportedOperation,
                "Move across providers/volumes is not supported on Android: $source -> $destination",
                retrySafe = false, requiresReview = true))
        }
        when {
            src.kind == UriKind.AppFile -> moveFile(src.relPath!!, dst.relPath!!, source, destination, conflict)
            src.kind == UriKind.MediaStoreRow && dst.kind == UriKind.MediaStoreFolder ->
                moveMedia(source, destination, conflict)
            (src.kind == UriKind.SafDocument || src.kind == UriKind.SafTree) &&
                (dst.kind == UriKind.SafDocument || dst.kind == UriKind.SafTree) ->
                moveSaf(source, destination, conflict)
            else -> Result.fail(FileMindError(
                ErrorCode.UnsupportedOperation,
                "Unsupported move $source -> $destination",
                retrySafe = false, requiresReview = true))
        }
    }

    private suspend fun moveSaf(source: String, destination: String, conflict: ConflictPolicy): Result<String> {
        val srcDocId = AndroidUris.docIdOf(source)!!
        val dstDocId = AndroidUris.docIdOf(destination)!!
        val srcParentDocId = parentDocId(srcDocId)
        val dstParentDocId = parentDocId(dstDocId)
        val authority = AndroidUris.classify(source).authority!!

        if (docExists(authority, dstDocId)) {
            return when (conflict) {
                ConflictPolicy.KeepBoth -> moveSaf(source, uniquified(destination, authority), conflict)
                ConflictPolicy.Skip -> Result.ok(source)
                ConflictPolicy.Ask, ConflictPolicy.ReplaceWithConfirmation ->
                    Result.fail(FileMindError.conflict(
                        "Destination exists: $destination — never overwriting silently."))
            }
        }

        val ensured = ensureSafDirs(AndroidUris.classify(
            AndroidUris.documentUri(authority, dstParentDocId)))
        if (!ensured.isSuccess) return Result.fail(ensured.error!!)

        val srcUri = scopedDocUri(authority, srcDocId, needWrite = true)
        val srcParentUri = scopedDocUri(authority, srcParentDocId, needWrite = true)
        val dstParentUri = scopedDocUri(authority, dstParentDocId, needWrite = true)
        if (srcUri == null || srcParentUri == null || dstParentUri == null) {
            return Result.fail(FileMindError(
                ErrorCode.PermissionDenied,
                "No write grant covers move $source -> $destination",
                retrySafe = false, requiresReview = true))
        }
        return runCatching {
            val result = DocumentsContract.moveDocument(
                resolver, srcUri, srcParentUri, dstParentUri)
            if (result == null) {
                // Some providers return null but still move; verify via stat.
                if (docExists(authority, dstDocId)) Result.ok(destination)
                else Result.fail(FileMindError.io("moveDocument returned null for $source"))
            } else {
                Result.ok(result.toString().let { if (it == destination) it else destination })
            }
        }.getOrElse {
            if (it is SecurityException) Result.fail(FileMindError(
                ErrorCode.PermissionDenied, "move denied for $source: ${it.message}",
                retrySafe = false, requiresReview = true))
            else Result.fail(FileMindError.io("move failed for $source: ${it.message}"))
        }
    }

    private fun moveMedia(source: String, destination: String, conflict: ConflictPolicy): Result<String> {
        val dst = AndroidUris.classify(destination)
        val fullRel = (dst.relPath ?: "").trim('/')
        // relPath of a destination includes the file name — RELATIVE_PATH is
        // the directory portion only (MediaStore requires a trailing slash).
        val relDir = fullRel.substringBeforeLast('/', "")
        val newRel = if (relDir.isEmpty()) "" else "$relDir/"
        val newName = fullRel.substringAfterLast('/')
        return runCatching {
            val values = android.content.ContentValues().apply {
                put(MediaStore.MediaColumns.RELATIVE_PATH, newRel)
                put(MediaStore.MediaColumns.DISPLAY_NAME, newName)
            }
            val rows = resolver.update(Uri.parse(source), values, null, null)
            if (rows > 0) Result.ok(destination)
            else notFound<String>(source)
        }.getOrElse { Result.fail(FileMindError.io("MediaStore move failed: ${it.message}")) }
    }

    private fun moveFile(srcPath: String, dstPath: String, source: String, destination: String,
                         conflict: ConflictPolicy): Result<String> = runCatching {
        val src = File(srcPath); var dst = File(dstPath)
        if (!src.exists()) return notFound(source)
        if (dst.exists()) {
            when (conflict) {
                ConflictPolicy.KeepBoth -> dst = File(uniquifyFilePath(dstPath))
                ConflictPolicy.Skip -> return Result.ok(source)
                ConflictPolicy.Ask, ConflictPolicy.ReplaceWithConfirmation ->
                    return Result.fail(FileMindError.conflict(
                        "Destination exists: $destination"))
            }
        }
        dst.parentFile?.mkdirs()
        if (src.renameTo(dst)) Result.ok("file://${dst.absolutePath}")
        else Result.fail(FileMindError.io("renameTo failed: $source -> ${dst.absolutePath}"))
    }.getOrElse { Result.fail(FileMindError.io("move failed: ${it.message}")) }

    override suspend fun copy(source: String, destination: String, conflict: ConflictPolicy)
            : Result<String> = withContext(Dispatchers.IO) {
        val src = AndroidUris.classify(source)
        val dst = AndroidUris.classify(destination)
        runCatching<Result<String>> {
            val input = when (src.kind) {
                UriKind.AppFile -> FileInputStream(File(src.relPath!!))
                else -> resolver.openInputStream(Uri.parse(source))
                    ?: return@runCatching notFound(source)
            }
            input.use { ins ->
                when (dst.kind) {
                    UriKind.SafDocument, UriKind.SafTree -> copyToSaf(ins, source, destination, conflict)
                    UriKind.MediaStoreFolder, UriKind.MediaStoreRow -> copyToMedia(ins, source, destination, conflict)
                    UriKind.AppFile -> {
                        var out = File(dst.relPath!!)
                        if (out.exists()) {
                            when (conflict) {
                                ConflictPolicy.KeepBoth -> out = File(uniquifyFilePath(dst.relPath))
                                ConflictPolicy.Skip -> return@use Result.ok(source)
                                else -> return@use Result.fail(FileMindError.conflict(
                                    "Destination exists: $destination"))
                            }
                        }
                        out.parentFile?.mkdirs()
                        out.outputStream().use { ins.copyTo(it) }
                        Result.ok("file://${out.absolutePath}")
                    }
                    UriKind.Other -> Result.fail(FileMindError(
                        ErrorCode.UnsupportedOperation,
                        "Cannot copy into $destination", retrySafe = false, requiresReview = true))
                }
            }
        }.getOrElse {
            Result.fail(FileMindError.io("copy failed $source -> $destination: ${it.message}"))
        }
    }

    private suspend fun copyToSaf(ins: InputStream, source: String, destination: String,
                          conflict: ConflictPolicy): Result<String> {
        val dstDocId = AndroidUris.docIdOf(destination)!!
        val authority = AndroidUris.classify(destination).authority!!
        var targetDocId = dstDocId
        if (docExists(authority, targetDocId)) {
            when (conflict) {
                ConflictPolicy.KeepBoth -> targetDocId = docIdOf(uniquified(
                    AndroidUris.documentUri(authority, dstDocId), authority))
                ConflictPolicy.Skip -> return Result.ok(source)
                else -> return Result.fail(FileMindError.conflict(
                    "Destination exists: $destination"))
            }
        }
        val parent = parentDocId(targetDocId)
        val ensured = ensureSafDirs(AndroidUris.classify(
            AndroidUris.documentUri(authority, parent)))
        if (!ensured.isSuccess) return Result.fail(ensured.error!!)
        val name = targetDocId.substringAfterLast('/')
        val mime = resolver.getType(Uri.parse(source)) ?: "application/octet-stream"
        val parentUri = scopedDocUri(authority, parent, needWrite = true)
            ?: return Result.fail(FileMindError(
                ErrorCode.PermissionDenied,
                "No write grant covers $destination",
                retrySafe = false, requiresReview = true))
        val created = DocumentsContract.createDocument(resolver, parentUri, mime, name)
            ?: return Result.fail(FileMindError.io("createDocument failed for $destination"))
        resolver.openOutputStream(created)?.use { out -> ins.copyTo(out) }
            ?: return Result.fail(FileMindError.io("openOutputStream failed for $destination"))
        return Result.ok(destination)
    }

    private fun copyToMedia(ins: InputStream, source: String, destination: String,
                            conflict: ConflictPolicy): Result<String> {
        val dst = AndroidUris.classify(destination)
        val volume = dst.volume ?: MediaStore.VOLUME_EXTERNAL
        val collection = MediaStore.Files.getContentUri(volume)
        val fullRel = (dst.relPath ?: "").trim('/')
        val relDir = fullRel.substringBeforeLast('/', "")
        val name = fullRel.substringAfterLast('/')
        val values = android.content.ContentValues().apply {
            put(MediaStore.MediaColumns.DISPLAY_NAME, name)
            put(MediaStore.MediaColumns.RELATIVE_PATH, if (relDir.isEmpty()) "" else "$relDir/")
            put(MediaStore.MediaColumns.MIME_TYPE,
                resolver.getType(Uri.parse(source)) ?: "application/octet-stream")
            put(MediaStore.MediaColumns.IS_PENDING, 1)
        }
        val item = resolver.insert(collection, values)
            ?: return Result.fail(FileMindError.io("MediaStore insert failed for $destination"))
        val ok = resolver.openOutputStream(item)?.use { out -> ins.copyTo(out); true } == true
        resolver.update(item, android.content.ContentValues().apply {
            put(MediaStore.MediaColumns.IS_PENDING, 0)
        }, null, null)
        return if (ok) Result.ok(item.toString())
        else Result.fail(FileMindError.io("MediaStore write failed for $destination"))
    }

    override suspend fun rename(source: String, newName: String, conflict: ConflictPolicy)
            : Result<String> = withContext(Dispatchers.IO) {
        // Accept either a bare display name or a full destination URI.
        val name = if (newName.contains("://") || newName.contains('/'))
            normalizer.fileName(newName) else newName
        val info = AndroidUris.classify(source)
        runCatching {
            when (info.kind) {
                UriKind.SafDocument -> {
                    val scoped = scopedDocUri(info.authority!!, info.docId!!,
                        needWrite = true) ?: Uri.parse(source)
                    val renamed = DocumentsContract.renameDocument(resolver, scoped, name)
                    if (renamed != null) Result.ok(renamed.toString())
                    else Result.fail(FileMindError.io("renameDocument returned null for $source"))
                }
                UriKind.MediaStoreRow -> {
                    val values = android.content.ContentValues().apply {
                        put(MediaStore.MediaColumns.DISPLAY_NAME, name)
                    }
                    if (resolver.update(Uri.parse(source), values, null, null) > 0)
                        Result.ok(source)
                    else notFound(source)
                }
                UriKind.AppFile -> {
                    val src = File(info.relPath!!)
                    val dst = File(src.parentFile, name)
                    if (dst.exists()) {
                        when (conflict) {
                            ConflictPolicy.KeepBoth -> {
                                val u = File(uniquifyFilePath(dst.absolutePath))
                                if (src.renameTo(u)) Result.ok("file://${u.absolutePath}")
                                else Result.fail(FileMindError.io("rename failed"))
                            }
                            ConflictPolicy.Skip -> Result.ok(source)
                            else -> Result.fail(FileMindError.conflict("Destination exists: ${dst.absolutePath}"))
                        }
                    } else if (src.renameTo(dst)) Result.ok("file://${dst.absolutePath}")
                    else Result.fail(FileMindError.io("rename failed for $source"))
                }
                else -> Result.fail(FileMindError(
                    ErrorCode.UnsupportedOperation,
                    "rename not supported for $source",
                    retrySafe = false, requiresReview = true))
            }
        }.getOrElse { Result.fail(FileMindError.io("rename failed for $source: ${it.message}")) }
    }

    // ------------------------------------------------------------ capability

    override suspend fun checkCapabilities(path: String): Result<PermissionCapabilities> {
        val held = grantStore.covers(path, needWrite = false)
        return Result.ok(AndroidCapabilityModel.capabilities(path, held))
    }

    override suspend fun getFreeSpace(path: String): Result<Long> = withContext(Dispatchers.IO) {
        runCatching {
            val info = AndroidUris.classify(path)
            val dir: File = when {
                info.kind == UriKind.AppFile -> File(info.relPath!!).let {
                    if (it.isDirectory) it else it.parentFile ?: it
                }
                else -> context.getExternalFilesDir(null) ?: context.filesDir
            }
            Result.ok(StatFs(dir.absolutePath).availableBytes)
        }.getOrElse { Result.fail(FileMindError.io("StatFs failed for $path: ${it.message}")) }
    }

    override suspend fun verify(path: String, expectedFingerprint: String): Result<Boolean> {
        val stat = stat(path)
        if (!stat.isSuccess) return Result.fail(stat.error!!)
        val s = stat.value!!
        val actual = FileIdentity.computeFingerprint(s.sizeBytes, s.modifiedAt)
        return Result.ok(actual == expectedFingerprint)
    }

    override fun getProtectedPaths(): List<String> = protectedPaths.protectedRoots

    // ------------------------------------------------------------ helpers

    private fun docFor(path: String): DocumentFile? {
        val info = AndroidUris.classify(path)
        return when (info.kind) {
            UriKind.SafTree -> DocumentFile.fromTreeUri(context, Uri.parse(path))
            UriKind.SafDocument -> DocumentFile.fromSingleUri(context, Uri.parse(path))
                ?: DocumentFile.fromTreeUri(context, Uri.parse(path))
            else -> null
        }
    }

    private suspend fun docExists(authority: String, docId: String): Boolean =
        runCatching {
            val uri = scopedDocUri(authority, docId, needWrite = false) ?: return false
            resolver.query(uri, arrayOf(DocumentsContract.Document.COLUMN_DOCUMENT_ID),
                null, null, null)?.use { it.moveToFirst() } == true
        }.getOrDefault(false)

    private fun parentDocId(docId: String): String {
        val rel = docId.substringAfter(':', "")
        val vol = docId.substringBefore(':')
        val parent = rel.substringBeforeLast('/', "")
        return if (parent.isEmpty()) "$vol:" else "$vol:$parent"
    }

    private fun docIdOf(uri: String): String = AndroidUris.docIdOf(uri) ?: uri

    /** Deterministic "name (n).ext" suffixing — KeepBoth never overwrites. */
    private suspend fun uniquified(uri: String, authority: String): String {
        val docId = AndroidUris.docIdOf(uri)!!
        var candidate = docId
        var n = 1
        while (docExists(authority, candidate)) {
            candidate = bumpName(docId, n); n++
        }
        return AndroidUris.documentUri(authority, candidate)
    }

    private fun uniquifyFilePath(path: String): String {
        var candidate = path; var n = 1
        while (File(candidate).exists()) { candidate = bumpName(path, n); n++ }
        return candidate
    }

    private fun bumpName(path: String, n: Int): String {
        val slash = path.lastIndexOf('/')
        val dir = if (slash >= 0) path.substring(0, slash + 1) else ""
        val name = if (slash >= 0) path.substring(slash + 1) else path
        val dot = name.lastIndexOf('.')
        return if (dot > 0)
            "$dir${name.substring(0, dot)} ($n)${name.substring(dot)}"
        else "$dir$name ($n)"
    }

    private fun <T> notFound(path: String): Result<T> =
        Result.fail(FileMindError.notFound("Not found or not readable: $path"))
}

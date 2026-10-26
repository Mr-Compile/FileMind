package com.filemind.core.parity

import com.filemind.core.app.CollectionEntry
import com.filemind.core.app.CollectionRepository
import com.filemind.core.app.RuleRepository
import com.filemind.core.contracts.ConflictPolicy
import com.filemind.core.contracts.FileIdentity
import com.filemind.core.contracts.PermissionCapabilities
import com.filemind.core.contracts.Rule
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.core.ports.FileEntry
import com.filemind.core.ports.FileStat
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.OperationStore
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.ports.PathNormalizer
import com.filemind.core.ports.ProtectedPathProvider
import java.io.ByteArrayInputStream
import java.io.InputStream
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow

/** Slash-agnostic fixture normalizer matching WindowsStylePathNormalizer semantics. */
class FixturePathNormalizer : PathNormalizer {
    override fun normalize(path: String): String = path.replace('/', '\\').trimEnd('\\')

    override fun combine(vararg parts: String): String =
        parts.map { it.replace('/', '\\').trim('\\') }
            .filter { it.isNotEmpty() }
            .joinToString("\\")

    override fun isUnder(path: String, root: String): Boolean =
        normalize(path).startsWith(normalize(root).trimEnd('\\') + "\\", ignoreCase = true)

    override fun relativeTo(path: String, root: String): String? {
        val p = normalize(path)
        val r = normalize(root).trimEnd('\\')
        if (p.equals(r, ignoreCase = true)) return ""
        if (!p.startsWith("$r\\", ignoreCase = true)) return null
        return p.substring(r.length + 1)
    }

    override fun fileName(path: String): String = path.split('/', '\\').last()

    override fun directoryOf(path: String): String? {
        val i = path.indexOfLast { it == '/' || it == '\\' }
        return if (i > 0) path.substring(0, i) else null
    }
}

class FixtureProtectedPaths(vararg roots: String) : ProtectedPathProvider {
    private val roots = roots.toList()
    override val protectedRoots: List<String> get() = roots
    override fun isProtected(path: String): Boolean =
        roots.any { path.startsWith(it, ignoreCase = true) }
}

/** Kotlin port of tests/FileMind.Testing/FakeFileSystemAdapter. */
class FakeFileSystemAdapter : FileSystemAdapter {
    private class Node(
        var isDirectory: Boolean = false,
        var content: ByteArray = ByteArray(0),
        var createdAt: Instant = Instant.now(),
        var modifiedAt: Instant = Instant.now(),
        var displayName: String? = null,
        var mimeType: String? = null
    )

    private val map = java.util.TreeMap<String, Node>(String.CASE_INSENSITIVE_ORDER)
    private val protectedPaths = mutableListOf<String>()
    var freeSpace: Long = 1_000_000_000
    var lockPaths: Boolean = false
    val callLog = mutableListOf<String>()

    private fun key(p: String) = p.replace('/', '\\')

    fun seedFile(path: String, content: String = "data", modifiedAt: Instant? = null,
        displayName: String? = null, mimeType: String? = null) {
        map[key(path)] = Node(
            content = content.toByteArray(Charsets.UTF_8),
            modifiedAt = modifiedAt ?: Instant.now(),
            displayName = displayName,
            mimeType = mimeType
        )
        directoryOf(path)?.let { ensureDirectory(it) }
    }

    fun seedDirectory(path: String) = ensureDirectory(path)
    fun protectPath(path: String) = protectedPaths.add(path)
    fun readText(path: String): String? =
        map[key(path)]?.takeIf { !it.isDirectory }?.content?.toString(Charsets.UTF_8)

    private fun ensureDirectory(path: String) {
        if (path.isEmpty() || map.containsKey(key(path))) return
        map[key(path)] = Node(isDirectory = true)
        val parent = directoryOf(path)
        if (parent != null && parent != path) ensureDirectory(parent)
    }

    private fun directoryOf(path: String): String? {
        val i = path.indexOfLast { it == '/' || it == '\\' }
        return when {
            i > 0 -> path.substring(0, i)
            else -> if (i < 0) null else ""
        }
    }

    override fun enumerate(root: String, recursive: Boolean): Flow<FileEntry> = flow {
        callLog.add("enumerate:$root")
        val prefix = key(root).trimEnd('\\') + "\\"
        for ((path, node) in map.toSortedMap(String.CASE_INSENSITIVE_ORDER)) {
            if (node.isDirectory) continue
            if (!path.startsWith(prefix, ignoreCase = true) &&
                !path.equals(key(root).trimEnd('\\'), ignoreCase = true)
            ) continue
            if (!recursive) {
                val rel = path.substring(prefix.length)
                if (rel.contains('\\')) continue
            }
            emit(FileEntry(path, node.content.size.toLong(), node.createdAt, node.modifiedAt,
                displayName = node.displayName, mimeType = node.mimeType))
        }
    }

    override suspend fun stat(path: String): Result<FileStat> {
        callLog.add("stat:$path")
        if (lockPaths) return Result.fail(FileMindError.fileLocked("'$path' locked."))
        val n = map[key(path)] ?: return Result.fail(FileMindError.notFound("'$path' not found."))
        return Result.ok(FileStat(path, n.content.size.toLong(), n.createdAt, n.modifiedAt, n.isDirectory, "fake-${path.hashCode().toString(16)}"))
    }

    override suspend fun openRead(path: String): Result<InputStream> {
        if (lockPaths) return Result.fail(FileMindError.fileLocked("'$path' locked."))
        val n = map[key(path)]
        if (n == null || n.isDirectory) return Result.fail(FileMindError.notFound("'$path' not found."))
        return Result.ok(ByteArrayInputStream(n.content))
    }

    override suspend fun exists(path: String): Result<Boolean> =
        Result.ok(map.containsKey(key(path)))

    override suspend fun createDirectory(path: String): Result<Boolean> {
        callLog.add("mkdir:$path")
        ensureDirectory(path)
        return Result.ok(true)
    }

    override suspend fun move(source: String, destination: String, conflict: ConflictPolicy): Result<String> {
        callLog.add("move:$source->$destination")
        if (lockPaths) return Result.fail(FileMindError.fileLocked("'$source' locked."))
        val n = map[key(source)]
        if (n == null || n.isDirectory) return Result.fail(FileMindError.notFound("'$source' not found."))
        val resolved = resolveConflict(destination, conflict) ?: return Result.ok(source)
        if (!resolved.isSuccess) return Result.fail(resolved.error!!)
        map.remove(key(source))
        map[key(resolved.value!!)] = n
        return Result.ok(resolved.value)
    }

    override suspend fun copy(source: String, destination: String, conflict: ConflictPolicy): Result<String> {
        callLog.add("copy:$source->$destination")
        if (lockPaths) return Result.fail(FileMindError.fileLocked("'$source' locked."))
        val n = map[key(source)]
        if (n == null || n.isDirectory) return Result.fail(FileMindError.notFound("'$source' not found."))
        val resolved = resolveConflict(destination, conflict) ?: return Result.ok(source)
        if (!resolved.isSuccess) return Result.fail(resolved.error!!)
        map[key(resolved.value!!)] = Node(content = n.content.copyOf(), createdAt = n.createdAt)
        return Result.ok(resolved.value)
    }

    override suspend fun rename(source: String, newName: String, conflict: ConflictPolicy): Result<String> {
        callLog.add("rename:$source->$newName")
        if (lockPaths) return Result.fail(FileMindError.fileLocked("'$source' locked."))
        val n = map[key(source)]
        if (n == null || n.isDirectory) return Result.fail(FileMindError.notFound("'$source' not found."))
        val dir = directoryOf(source)
        val destination = if (dir.isNullOrEmpty()) newName else "$dir\\$newName"
        val resolved = resolveConflict(destination, conflict) ?: return Result.ok(source)
        if (!resolved.isSuccess) return Result.fail(resolved.error!!)
        map.remove(key(source))
        map[key(resolved.value!!)] = n
        return Result.ok(resolved.value)
    }

    private fun resolveConflict(destination: String, conflict: ConflictPolicy): Result<String>? {
        if (!map.containsKey(key(destination))) return Result.ok(destination)
        return when (conflict) {
            ConflictPolicy.Skip -> null
            ConflictPolicy.Ask -> Result.fail(FileMindError.conflict("'$destination' exists; user decision required."))
            ConflictPolicy.KeepBoth -> Result.ok(nextFreeName(destination))
            else -> Result.ok(destination)
        }
    }

    private fun nextFreeName(destination: String): String {
        val dir = directoryOf(destination)
        val dot = destination.lastIndexOf('.')
        val name = if (dot > 0) destination.substring(dir?.let { it.length + 1 } ?: 0, dot) else fileNameOf(destination)
        val ext = if (dot > 0) destination.substring(dot) else ""
        var i = 2
        while (true) {
            val candidate = if (dir.isNullOrEmpty()) "$name ($i)$ext" else "$dir\\$name ($i)$ext"
            if (!map.containsKey(key(candidate))) return candidate
            i++
        }
    }

    private fun fileNameOf(path: String) = path.split('/', '\\').last()

    override suspend fun checkCapabilities(path: String): Result<PermissionCapabilities> =
        if (map.containsKey(key(path))) Result.ok(PermissionCapabilities.Default)
        else Result.fail(FileMindError.notFound("'$path' not found."))

    override suspend fun getFreeSpace(path: String): Result<Long> = Result.ok(freeSpace)

    override suspend fun verify(path: String, expectedFingerprint: String): Result<Boolean> {
        callLog.add("verify:$path")
        val n = map[key(path)]
        if (n == null || n.isDirectory) return Result.fail(FileMindError.notFound("'$path' not found."))
        val actual = FileIdentity.computeFingerprint(n.content.size.toLong(), n.modifiedAt)
        return Result.ok(actual == expectedFingerprint)
    }

    override fun getProtectedPaths(): List<String> = protectedPaths
}

class InMemoryOperationStore : OperationStore {
    private val records = ConcurrentHashMap<String, OperationRecord>()

    override suspend fun load(operationId: String): Result<OperationRecord> =
        records[operationId]?.let { Result.ok(it) }
            ?: Result.fail(FileMindError.notFound("Operation '$operationId' not found."))

    override suspend fun save(record: OperationRecord): Result<OperationRecord> {
        records[record.operationId] = record
        return Result.ok(record)
    }

    override suspend fun loadIncomplete(): Result<List<OperationRecord>> =
        Result.ok(records.values.filter { !it.isTerminal })
}

class InMemoryRuleRepository(private var rules: List<Rule> = emptyList()) : RuleRepository {
    override suspend fun getAll(): Result<List<Rule>> = Result.ok(rules)
    override suspend fun get(id: String): Result<Rule> =
        rules.firstOrNull { it.id == id }?.let { Result.ok(it) }
            ?: Result.fail(FileMindError.notFound("Rule '$id' not found."))
    override suspend fun upsert(rule: Rule): Result<Rule> {
        rules = rules.filter { it.id != rule.id } + rule
        return Result.ok(rule)
    }
    override suspend fun delete(id: String): Result<Boolean> {
        val before = rules.size
        rules = rules.filter { it.id != id }
        return Result.ok(rules.size != before)
    }
}

class InMemoryCollectionRepository(
    private var collections: List<CollectionEntry> = emptyList()
) : CollectionRepository {
    override suspend fun getAll(): Result<List<CollectionEntry>> = Result.ok(collections)
    override suspend fun upsert(entry: CollectionEntry): Result<CollectionEntry> {
        collections = collections.filter { it.id != entry.id } + entry
        return Result.ok(entry)
    }
}

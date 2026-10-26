package com.filemind.app.vm

import android.net.Uri
import androidx.compose.runtime.Composable
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.work.WorkInfo
import androidx.work.WorkManager
import com.filemind.app.FileMindApp
import com.filemind.app.theme.ThemeMode
import com.filemind.core.app.AnalyzedFile
import com.filemind.core.app.ModePolicy
import com.filemind.core.app.SmartInboxItem
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OrganizationMode
import com.filemind.core.contracts.Rule
import com.filemind.core.contracts.SafetyResult
import com.filemind.platform.work.WorkContract
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/** Simple factory — one FileMindApp service graph, no DI framework. */
@Composable
inline fun <reified VM : ViewModel> backendViewModel(
    app: FileMindApp,
    crossinline create: (FileMindApp) -> VM
): VM = viewModel(factory = object : ViewModelProvider.Factory {
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        @Suppress("UNCHECKED_CAST")
        return create(app) as T
    }
})

// ------------------------------------------------------------------ settings

class SettingsViewModel(private val app: FileMindApp) : ViewModel() {

    private val _themeMode = MutableStateFlow(ThemeMode.System)
    val themeMode: StateFlow<ThemeMode> = _themeMode

    private val _orgMode = MutableStateFlow(OrganizationMode.Manual)
    val orgMode: StateFlow<OrganizationMode> = _orgMode

    init {
        viewModelScope.launch {
            app.backend.settings.get(KEY_THEME).value?.let { v ->
                _themeMode.value = ThemeMode.entries.firstOrNull { it.name == v } ?: ThemeMode.System
            }
            app.backend.settings.get(ModePolicy.SettingsKey).value?.let { v ->
                ModePolicy.tryParse(v)?.let { _orgMode.value = it }
            }
        }
    }

    fun setTheme(mode: ThemeMode) {
        _themeMode.value = mode
        viewModelScope.launch { app.backend.settings.set(KEY_THEME, mode.name) }
    }

    fun setOrgMode(mode: OrganizationMode) {
        _orgMode.value = mode
        viewModelScope.launch {
            app.backend.settings.set(ModePolicy.SettingsKey, ModePolicy.display(mode))
        }
    }

    companion object { const val KEY_THEME = "ui.theme.mode" }
}

// ---------------------------------------------------------------------- home

data class HomeState(
    val loading: Boolean = true,
    val totalBytes: Long = 0,
    val freeBytes: Long = 0,
    val grantCount: Int = 0,
    val indexedCount: Int = 0,
    val inboxCount: Int = 0,
    val dupePairs: Int = 0,
    val bigFiles: Int = 0,
    val recentOps: List<OperationRecord> = emptyList(),
    val activeOps: List<OperationRecord> = emptyList(),
    val error: String? = null
)

class HomeViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(HomeState())
    val state: StateFlow<HomeState> = _state

    fun refresh() {
        viewModelScope.launch {
            val grants = app.backend.grantStore.revalidate()
            val index = app.backend.fileIndex.getAll().value ?: emptyList()
            val inbox = app.backend.smartInbox.getOpen().value ?: emptyList()
            val recent = app.backend.operationRepository.getRecent(6).value ?: emptyList()
            val incomplete = app.backend.operationRepository.getIncomplete().value ?: emptyList()
            // Cleanup opportunities: same-size groups = likely dupes; >3 GB files.
            val dupePairs = index.groupBy { it.sizeBytes }
                .count { (size, g) -> size > 0 && g.size > 1 }
            val big = index.count { it.sizeBytes > 3_000_000_000L }
            _state.value = HomeState(
                loading = false,
                grantCount = grants.size,
                indexedCount = index.size,
                inboxCount = inbox.size,
                dupePairs = dupePairs,
                bigFiles = big,
                recentOps = recent,
                activeOps = incomplete
            )
        }
    }

    init { refresh() }
}

// --------------------------------------------------------------------- files

data class FilesState(
    val loading: Boolean = true,
    val files: List<FileCandidate> = emptyList(),
    val selected: Set<String> = emptySet(),
    val grantedTrees: List<String> = emptyList(),
    val error: String? = null
)

class FilesViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(FilesState())
    val state: StateFlow<FilesState> = _state

    fun refresh() {
        viewModelScope.launch {
            val result = app.backend.fileIndex.getAll()
            _state.value = if (result.isSuccess) {
                FilesState(loading = false, files = result.value ?: emptyList())
            } else FilesState(loading = false, error = result.error?.message)
        }
    }

    /** Rescan a source URI and upsert results into the index. */
    fun rescan(rootUri: String) {
        viewModelScope.launch {
            _state.value = _state.value.copy(loading = true)
            try {
                val pipeline = app.backend.pipeline()
                val found = mutableListOf<FileCandidate>()
                pipeline.scan(rootUri).collect { found.add(it) }
                pipeline.index(found)
                refresh()
            } catch (e: Exception) {
                if (e is kotlinx.coroutines.CancellationException) throw e
                _state.value = _state.value.copy(
                    loading = false, error = e.message ?: "Rescan failed")
            }
        }
    }

    fun grantedTrees(): List<String> = _grants
    private var _grants: List<String> = emptyList()
    fun refreshGrants() {
        viewModelScope.launch {
            _grants = app.backend.grantStore.revalidate().map { it.treeUri }
            _state.value = _state.value.copy(grantedTrees = _grants)
        }
    }

    fun toggle(uri: String) {
        _state.value = _state.value.let { s ->
            s.copy(selected = if (uri in s.selected) s.selected - uri else s.selected + uri)
        }
    }

    fun clearSelection() { _state.value = _state.value.copy(selected = emptySet()) }

    init { refresh(); refreshGrants() }
}

// ------------------------------------------------------------------- devices

data class DevicesState(
    val loading: Boolean = true,
    val model: String = "",
    val sdk: Int = 0,
    val totalBytes: Long = 0,
    val freeBytes: Long = 0,
    val grants: List<com.filemind.platform.db.UriGrantEntity> = emptyList()
)

class DevicesViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(DevicesState())
    val state: StateFlow<DevicesState> = _state

    fun refresh() {
        viewModelScope.launch {
            val grants = app.backend.grantStore.revalidate()
            val dir = app.getExternalFilesDir(null) ?: app.filesDir
            val stat = android.os.StatFs(dir.absolutePath)
            _state.value = DevicesState(
                loading = false,
                model = "${android.os.Build.MANUFACTURER} ${android.os.Build.MODEL}",
                sdk = android.os.Build.VERSION.SDK_INT,
                totalBytes = stat.totalBytes,
                freeBytes = stat.availableBytes,
                grants = grants
            )
        }
    }

    fun revoke(treeUri: String) {
        viewModelScope.launch {
            app.backend.grantStore.release(Uri.parse(treeUri))
            refresh()
        }
    }

    init { refresh() }
}

// ------------------------------------------------------------------ activity

data class ActivityState(
    val loading: Boolean = true,
    val active: List<OperationRecord> = emptyList(),
    val recent: List<OperationRecord> = emptyList(),
    val inbox: List<SmartInboxItem> = emptyList(),
    val busyUndo: Boolean = false
)

class ActivityViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(ActivityState())
    val state: StateFlow<ActivityState> = _state

    fun refresh() {
        viewModelScope.launch {
            _state.value = ActivityState(
                loading = false,
                active = app.backend.operationRepository.getIncomplete().value ?: emptyList(),
                recent = app.backend.operationRepository.getRecent(30).value ?: emptyList(),
                inbox = app.backend.smartInbox.getOpen().value ?: emptyList()
            )
        }
    }

    fun resolveInbox(id: String) {
        viewModelScope.launch {
            app.backend.smartInbox.resolve(id)
            refresh()
        }
    }

    fun undo(operationId: String) {
        viewModelScope.launch {
            _state.value = _state.value.copy(busyUndo = true)
            app.backend.pipeline().undo(operationId)
            _state.value = _state.value.copy(busyUndo = false)
            refresh()
        }
    }

    /** Live WorkManager info for scan/organize jobs — feeds the Task Center. */
    fun workInfos() = WorkManager.getInstance(app)
        .getWorkInfosByTagFlow(WorkContract.TAG_SCAN)
        .map { list -> list.filter { it.state == WorkInfo.State.RUNNING || it.state == WorkInfo.State.ENQUEUED } }

    init { refresh() }
}

// --------------------------------------------------------------------- tools

data class ToolsState(
    val loading: Boolean = true,
    val rules: List<Rule> = emptyList(),
    val indexedCount: Int = 0,
    val inboxCount: Int = 0,
    val kindBreakdown: Map<String, Int> = emptyMap(),
    val bytesByKind: Map<String, Long> = emptyMap(),
    val largest: List<FileCandidate> = emptyList()
)

class ToolsViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(ToolsState())
    val state: StateFlow<ToolsState> = _state

    fun refresh() {
        viewModelScope.launch {
            val index = app.backend.fileIndex.getAll().value ?: emptyList()
            fun bucket(c: FileCandidate) = when {
                c.mimeType?.startsWith("image/") == true -> "Images"
                c.mimeType?.startsWith("video/") == true -> "Video"
                c.mimeType?.startsWith("audio/") == true -> "Audio"
                c.mimeType?.startsWith("text/") == true -> "Docs"
                else -> "Other"
            }
            _state.value = ToolsState(
                loading = false,
                rules = app.backend.rules.getAll().value ?: emptyList(),
                indexedCount = index.size,
                inboxCount = app.backend.smartInbox.getOpen().value?.size ?: 0,
                kindBreakdown = index.groupingBy(::bucket).eachCount(),
                bytesByKind = index.groupBy(::bucket)
                    .mapValues { (_, g) -> g.sumOf { it.sizeBytes } },
                largest = index.sortedByDescending { it.sizeBytes }.take(10)
            )
        }
    }

    fun toggleRule(rule: Rule) {
        viewModelScope.launch {
            app.backend.rules.upsert(rule.copy(enabled = !rule.enabled))
            refresh()
        }
    }

    init { refresh() }
}

// -------------------------------------------------------------------- search

data class SearchState(
    val loading: Boolean = true,
    val all: List<FileCandidate> = emptyList(),
    val query: String = "",
    val kind: String = "",
    val ext: String = "",
    val scope: String = "",          // "" = everything, else a source-URI prefix
    val minSize: Long = 0            // bytes; 0 = any size
) {
    val results: List<FileCandidate>
        get() = all.filter { c ->
            (query.isBlank() || c.displayName.contains(query, true) ||
                c.sourceUri.contains(query, true)) &&
                (kind.isBlank() || c.mimeType?.startsWith(kind, true) == true ||
                    c.platformHints["kind"] == kind) &&
                (ext.isBlank() || c.extension.equals(ext, true) ||
                    c.extension.equals(".$ext", true)) &&
                (scope.isBlank() || c.sourceUri.startsWith(scope)) &&
                c.sizeBytes >= minSize
        }
}

class SearchViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(SearchState())
    val state: StateFlow<SearchState> = _state

    private var _grants: List<String> = emptyList()
    fun grantedTrees() = _grants

    fun refresh() {
        viewModelScope.launch {
            _grants = app.backend.grantStore.revalidate().map { it.treeUri }
            _state.value = _state.value.copy(
                loading = false,
                all = app.backend.fileIndex.getAll().value ?: emptyList()
            )
        }
    }

    fun update(
        query: String? = null, kind: String? = null, ext: String? = null,
        scope: String? = null, minSize: Long? = null
    ) {
        _state.value = _state.value.copy(
            query = query ?: _state.value.query,
            kind = kind ?: _state.value.kind,
            ext = ext ?: _state.value.ext,
            scope = scope ?: _state.value.scope,
            minSize = minSize ?: _state.value.minSize
        )
    }

    init { refresh() }
}

// ----------------------------------------------------------------- organizer

sealed interface OrgStep {
    data object Access : OrgStep
    data object Source : OrgStep
    data class Scanning(val rootLabel: String, val found: Int = 0) : OrgStep
    data class Preview(
        val analyzed: List<AnalyzedFile>,
        val plan: ActionPlan,
        val safety: SafetyResult
    ) : OrgStep
    data class Executing(
        val plan: ActionPlan,
        val label: String = "Executing",
        val stage: String = "Executing",
        val done: Int = 0,
        val log: List<String> = emptyList()
    ) : OrgStep
    data class Done(val record: OperationRecord, val wasDryRun: Boolean = false) : OrgStep
    data class Failed(val title: String, val detail: String) : OrgStep
}

data class OrgState(
    val step: OrgStep = OrgStep.Access,
    val grantedTrees: List<String> = emptyList(),
    val sourceUri: String? = null,
    val sourceLabel: String? = null,
    val dryRun: Boolean = true,
    val preserveTree: Boolean = false,
    val busy: Boolean = false,
    val error: String? = null
)

class OrganizerViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(OrgState())
    val state: StateFlow<OrgState> = _state
    private var runJob: Job? = null
    private var lastRecord: OperationRecord? = null

    fun refreshGrants() {
        viewModelScope.launch {
            val grants = app.backend.grantStore.revalidate()
            val trees = grants.map { it.treeUri }
            _state.value = _state.value.copy(
                grantedTrees = trees,
                // Returning users with grants skip the access explainer.
                step = if (_state.value.step == OrgStep.Access && trees.isNotEmpty())
                    OrgStep.Source else _state.value.step
            )
        }
    }

    fun dismissAccess() {
        _state.value = _state.value.copy(step = OrgStep.Source)
    }

    fun setDryRun(v: Boolean) { _state.value = _state.value.copy(dryRun = v) }
    fun setPreserveTree(v: Boolean) { _state.value = _state.value.copy(preserveTree = v) }

    /** Persist the SAF grant returned by OpenDocumentTree, then use it. */
    fun onTreePicked(treeUri: Uri, flags: Int) {
        viewModelScope.launch {
            val held = app.backend.grantStore.take(treeUri, flags)
            if (!held) {
                _state.value = _state.value.copy(step = OrgStep.Failed(
                    "Folder access was not kept",
                    "Android could not persist permission for this folder. " +
                        "Pick it again, or choose a different folder."))
                return@launch
            }
            refreshGrants()
            startScan(treeUri.toString())
        }
    }

    /** Scan + analyze a source URI (SAF tree string or app-private path). */
    fun startScan(rootUri: String, label: String = rootUri) {
        runJob?.cancel()
        _state.value = _state.value.copy(
            step = OrgStep.Scanning(label), sourceUri = rootUri,
            sourceLabel = label, error = null)
        runJob = viewModelScope.launch {
            try {
                val pipeline = app.backend.pipeline()
                val candidates = mutableListOf<FileCandidate>()
                pipeline.scan(rootUri).collect { c ->
                    candidates.add(c)
                    if (candidates.size % 10 == 0) {
                        _state.value = _state.value.copy(
                            step = OrgStep.Scanning(label, candidates.size))
                    }
                }
                if (candidates.isEmpty()) {
                    _state.value = _state.value.copy(step = OrgStep.Source, error =
                        "No files found in this source. Pick a folder that contains files.")
                    return@launch
                }
                pipeline.index(candidates)
                val analyzed = pipeline.analyze(candidates)
                // Route Review decisions into the Smart Inbox — mirrors
                // ScanWorker's write-back so foreground scans feed the inbox.
                analyzed.value?.forEach { a ->
                    if (a.decision.kind == com.filemind.core.contracts.DecisionKind.Review) {
                        app.backend.smartInbox.add(SmartInboxItem(
                            id = a.candidate.scanId,
                            path = a.candidate.sourceUri,
                            reason = a.decision.reasons.joinToString("; "),
                            classification = a.classification,
                            at = java.time.Instant.now(),
                            resolved = false))
                    }
                }
                if (analyzed.isSuccess) {
                    buildPlan(analyzed.value!!)
                } else {
                    _state.value = _state.value.copy(step = OrgStep.Failed(
                        "Analysis failed", analyzed.error?.message ?: "unknown"))
                }
            } catch (e: Exception) {
                if (e is kotlinx.coroutines.CancellationException) throw e
                _state.value = _state.value.copy(step = OrgStep.Failed(
                    "Scan failed", e.message ?: e.javaClass.simpleName))
            }
        }
    }

    /** Build plan + safety pre-check for the analyzed set. */
    fun buildPlan(analyzed: List<AnalyzedFile>) {
        viewModelScope.launch {
            val pipeline = app.backend.pipeline()
            val src = _state.value.sourceUri
            // Mockup parity: moves land under <source>/organized/<template>.
            val destRoot = src?.let { app.backend.paths.combine(it, "organized") }
            val plan = pipeline.plan(
                analyzed,
                dryRun = _state.value.dryRun,
                destinationRoot = destRoot,
                sourceRoot = src,
                preserveTree = _state.value.preserveTree)
            if (!plan.isSuccess) {
                _state.value = _state.value.copy(step = OrgStep.Failed(
                    "Planning failed", plan.error?.message ?: "unknown"))
                return@launch
            }
            val safety = pipeline.dryRun(plan.value!!)
            _state.value = _state.value.copy(step = OrgStep.Preview(
                analyzed, plan.value!!, safety.value
                    ?: SafetyResult.block("Safety check failed.")))
        }
    }

    fun execute(plan: ActionPlan) {
        _state.value = _state.value.copy(step = OrgStep.Executing(plan))
        runJob = viewModelScope.launch {
            // Live progress: TransactionEngine persists the record per item,
            // so polling getIncomplete() yields real committed/total state.
            val poller = launch {
                while (true) {
                    kotlinx.coroutines.delay(250)
                    val live = app.backend.operationRepository.getIncomplete().value
                        ?.firstOrNull { it.planId == plan.planId } ?: continue
                    val done = live.items.count {
                        it.status == com.filemind.core.contracts.OperationStatus.Committed ||
                            it.status == com.filemind.core.contracts.OperationStatus.Failed ||
                            it.status == com.filemind.core.contracts.OperationStatus.NeedsAttention
                    }
                    val log = live.items.takeLast(4).map {
                        "${displayNameOf(it.originalLocation)} — ${it.status.name}"
                    }
                    val s = _state.value
                    if (s.step is OrgStep.Executing) {
                        _state.value = s.copy(step = OrgStep.Executing(
                            plan, stage = live.status.name, done = done, log = log))
                    }
                }
            }
            try {
                val result = app.backend.pipeline().execute(plan)
                poller.cancel()
                if (result.isSuccess) {
                    lastRecord = result.value
                    _state.value = _state.value.copy(step =
                        OrgStep.Done(result.value!!, wasDryRun = plan.dryRun))
                } else {
                    _state.value = _state.value.copy(step = OrgStep.Failed(
                        "Execution failed", result.error?.message ?: "unknown"))
                }
            } catch (e: Exception) {
                poller.cancel()
                if (e is kotlinx.coroutines.CancellationException) {
                    finalizeCancelled(plan)
                    _state.value = _state.value.copy(step = OrgStep.Source,
                        error = "Operation cancelled.")
                } else {
                    _state.value = _state.value.copy(step = OrgStep.Failed(
                        "Execution failed", e.message ?: e.javaClass.simpleName))
                }
            }
        }
    }

    /** Cancelled mid-run records must reach a terminal state — otherwise they
     *  linger in getIncomplete() as ghost "active" operations. */
    private suspend fun finalizeCancelled(plan: ActionPlan) {
        val live = app.backend.operationRepository.getIncomplete().value
            ?.firstOrNull { it.planId == plan.planId } ?: return
        app.backend.operationRepository.save(live.copy(
            status = com.filemind.core.contracts.OperationStatus.NeedsAttention,
            error = com.filemind.core.errors.FileMindError.validation(
                "Cancelled partway through; some items may have moved already.")))
    }

    private fun displayNameOf(uri: String): String {
        val docId = runCatching {
            com.filemind.platform.uri.AndroidUris.docIdOf(uri)
        }.getOrNull()
        return (docId ?: uri.substringAfterLast('/')).substringAfterLast('/')
    }

    fun cancel() {
        runJob?.cancel()
        _state.value = _state.value.copy(step = OrgStep.Source, error = "Cancelled.")
    }

    fun undo(operationId: String) {
        viewModelScope.launch {
            val result = app.backend.pipeline().undo(operationId)
            if (result.isSuccess) {
                lastRecord = result.value
                _state.value = _state.value.copy(step = OrgStep.Done(result.value!!))
            } else {
                _state.value = _state.value.copy(error =
                    result.error?.message ?: "Undo failed")
            }
        }
    }

    fun reset() {
        runJob?.cancel()
        _state.value = OrgState(
            step = OrgStep.Source,
            grantedTrees = _state.value.grantedTrees,
            dryRun = _state.value.dryRun,
            preserveTree = _state.value.preserveTree)
    }

    init { refreshGrants() }
}

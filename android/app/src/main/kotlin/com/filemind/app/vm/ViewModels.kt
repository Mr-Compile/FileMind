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
            _state.value = HomeState(
                loading = false,
                grantCount = grants.size,
                indexedCount = index.size,
                inboxCount = inbox.size,
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

    fun toggle(uri: String) {
        _state.value = _state.value.let { s ->
            s.copy(selected = if (uri in s.selected) s.selected - uri else s.selected + uri)
        }
    }

    fun clearSelection() { _state.value = _state.value.copy(selected = emptySet()) }

    init { refresh() }
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

data class ToolsState(val loading: Boolean = true, val rules: List<Rule> = emptyList())

class ToolsViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(ToolsState())
    val state: StateFlow<ToolsState> = _state

    init {
        viewModelScope.launch {
            _state.value = ToolsState(
                loading = false,
                rules = app.backend.rules.getAll().value ?: emptyList()
            )
        }
    }
}

// -------------------------------------------------------------------- search

data class SearchState(
    val loading: Boolean = true,
    val all: List<FileCandidate> = emptyList(),
    val query: String = "",
    val kind: String = "",
    val ext: String = ""
) {
    val results: List<FileCandidate>
        get() = all.filter { c ->
            (query.isBlank() || c.displayName.contains(query, true) ||
                c.sourceUri.contains(query, true)) &&
                (kind.isBlank() || c.mimeType?.startsWith(kind, true) == true ||
                    c.platformHints["kind"] == kind) &&
                (ext.isBlank() || c.extension.equals(ext, true) ||
                    c.extension.equals(".$ext", true))
        }
}

class SearchViewModel(private val app: FileMindApp) : ViewModel() {
    private val _state = MutableStateFlow(SearchState())
    val state: StateFlow<SearchState> = _state

    init {
        viewModelScope.launch {
            _state.value = _state.value.copy(
                loading = false,
                all = app.backend.fileIndex.getAll().value ?: emptyList()
            )
        }
    }

    fun update(query: String? = null, kind: String? = null, ext: String? = null) {
        _state.value = _state.value.copy(
            query = query ?: _state.value.query,
            kind = kind ?: _state.value.kind,
            ext = ext ?: _state.value.ext
        )
    }
}

// ----------------------------------------------------------------- organizer

sealed interface OrgStep {
    data object Source : OrgStep
    data class Scanning(val rootLabel: String, val found: Int = 0) : OrgStep
    data class Review(val analyzed: List<AnalyzedFile>) : OrgStep
    data class Preview(
        val analyzed: List<AnalyzedFile>,
        val plan: ActionPlan,
        val safety: SafetyResult
    ) : OrgStep
    data class Executing(val plan: ActionPlan, val label: String = "Executing") : OrgStep
    data class Done(val record: OperationRecord) : OrgStep
    data class Failed(val title: String, val detail: String) : OrgStep
}

data class OrgState(
    val step: OrgStep = OrgStep.Source,
    val grantedTrees: List<String> = emptyList(),
    val sourceUri: String? = null,
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
            _state.value = _state.value.copy(
                grantedTrees = grants.map { it.treeUri }
            )
        }
    }

    /** Persist the SAF grant returned by OpenDocumentTree, then use it. */
    fun onTreePicked(treeUri: Uri, flags: Int) {
        viewModelScope.launch {
            app.backend.grantStore.take(treeUri, flags)
            refreshGrants()
            startScan(treeUri.toString())
        }
    }

    /** Scan + analyze a source URI (SAF tree string or app-private path). */
    fun startScan(rootUri: String, label: String = rootUri) {
        runJob?.cancel()
        _state.value = _state.value.copy(
            step = OrgStep.Scanning(label), sourceUri = rootUri, error = null)
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
                if (analyzed.isSuccess) {
                    _state.value = _state.value.copy(
                        step = OrgStep.Review(analyzed.value!!))
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
            val plan = pipeline.plan(analyzed, dryRun = false, sourceRoot = _state.value.sourceUri)
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
            try {
                val result = app.backend.pipeline().execute(plan)
                if (result.isSuccess) {
                    lastRecord = result.value
                    _state.value = _state.value.copy(step = OrgStep.Done(result.value!!))
                } else {
                    _state.value = _state.value.copy(step = OrgStep.Failed(
                        "Execution failed", result.error?.message ?: "unknown"))
                }
            } catch (e: Exception) {
                if (e is kotlinx.coroutines.CancellationException) {
                    _state.value = _state.value.copy(step = OrgStep.Source,
                        error = "Operation cancelled.")
                } else {
                    _state.value = _state.value.copy(step = OrgStep.Failed(
                        "Execution failed", e.message ?: e.javaClass.simpleName))
                }
            }
        }
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
        _state.value = OrgState(grantedTrees = _state.value.grantedTrees)
    }

    init { refreshGrants() }
}

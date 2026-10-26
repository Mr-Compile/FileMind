package com.filemind.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.DriveFileMove
import androidx.compose.material.icons.filled.DriveFileRenameOutline
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.FolderOpen
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.FilterButton
import com.filemind.app.components.FilterGroup
import com.filemind.app.components.FilterOption
import com.filemind.app.components.FilterSheet
import com.filemind.app.components.ListRow
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.LocalSnackbar
import com.filemind.app.components.PickerOption
import com.filemind.app.components.PickerSheet
import com.filemind.app.components.SearchField
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.components.fileKindIcon
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.app.vm.FilesViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.FileCandidate
import kotlinx.coroutines.launch

private val KIND_OPTIONS = listOf(
    FilterOption("", "All"), FilterOption("image/", "Images"),
    FilterOption("video/", "Video"), FilterOption("audio/", "Audio"),
    FilterOption("text/", "Docs")
)
private val SORT_OPTIONS = listOf(
    FilterOption("name", "Name"), FilterOption("size", "Size"),
    FilterOption("modified", "Recently modified")
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FilesScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { FilesViewModel(it) }
    val state by vm.state.collectAsState()
    val snackbar = LocalSnackbar.current
    val scope = rememberCoroutineScope()

    var query by remember { mutableStateOf("") }
    var kindFilter by remember { mutableStateOf("") }
    var sortBy by remember { mutableStateOf("name") }
    var scopeUri by remember { mutableStateOf("") }   // "" = All indexed files
    var inspecting by remember { mutableStateOf<FileCandidate?>(null) }
    var showFilters by remember { mutableStateOf(false) }
    var showScopePicker by remember { mutableStateOf(false) }
    var showSelActions by remember { mutableStateOf(false) }
    var renaming by remember { mutableStateOf<FileCandidate?>(null) }

    // Refresh the index whenever the screen resumes.
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) vm.refresh()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }

    val scopeLabel = if (scopeUri.isBlank()) "All indexed files"
    else shortPath(scopeUri, 36)

    Box(Modifier.fillMaxSize()) {
        Column(Modifier.fillMaxSize().padding(horizontal = Spacing.s4)) {
            if (state.selected.isNotEmpty()) {
                // Contextual selection bar — mockup s-files selection state
                Row(verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.padding(vertical = Spacing.s1)) {
                    IconButton(onClick = { vm.clearSelection() }) {
                        Icon(Icons.Filled.Close, contentDescription = "Clear selection")
                    }
                    Text("${state.selected.size} selected",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.weight(1f))
                    FilledTonalButton(onClick = { showSelActions = true }) {
                        Text("Actions")
                    }
                }
            }

            // Scope row — mockup: folder picker chip + rescan icon
            Row(verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                FilledTonalButton(onClick = { showScopePicker = true },
                    modifier = Modifier.weight(1f)) {
                    Icon(Icons.Filled.Folder, null)
                    Spacer(Modifier.width(Spacing.s2))
                    Text(scopeLabel, maxLines = 1)
                }
                IconButton(
                    enabled = scopeUri.isNotBlank() && !state.loading,
                    onClick = {
                        vm.rescan(scopeUri)
                        scope.launch { snackbar.showSnackbar("Rescanning folder…") }
                    }) {
                    Icon(Icons.Filled.Refresh, contentDescription = "Rescan folder")
                }
            }
            Spacer(Modifier.height(Spacing.s2))

            // Search pill + icon filter button
            Row(verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                SearchField(value = query, onValueChange = { query = it },
                    placeholder = "Search files",
                    modifier = Modifier.weight(1f))
                val activeFilters = (if (kindFilter.isNotEmpty()) 1 else 0) +
                    (if (sortBy != "name") 1 else 0)
                FilterButton(activeFilters, onClick = { showFilters = true })
            }
            Spacer(Modifier.height(Spacing.s2))

            when {
                state.loading -> LoadingBox("Loading index")
                state.error != null -> EmptyState(
                    "Index unavailable",
                    state.error ?: "The file index could not be read.",
                    "Retry", { vm.refresh() })
                else -> {
                    val shown = state.files
                        .filter {
                            (scopeUri.isBlank() || it.sourceUri.startsWith(scopeUri)) &&
                                (query.isBlank() ||
                                    it.displayName.contains(query, true)) &&
                                (kindFilter.isBlank() ||
                                    it.mimeType?.startsWith(kindFilter) == true)
                        }
                        .sortedWith(
                            when (sortBy) {
                                "size" -> compareByDescending { it.sizeBytes }
                                "modified" -> compareByDescending { it.modifiedAt }
                                else -> compareBy(String.CASE_INSENSITIVE_ORDER) {
                                    it.displayName }
                            })
                    if (shown.isEmpty()) {
                        EmptyState(
                            title = "No indexed files",
                            body = if (state.files.isEmpty())
                                "Nothing has been scanned yet. Organize a folder to build the index."
                            else "No files match this filter.",
                            actionLabel = "Open organizer",
                            onAction = { nav.navigate(Routes.Organizer) })
                    } else {
                        LazyColumn {
                            items(shown, key = { it.sourceUri }) { file ->
                                val selected = file.sourceUri in state.selected
                                ListRow(
                                    title = file.displayName,
                                    subtitle = "${formatBytes(file.sizeBytes)} · " +
                                        "${file.extension} · ${formatInstant(file.modifiedAt)}",
                                    icon = {
                                        Icon(fileKindIcon(file.extension), null,
                                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                                    },
                                    trailing = {
                                        if (selected) {
                                            Icon(Icons.Filled.CheckCircle, "Selected",
                                                tint = MaterialTheme.colorScheme.primary)
                                        } else {
                                            StatusChip(chipSpec("info",
                                                file.extension.uppercase().ifBlank { "FILE" },
                                                fileKindIcon(file.extension)))
                                        }
                                    },
                                    selected = selected,
                                    onClick = { inspecting = file },
                                    onLongClick = { vm.toggle(file.sourceUri) })
                            }
                        }
                    }
                }
            }
        }

        ExtendedFloatingActionButton(
            onClick = { nav.navigate(Routes.Organizer) },
            icon = { Icon(Icons.Filled.DriveFileMove, null) },
            text = { Text("Organize") },
            modifier = Modifier.align(Alignment.BottomEnd).padding(Spacing.s4))
    }

    if (showFilters) {
        FilterSheet(
            title = "Filter files",
            groups = listOf(
                FilterGroup("Kind", "kind", KIND_OPTIONS),
                FilterGroup("Sort by", "sort", SORT_OPTIONS)
            ),
            selected = mapOf("kind" to kindFilter, "sort" to sortBy),
            onSelect = { key, v -> if (key == "kind") kindFilter = v else sortBy = v },
            onClear = { kindFilter = ""; sortBy = "name" },
            onDismiss = { showFilters = false }
        )
    }

    if (showScopePicker) {
        PickerSheet(
            title = "Folder scope",
            options = listOf(
                PickerOption("", "All indexed files", "Everything FileMind has scanned")
            ) + state.grantedTrees.map {
                PickerOption(it, shortPath(it, 40), "Granted folder")
            },
            selected = scopeUri,
            onPick = { scopeUri = it },
            onDismiss = { showScopePicker = false })
    }

    // Selection actions — mockup s-files selection sheet
    if (showSelActions && state.selected.isNotEmpty()) {
        ModalBottomSheet(onDismissRequest = { showSelActions = false }) {
            Column(Modifier.padding(Spacing.s4)) {
                Text("${state.selected.size} selected",
                    style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s3))
                listOf(
                    Triple("move", "Plan a move", Icons.Filled.DriveFileMove),
                    Triple("rename", "Rename…", Icons.Filled.DriveFileRenameOutline),
                    Triple("inbox", "Send to Smart Inbox", Icons.Filled.Badge)
                ).forEach { (verb, label, icon) ->
                    ListRow(
                        title = label,
                        icon = { Icon(icon, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                        onClick = {
                            showSelActions = false
                            scope.launch {
                                snackbar.showSnackbar(
                                    when (verb) {
                                        "move" -> "Use the Organizer to move files."
                                        "rename" -> "Rename one file at a time — tap a row."
                                        else -> "Smart Inbox is filled during scans."
                                    })
                            }
                        })
                }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }

    inspecting?.let { file ->
        ModalBottomSheet(onDismissRequest = { inspecting = null }) {
            Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
                Text(file.displayName, style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s1))
                Text(shortPath(file.sourceUri, 80),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))
                // Inspector action row — mockup s-files
                Row(horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                    FilledTonalButton(onClick = {
                        inspecting = null
                        try {
                            app.startActivity(
                                android.content.Intent(android.content.Intent.ACTION_VIEW)
                                    .setData(android.net.Uri.parse(file.sourceUri))
                                    .addFlags(android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION))
                        } catch (e: Exception) {
                            scope.launch { snackbar.showSnackbar("No app can open this file.") }
                        }
                    }) { Text("Open") }
                    FilledTonalButton(onClick = {
                        inspecting = null
                        renaming = file
                    }) { Text("Rename") }
                    FilledTonalButton(onClick = {
                        inspecting = null
                        vm.toggle(file.sourceUri)
                    }) { Text("Select") }
                }
                Spacer(Modifier.height(Spacing.s3))
                Text("Size: ${formatBytes(file.sizeBytes)}")
                Text("Type: ${file.mimeType ?: file.extension}")
                Text("Modified: ${formatInstant(file.modifiedAt)}")
                file.metadata.forEach { (k, v) -> Text("$k: $v") }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }

    renaming?.let { file ->
        RenameDialog(
            current = file.displayName,
            onRename = { newName ->
                renaming = null
                scope.launch {
                    snackbar.showSnackbar("Rename runs inside an organizer plan — coming with file actions.")
                }
            },
            onDismiss = { renaming = null })
    }
}

@Composable
private fun RenameDialog(
    current: String,
    onRename: (String) -> Unit,
    onDismiss: () -> Unit
) {
    var name by remember { mutableStateOf(current) }
    androidx.compose.material3.AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Rename file") },
        text = {
            androidx.compose.material3.OutlinedTextField(
                value = name, onValueChange = { name = it },
                singleLine = true, modifier = Modifier.fillMaxWidth())
        },
        confirmButton = {
            androidx.compose.material3.TextButton(
                onClick = { if (name.isNotBlank()) onRename(name.trim()) }) {
                Text("Rename")
            }
        },
        dismissButton = {
            androidx.compose.material3.TextButton(onClick = onDismiss) { Text("Cancel") }
        })
}

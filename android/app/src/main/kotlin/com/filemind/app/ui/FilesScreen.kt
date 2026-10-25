package com.filemind.app.ui

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.FilterButton
import com.filemind.app.components.FilterGroup
import com.filemind.app.components.FilterOption
import com.filemind.app.components.FilterSheet
import com.filemind.app.components.LoadingBox
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.FilesViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.FileCandidate

private val KIND_OPTIONS = listOf(
    FilterOption("", "All"), FilterOption("image/", "Images"),
    FilterOption("video/", "Video"), FilterOption("audio/", "Audio"),
    FilterOption("text/", "Docs")
)
private val SORT_OPTIONS = listOf(
    FilterOption("name", "Name"), FilterOption("size", "Size"),
    FilterOption("modified", "Recently modified")
)

@OptIn(ExperimentalFoundationApi::class, ExperimentalMaterial3Api::class)
@Composable
fun FilesScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { FilesViewModel(it) }
    val state by vm.state.collectAsState()

    var query by remember { mutableStateOf("") }
    var kindFilter by remember { mutableStateOf("") }
    var sortBy by remember { mutableStateOf("name") }
    var inspecting by remember { mutableStateOf<FileCandidate?>(null) }
    var showFilters by remember { mutableStateOf(false) }

    Column(Modifier.fillMaxSize().padding(Spacing.s4)) {
        if (state.selected.isNotEmpty()) {
            // Contextual selection bar (spec: long-press selection app bar)
            Row(verticalAlignment = Alignment.CenterVertically) {
                IconButton(onClick = { vm.clearSelection() }) {
                    Icon(Icons.Filled.Close, contentDescription = "Clear selection")
                }
                Text("${state.selected.size} selected",
                    style = MaterialTheme.typography.titleMedium,
                    modifier = Modifier.weight(1f))
            }
            Spacer(Modifier.height(Spacing.s2))
        }

        OutlinedTextField(
            value = query, onValueChange = { query = it },
            label = { Text("Filter by name") },
            modifier = Modifier.fillMaxWidth(), singleLine = true
        )
        Spacer(Modifier.height(Spacing.s2))

        /* Filters live in a bottom sheet — no horizontally scrolling chips. */
        val activeFilters = (if (kindFilter.isNotEmpty()) 1 else 0) + (if (sortBy != "name") 1 else 0)
        Row(
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(Spacing.s2)
        ) {
            FilterButton(activeFilters, onClick = { showFilters = true })
            Text(
                listOfNotNull(
                    kindFilter.takeIf { it.isNotEmpty() }
                        ?.let { KIND_OPTIONS.firstOrNull { o -> o.value == it }?.label },
                    "Sort: ${SORT_OPTIONS.firstOrNull { it.value == sortBy }?.label ?: "Name"}"
                ).joinToString(" · "),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
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
                        (query.isBlank() || it.displayName.contains(query, true)) &&
                            (kindFilter.isBlank() || it.mimeType?.startsWith(kindFilter) == true)
                    }
                    .sortedWith(
                        when (sortBy) {
                            "size" -> compareByDescending { it.sizeBytes }
                            "modified" -> compareByDescending { it.modifiedAt }
                            else -> compareBy(String.CASE_INSENSITIVE_ORDER) { it.displayName }
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
                    LazyColumn(verticalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                        items(shown, key = { it.sourceUri }) { file ->
                            val selected = file.sourceUri in state.selected
                            Card(
                                Modifier.fillMaxWidth().combinedClickable(
                                    onClick = { inspecting = file },
                                    onLongClick = { vm.toggle(file.sourceUri) }
                                )
                            ) {
                                Row(Modifier.padding(Spacing.s3),
                                    verticalAlignment = Alignment.CenterVertically) {
                                    Column(Modifier.weight(1f)) {
                                        Text(file.displayName,
                                            style = MaterialTheme.typography.bodyLarge,
                                            maxLines = 1)
                                        Text("${formatBytes(file.sizeBytes)} · ${file.extension} · ${formatInstant(file.modifiedAt)}",
                                            style = MaterialTheme.typography.labelMedium,
                                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                    if (selected) {
                                        Icon(Icons.Filled.CheckCircle,
                                            contentDescription = "Selected",
                                            tint = MaterialTheme.colorScheme.primary)
                                    } else {
                                        Icon(Icons.Filled.Info, contentDescription = "Details",
                                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
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

    inspecting?.let { file ->
        ModalBottomSheet(onDismissRequest = { inspecting = null }) {
            Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
                Text(file.displayName, style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s2))
                Text(shortPath(file.sourceUri, 80),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s2))
                Text("Size: ${formatBytes(file.sizeBytes)}")
                Text("Type: ${file.mimeType ?: file.extension}")
                Text("Modified: ${formatInstant(file.modifiedAt)}")
                file.metadata.forEach { (k, v) -> Text("$k: $v") }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }
}

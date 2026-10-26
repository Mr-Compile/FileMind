package com.filemind.app.ui

import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.FilterButton
import com.filemind.app.components.FilterGroup
import com.filemind.app.components.FilterOption
import com.filemind.app.components.FilterSheet
import com.filemind.app.components.ListRow
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.PickerOption
import com.filemind.app.components.PickerSheet
import com.filemind.app.components.SearchField
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.components.fileKindIcon
import com.filemind.app.components.plannedChip
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.SearchViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.FileCandidate

private val KIND_OPTIONS = listOf(
    FilterOption("", "All"), FilterOption("image/", "Images"),
    FilterOption("video/", "Video"), FilterOption("audio/", "Audio"),
    FilterOption("text/", "Docs"), FilterOption("application/", "Apps & data")
)
private val SIZE_OPTIONS = listOf(
    FilterOption("0", "Any size"), FilterOption("1048576", "Over 1 MB"),
    FilterOption("10485760", "Over 10 MB"), FilterOption("104857600", "Over 100 MB")
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SearchScreen(app: FileMindApp) {
    val vm = backendViewModel(app) { SearchViewModel(it) }
    val state by vm.state.collectAsState()
    var showFilters by remember { mutableStateOf(false) }
    var showScope by remember { mutableStateOf(false) }
    var inspecting by remember { mutableStateOf<FileCandidate?>(null) }

    // Refresh index on resume so post-organize results appear.
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) vm.refresh()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }

    Column(Modifier.fillMaxSize().padding(Spacing.s4)) {
        // Large search pill — mockup s-search
        SearchField(value = state.query, onValueChange = { vm.update(query = it) },
            placeholder = "Search indexed files",
            modifier = Modifier.fillMaxWidth(), large = true)
        Spacer(Modifier.height(Spacing.s2))

        // Scope + filters row
        Row(verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
            FilledTonalButton(onClick = { showScope = true }) {
                Icon(Icons.Filled.Folder, null)
                Spacer(Modifier.width(Spacing.s2))
                Text(if (state.scope.isBlank()) "Everywhere"
                else shortPath(state.scope, 24), maxLines = 1)
            }
            val activeFilters = (if (state.kind.isNotEmpty()) 1 else 0) +
                (if (state.ext.isNotEmpty()) 1 else 0) +
                (if (state.minSize > 0) 1 else 0)
            FilterButton(activeFilters, onClick = { showFilters = true })
        }
        if (state.kind.isNotEmpty() || state.ext.isNotEmpty() || state.minSize > 0) {
            Spacer(Modifier.height(Spacing.s1))
            Text(
                listOfNotNull(
                    state.kind.takeIf { it.isNotEmpty() }
                        ?.let { k -> KIND_OPTIONS.firstOrNull { it.value == k }?.label ?: k },
                    state.ext.takeIf { it.isNotEmpty() }?.let { ".$it" },
                    state.minSize.takeIf { it > 0 }?.let { "≥ ${formatBytes(it)}" }
                ).joinToString(" · "),
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Spacer(Modifier.height(Spacing.s2))

        // Truthful capability note — name/path search only
        Card(Modifier.fillMaxWidth()) {
            Row(Modifier.padding(Spacing.s3),
                verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Name/path search only",
                        style = MaterialTheme.typography.bodyLarge)
                    Text("Full-text content search isn't available on Android yet.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                StatusChip(plannedChip())
            }
        }
        Spacer(Modifier.height(Spacing.s3))

        when {
            state.loading -> LoadingBox("Loading index")
            state.results.isEmpty() -> EmptyState(
                title = "No matches",
                body = if (state.all.isEmpty())
                    "Nothing is indexed yet — organize a folder first."
                else "Try a different name, extension, or kind.")
            else -> LazyColumn {
                items(state.results, key = { it.sourceUri }) { file ->
                    ListRow(
                        title = file.displayName,
                        subtitle = "${formatBytes(file.sizeBytes)} · " +
                            shortPath(file.sourceUri, 56),
                        icon = {
                            Icon(fileKindIcon(file.extension), null,
                                tint = MaterialTheme.colorScheme.onSurfaceVariant)
                        },
                        onClick = { inspecting = file })
                }
            }
        }
    }

    if (showFilters) {
        FilterSheet(
            title = "Filter results",
            groups = listOf(
                FilterGroup("Kind", "kind", KIND_OPTIONS),
                FilterGroup("Minimum size", "size", SIZE_OPTIONS)
            ),
            selected = mapOf(
                "kind" to state.kind,
                "size" to state.minSize.toString()),
            onSelect = { key, v ->
                if (key == "kind") vm.update(kind = v)
                else vm.update(minSize = v.toLongOrNull() ?: 0)
            },
            onClear = { vm.update(kind = "", ext = "", minSize = 0) },
            onDismiss = { showFilters = false },
            extraContent = {
                OutlinedTextField(
                    value = state.ext,
                    onValueChange = { vm.update(ext = it) },
                    label = { Text("Extension (e.g. pdf)") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }
        )
    }

    if (showScope) {
        PickerSheet(
            title = "Search scope",
            options = listOf(
                PickerOption("", "Everywhere", "Search the whole index")
            ) + vm.grantedTrees().map {
                PickerOption(it, shortPath(it, 40), "Granted folder")
            },
            selected = state.scope,
            onPick = { vm.update(scope = it) },
            onDismiss = { showScope = false })
    }

    inspecting?.let { file ->
        ModalBottomSheet(onDismissRequest = { inspecting = null }) {
            Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
                Text(file.displayName, style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s1))
                Text(shortPath(file.sourceUri, 90),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))
                Row(horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                    StatusChip(chipSpec("info", file.extension.uppercase()
                        .ifBlank { "FILE" }, fileKindIcon(file.extension)))
                    StatusChip(chipSpec("neutral", formatBytes(file.sizeBytes),
                        Icons.Filled.Search))
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
}

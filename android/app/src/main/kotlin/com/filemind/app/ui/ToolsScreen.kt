package com.filemind.app.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DriveFileMove
import androidx.compose.material.icons.filled.DriveFileRenameOutline
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Rule
import androidx.compose.material.icons.filled.SmartToy
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material.icons.filled.Upload
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Card
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.ListRow
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.SwitchRow
import com.filemind.app.components.chipSpec
import com.filemind.app.components.plannedChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.ToolsViewModel
import com.filemind.app.vm.backendViewModel

private data class ToolRow(
    val name: String, val desc: String, val icon: ImageVector,
    val available: Boolean, val onClick: (() -> Unit)?)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ToolsScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { ToolsViewModel(it) }
    val state by vm.state.collectAsState()
    var showCleanup by remember { mutableStateOf(false) }

    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) vm.refresh()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }

    // Same tool catalog as the Windows app — truthful availability per platform:
    // rows that exist on Android navigate; the rest carry a Planned chip.
    val tools = listOf(
        ToolRow("Organize", "Scan a folder, review suggestions, apply safely.",
            Icons.Filled.DriveFileMove, true) { nav.navigate(Routes.Organizer) },
        ToolRow("Smart Inbox",
            "${state.inboxCount} file(s) waiting for a human decision.",
            Icons.Filled.Badge, true) { nav.navigate(Routes.Activity) },
        ToolRow("Find duplicates", "Detect identical files by content hash.",
            Icons.Filled.ContentCopy, false, null),
        ToolRow("Cleanup", "Largest indexed files — review before removing anything.",
            Icons.Filled.Delete, true) { showCleanup = true },
        ToolRow("Undo history", "Every operation carries a reversible recipe.",
            Icons.Filled.History, true) { nav.navigate(Routes.Activity) },
        ToolRow("Watch folders", "Background organize on change — WorkManager job planned.",
            Icons.Filled.Visibility, false, null),
        ToolRow("AI suggestions", "AI-assisted classification suggestions.",
            Icons.Filled.SmartToy, false, null),
        ToolRow("Rename patterns", "Batch rename via naming templates.",
            Icons.Filled.DriveFileRenameOutline, false, null),
        ToolRow("Export profile", "Versioned organizer-profile.json export.",
            Icons.Filled.Upload, false, null)
    )

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {

        // Storage analysis — real numbers from the index
        SectionHeader("Storage analysis")
        Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s2)) {
            Column(Modifier.padding(Spacing.s4)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Filled.Storage, null,
                        tint = MaterialTheme.colorScheme.primary)
                    Spacer(Modifier.width(Spacing.s3))
                    Text("${state.indexedCount} file(s) indexed",
                        style = MaterialTheme.typography.titleMedium)
                }
                Spacer(Modifier.height(Spacing.s2))
                if (state.kindBreakdown.isEmpty()) {
                    Text("Scan a folder to break down space by type.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                } else {
                    state.kindBreakdown.entries
                        .sortedByDescending { it.value }
                        .forEach { (kind, count) ->
                            val bytes = state.bytesByKind[kind] ?: 0
                            Text("$kind — $count file(s) · ${formatBytes(bytes)}",
                                style = MaterialTheme.typography.bodyMedium)
                        }
                }
            }
        }

        SectionHeader("Tools")
        tools.forEach { tool ->
            ListRow(
                title = tool.name,
                subtitle = tool.desc,
                icon = { Icon(tool.icon, null,
                    tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                trailing = {
                    if (tool.available) {
                        Icon(Icons.Filled.ChevronRight, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    } else {
                        StatusChip(plannedChip())
                    }
                },
                onClick = { tool.onClick?.invoke() })
        }

        SectionHeader("Rules (${state.rules.size})")
        if (state.rules.isEmpty() && !state.loading) {
            Text("No organization rules defined — defaults apply.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = Spacing.s1))
        }
        Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s2)) {
            Column(Modifier.padding(Spacing.s2)) {
                state.rules.forEach { rule ->
                    SwitchRow(
                        title = rule.name,
                        subtitle = "Priority ${rule.priority} · " +
                            "${rule.conditionCount} condition(s)",
                        checked = rule.enabled,
                        onCheckedChange = { vm.toggleRule(rule) },
                        icon = { Icon(Icons.Filled.Rule, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant) })
                }
            }
        }
        Spacer(Modifier.height(Spacing.s12))
    }

    // Cleanup — the largest indexed files, straight from the index.
    if (showCleanup) {
        ModalBottomSheet(onDismissRequest = { showCleanup = false }) {
            Column(Modifier.padding(Spacing.s4)) {
                Text("Largest indexed files",
                    style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s2))
                if (state.largest.isEmpty()) {
                    Text("Nothing indexed yet — organize a folder first.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                } else {
                    LazyColumn {
                        items(state.largest) { f ->
                            ListRow(
                                title = f.displayName,
                                subtitle = "${formatBytes(f.sizeBytes)} · " +
                                    shortPath(f.sourceUri, 44),
                                icon = { Icon(Icons.Filled.Delete, null,
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant) })
                        }
                    }
                }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }
}

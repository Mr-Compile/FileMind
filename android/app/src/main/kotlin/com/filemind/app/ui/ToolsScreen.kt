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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.DriveFileMove
import androidx.compose.material.icons.filled.DriveFileRenameOutline
import androidx.compose.material.icons.filled.Rule
import androidx.compose.material.icons.filled.SmartToy
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material3.Card
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
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
    val available: Boolean, val route: String?)

@Composable
fun ToolsScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { ToolsViewModel(it) }
    val state by vm.state.collectAsState()

    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) vm.refresh()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }

    // Truthful availability — only Organize + Rules are real on Android.
    val tools = listOf(
        ToolRow("Organize", "Scan a folder, review suggestions, apply safely.",
            Icons.Filled.DriveFileMove, true, Routes.Organizer),
        ToolRow("Find duplicates", "Detect identical files by content hash.",
            Icons.Filled.ContentCopy, false, null),
        ToolRow("AI suggestions", "AI-assisted classification suggestions.",
            Icons.Filled.SmartToy, false, null),
        ToolRow("Rename patterns", "Batch rename via naming templates.",
            Icons.Filled.DriveFileRenameOutline, false, null)
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
                onClick = { tool.route?.let { nav.navigate(it) } })
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
}

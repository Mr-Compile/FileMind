package com.filemind.app.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.components.plannedChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.ToolsViewModel
import com.filemind.app.vm.backendViewModel
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle

private data class Tool(
    val name: String, val desc: String, val available: Boolean, val route: String?)

@Composable
fun ToolsScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { ToolsViewModel(it) }
    val state by vm.state.collectAsState()

    // Truthful availability: these services do not exist on Android yet.
    val tools = listOf(
        Tool("Organize", "Scan a folder, review suggestions, apply safely.", true, Routes.Organizer),
        Tool("Rules", "View the active organization rules.", true, null),
        Tool("Find duplicates", "Detect identical files by content hash.", false, null),
        Tool("Analyze storage", "Break down space by type, age, and size.", false, null),
        Tool("AI suggestions", "Groq-assisted classification suggestions.", false, null),
        Tool("Rename patterns", "Batch rename via naming templates.", false, null)
    )

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {
        SectionHeader("Tools")
        tools.forEach { tool ->
            Card(
                modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s1),
                onClick = {
                    if (tool.route != null) nav.navigate(tool.route)
                },
                enabled = tool.route != null || tool.name == "Rules"
            ) {
                Row(Modifier.padding(Spacing.s4),
                    verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text(tool.name, style = MaterialTheme.typography.titleMedium)
                        Text(tool.desc, style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (tool.available) {
                        StatusChip(chipSpec("success", "Available", Icons.Filled.CheckCircle))
                    } else {
                        StatusChip(plannedChip())
                    }
                }
            }
        }

        if (!state.loading && state.rules.isNotEmpty()) {
            Spacer(Modifier.height(Spacing.s4))
            SectionHeader("Active rules (${state.rules.size})")
            state.rules.forEach { rule ->
                Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Column(Modifier.padding(Spacing.s3)) {
                        Text(rule.name, style = MaterialTheme.typography.titleMedium)
                        Text("Priority ${rule.priority} · ${rule.conditionCount} condition(s)" +
                            if (!rule.enabled) " · disabled" else "",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
    }
}

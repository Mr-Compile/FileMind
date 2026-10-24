package com.filemind.app.ui

import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.ActivityViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.OperationStatus

/** Operation Center + Smart Inbox — the pack's "Task Center" surface. */
@Composable
fun ActivityScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { ActivityViewModel(it) }
    val state by vm.state.collectAsState()
    val workInfos by vm.workInfos().collectAsState(initial = emptyList())

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {
        if (state.loading) { LoadingBox("Loading activity"); return@Column }

        SectionHeader("Active")
        if (state.active.isEmpty() && workInfos.isEmpty()) {
            Text("No operations running.",
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        state.active.forEach { op ->
            Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                Row(Modifier.padding(Spacing.s3),
                    verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("${op.items.size} item(s) — ${op.operationId.take(8)}",
                            style = MaterialTheme.typography.bodyLarge)
                        Text(formatInstant(op.createdAt),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    StatusChip(operationChip(op.status))
                }
            }
        }
        workInfos.forEach { wi ->
            Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                Row(Modifier.padding(Spacing.s3),
                    verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("Background job ${wi.id.toString().take(8)}",
                            style = MaterialTheme.typography.bodyLarge)
                    }
                    StatusChip(operationChip(OperationStatus.Executing))
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Smart Inbox")
        if (state.inbox.isEmpty()) {
            Text("Nothing needs your decision.",
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        } else {
            state.inbox.forEach { item ->
                Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Column(Modifier.padding(Spacing.s3)) {
                        Text(shortPath(item.path, 60),
                            style = MaterialTheme.typography.bodyLarge, maxLines = 1)
                        Text(item.reason,
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Row(Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.End) {
                            TextButton(onClick = { vm.resolveInbox(item.id) }) {
                                Text("Mark resolved")
                            }
                        }
                    }
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Recent operations")
        if (state.recent.isEmpty()) {
            EmptyState("No history yet",
                "Completed organize runs show up here with results and undo.",
                "Open organizer", { nav.navigate(Routes.Organizer) })
        } else {
            state.recent.forEach { op ->
                Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Column(Modifier.padding(Spacing.s3)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Column(Modifier.weight(1f)) {
                                Text("${op.items.size} item(s)",
                                    style = MaterialTheme.typography.bodyLarge)
                                Text(formatInstant(op.createdAt),
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            StatusChip(operationChip(op.status))
                        }
                        val failed = op.items.count {
                            it.status == OperationStatus.Failed ||
                                it.status == OperationStatus.NeedsAttention }
                        if (failed > 0) {
                            Text("$failed item(s) failed or need attention",
                                color = MaterialTheme.colorScheme.error,
                                style = MaterialTheme.typography.bodyMedium)
                        }
                        op.error?.let {
                            Text(it.message, color = MaterialTheme.colorScheme.error,
                                style = MaterialTheme.typography.bodyMedium)
                        }
                        val undoable = op.status == OperationStatus.Committed &&
                            op.items.any { it.undoRecipe != null }
                        if (undoable) {
                            Row(Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.End) {
                                TextButton(
                                    enabled = !state.busyUndo,
                                    onClick = { vm.undo(op.operationId) }) {
                                    Text("Undo")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

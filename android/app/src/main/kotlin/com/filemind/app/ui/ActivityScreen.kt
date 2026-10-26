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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Undo
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.ConfidenceBar
import com.filemind.app.components.EmptyState
import com.filemind.app.components.ListRow
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.SummaryStrip
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.app.vm.ActivityViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.app.SmartInboxItem
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OperationStatus

/** Task Center — active runs, Smart Inbox, history. Mirrors mockup s-activity. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ActivityScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { ActivityViewModel(it) }
    val state by vm.state.collectAsState()
    val workInfos by vm.workInfos().collectAsState(initial = emptyList())

    var opDetail by remember { mutableStateOf<OperationRecord?>(null) }
    var inboxDetail by remember { mutableStateOf<SmartInboxItem?>(null) }

    // Refresh whenever the screen resumes.
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) vm.refresh()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {
        if (state.loading) { LoadingBox("Loading activity"); return@Column }

        // Running now — genuinely in-flight statuses only. NeedsAttention is
        // terminal (the run ended) but still lands in getIncomplete() on purpose;
        // it gets its own section so finished ops don't pose as "running".
        val running = state.active.filter { it.status in setOf(
            OperationStatus.Planned, OperationStatus.Validating,
            OperationStatus.Executing, OperationStatus.Verifying,
            OperationStatus.RollingBack) }
        val attention = state.active.filter {
            it.status == OperationStatus.NeedsAttention }
        SectionHeader("Running now")
        if (running.isEmpty() && workInfos.isEmpty()) {
            Text("Nothing running — operations appear here while they execute.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = Spacing.s1))
        }
        running.forEach { op -> ActiveOpCard(op) { opDetail = op } }
        workInfos.forEach { wi ->
            ListRow(
                title = "Background scan",
                subtitle = "Job ${wi.id.toString().take(8)}",
                icon = { Icon(Icons.Filled.Sync, null,
                    tint = MaterialTheme.colorScheme.primary) },
                trailing = { StatusChip(operationChip(OperationStatus.Executing)) })
        }

        if (attention.isNotEmpty()) {
            SectionHeader("Needs attention")
            Text("These runs ended with unresolved items — tap one to review.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = Spacing.s1))
            attention.forEach { op -> ActiveOpCard(op) { opDetail = op } }
        }

        // Smart Inbox
        SectionHeader("Smart Inbox")
        if (state.inbox.isEmpty()) {
            Text("Nothing needs your decision.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = Spacing.s1))
        } else {
            state.inbox.forEach { item ->
                ListRow(
                    title = shortPath(item.path, 44),
                    subtitle = item.reason,
                    icon = { Icon(Icons.Filled.Warning, null,
                        tint = MaterialTheme.colorScheme.semantic.statusWarning) },
                    trailing = {
                        ConfidenceBar(item.classification.confidence)
                    },
                    onClick = { inboxDetail = item })
            }
        }

        // Recent
        SectionHeader("Recent")
        if (state.recent.isEmpty()) {
            EmptyState("No history yet",
                "Completed organize runs show up here with results and undo.",
                "Open organizer", { nav.navigate(Routes.Organizer) })
        } else {
            state.recent.forEach { op ->
                val failed = op.items.count {
                    it.status == OperationStatus.Failed ||
                        it.status == OperationStatus.NeedsAttention }
                ListRow(
                    title = "Organize · ${op.items.size} item(s)" +
                        (if (failed > 0) " · $failed need attention" else ""),
                    subtitle = formatInstant(op.createdAt),
                    icon = { Icon(Icons.Filled.History, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { StatusChip(operationChip(op.status)) },
                    onClick = { opDetail = op })
            }
        }

        // Status legend — mockup s-activity legend
        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("What the statuses mean")
        Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s2)) {
            Column(Modifier.padding(Spacing.s3),
                verticalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                listOf(
                    Triple("Completed", "Every item verified in place", Icons.Filled.CheckCircle),
                    Triple("Running", "Operation is executing right now", Icons.Filled.PlayCircle),
                    Triple("Ready", "Planned, waiting to execute", Icons.Filled.Schedule),
                    Triple("Needs attention", "An item couldn't be verified — tap to review",
                        Icons.Filled.Warning),
                    Triple("Failed", "The operation stopped — nothing half-moved",
                        Icons.Filled.Error),
                    Triple("Undone", "Reversed by an undo run", Icons.Filled.Undo)
                ).forEach { (label, desc, icon) ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(icon, null, modifier = Modifier.width(20.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                        Spacer(Modifier.width(Spacing.s3))
                        Text(label, style = MaterialTheme.typography.bodyLarge,
                            fontWeight = FontWeight.Medium)
                        Spacer(Modifier.width(Spacing.s2))
                        Text(desc, style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
        Spacer(Modifier.height(Spacing.s12))
    }

    // Operation detail sheet
    opDetail?.let { op ->
        ModalBottomSheet(onDismissRequest = { opDetail = null }) {
            Column(Modifier.fillMaxWidth().verticalScroll(rememberScrollState())
                .padding(Spacing.s4)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Organize run", style = MaterialTheme.typography.titleLarge,
                        modifier = Modifier.weight(1f))
                    StatusChip(operationChip(op.status))
                }
                Text(formatInstant(op.createdAt),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))
                SummaryStrip(listOf(
                    op.items.count { it.status == OperationStatus.Committed }
                        .toString() to "moved",
                    op.items.count {
                        it.status == OperationStatus.Failed ||
                            it.status == OperationStatus.NeedsAttention
                    }.toString() to "attention",
                    op.items.size.toString() to "total"))
                op.error?.let {
                    Spacer(Modifier.height(Spacing.s2))
                    Text(it.message, color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodyMedium)
                }
                Spacer(Modifier.height(Spacing.s3))
                op.items.forEach { item ->
                    ListRow(
                        title = shortPath(item.originalLocation, 48),
                        subtitle = (item.resultingLocation
                            ?.let { "→ ${shortPath(it, 44)}" }
                            ?: item.error?.message) ?: "",
                        trailing = { StatusChip(operationChip(item.status)) })
                }
                val undoable = op.status == OperationStatus.Committed &&
                    op.items.any { it.undoRecipe != null }
                if (undoable) {
                    Spacer(Modifier.height(Spacing.s3))
                    OutlinedButton(
                        enabled = !state.busyUndo,
                        onClick = {
                            opDetail = null
                            vm.undo(op.operationId)
                        },
                        modifier = Modifier.fillMaxWidth()) {
                        Icon(Icons.Filled.Undo, null)
                        Spacer(Modifier.width(Spacing.s2))
                        Text("Undo this run")
                    }
                }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }

    // Inbox detail sheet
    inboxDetail?.let { item ->
        ModalBottomSheet(onDismissRequest = { inboxDetail = null }) {
            Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
                Text("Needs your decision",
                    style = MaterialTheme.typography.titleLarge)
                Spacer(Modifier.height(Spacing.s1))
                Text(shortPath(item.path, 80),
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))
                ConfidenceBar(item.classification.confidence)
                Spacer(Modifier.height(Spacing.s2))
                Text(item.classification.explanation,
                    style = MaterialTheme.typography.bodyMedium)
                Spacer(Modifier.height(Spacing.s2))
                Text(item.reason, style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s4))
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = {
                        inboxDetail = null
                        vm.resolveInbox(item.id)
                    }) {
                        Icon(Icons.Filled.Check, null)
                        Spacer(Modifier.width(Spacing.s1))
                        Text("Keep it — mark resolved")
                    }
                }
                Spacer(Modifier.height(Spacing.s4))
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ActiveOpCard(op: com.filemind.core.contracts.OperationRecord,
                         onOpen: () -> Unit) {
    val done = op.items.count {
        it.status == OperationStatus.Committed ||
            it.status == OperationStatus.Failed ||
            it.status == OperationStatus.NeedsAttention
    }
    Card(onClick = onOpen,
        modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
        Column(Modifier.padding(Spacing.s4)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Organize · ${op.items.size} item(s)",
                    style = MaterialTheme.typography.titleMedium,
                    modifier = Modifier.weight(1f))
                StatusChip(operationChip(op.status))
            }
            Spacer(Modifier.height(Spacing.s2))
            LinearProgressIndicator(
                progress = {
                    if (op.items.isNotEmpty()) done / op.items.size.toFloat() else 0f
                },
                modifier = Modifier.fillMaxWidth())
            Spacer(Modifier.height(Spacing.s1))
            Text("$done/${op.items.size} item(s) · ${formatInstant(op.createdAt)}",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}


package com.filemind.app.ui

import android.provider.DocumentsContract
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.DriveFileMove
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.FolderOpen
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Shield
import androidx.compose.material.icons.filled.Undo
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Badge
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.ConfidenceBar
import com.filemind.app.components.ConfirmSheet
import com.filemind.app.components.EmptyState
import com.filemind.app.components.FriendlyError
import com.filemind.app.components.ListRow
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.SummaryStrip
import com.filemind.app.components.SwitchRow
import com.filemind.app.components.WhySheet
import com.filemind.app.components.decisionChip
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.app.vm.OrgStep
import com.filemind.core.app.AnalyzedFile
import com.filemind.core.contracts.OperationStatus
import com.filemind.core.contracts.OperationType

/** Mockup flow: Access → Folder → Analyze → Plan → Run (Step N of 5). */
private fun stepIndex(step: OrgStep): Int = when (step) {
    OrgStep.Access -> 0
    OrgStep.Source -> 1
    is OrgStep.Scanning -> 2
    is OrgStep.Preview -> 3
    is OrgStep.Executing, is OrgStep.Done, is OrgStep.Failed -> 4
}
private val STEP_NAMES = listOf("Access", "Folder", "Analyze", "Plan", "Run")

@Composable
private fun StepsHeader(step: OrgStep) {
    val idx = stepIndex(step)
    Column(Modifier.padding(Spacing.s1)) {
        Text("Step ${idx + 1} of ${STEP_NAMES.size} — ${STEP_NAMES[idx]}",
            style = MaterialTheme.typography.labelMedium,
            fontWeight = androidx.compose.ui.text.font.FontWeight.Bold,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(6.dp))
        LinearProgressIndicator(
            progress = { (idx + 1) / STEP_NAMES.size.toFloat() },
            modifier = Modifier.fillMaxWidth())
    }
}

@Composable
fun OrganizerScreen(app: FileMindApp, nav: NavHostController) {
    // App-scoped session: navigating away and back resumes the exact step.
    val vm = app.organizerViewModel
    val state by vm.state.collectAsState()

    var confirm by remember { mutableStateOf<OrgStep.Preview?>(null) }
    var why by remember { mutableStateOf<AnalyzedFile?>(null) }
    var leaveRun by remember { mutableStateOf(false) }

    // System Back while a run is executing asks first — the run can safely
    // continue in the app-scoped VM (result lands in Activity).
    BackHandler(enabled = state.step is OrgStep.Executing) { leaveRun = true }
    if (leaveRun) {
        val exec = state.step as? OrgStep.Executing
        RunInProgressLeaveDialog(
            done = exec?.done ?: 0, total = exec?.plan?.items?.size ?: 0,
            onKeepRunning = { leaveRun = false; nav.popBackStack() },
            onCancelRun = { leaveRun = false; vm.cancel(); nav.popBackStack() },
            onStay = { leaveRun = false })
    }

    // Land the picker inside Download: Android 11+ refuses the Download
    // *root* itself ("To protect your privacy…") but allows subfolders —
    // opening inside Download lets the user create/pick e.g. FileMind/.
    val initialTreeUri = remember {
        DocumentsContract.buildDocumentUri(
            "com.android.externalstorage.documents", "primary:Download")
    }
    val treePicker = rememberLauncherForActivityResult(
        ActivityResultContracts.OpenDocumentTree()
    ) { uri ->
        uri?.let {
            vm.onTreePicked(it,
                android.content.Intent.FLAG_GRANT_READ_URI_PERMISSION or
                    android.content.Intent.FLAG_GRANT_WRITE_URI_PERMISSION)
        }
    }

    Column(Modifier.fillMaxSize().padding(Spacing.s4)) {
        StepsHeader(state.step)
        Spacer(Modifier.height(Spacing.s3))

        when (val step = state.step) {

            // ---- Step 1: permission explainer page (before any access ask)
            OrgStep.Access -> {
                Column(Modifier.verticalScroll(rememberScrollState())) {
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(Spacing.s4)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Filled.Shield, contentDescription = null,
                                    tint = MaterialTheme.colorScheme.primary)
                                Spacer(Modifier.width(Spacing.s3))
                                Text("Folder access",
                                    style = MaterialTheme.typography.titleMedium)
                            }
                            Spacer(Modifier.height(Spacing.s2))
                            Text(
                                "FileMind organizes only the folders you pick. Android " +
                                    "asks for access per folder — nothing leaves the device, " +
                                    "and scanning never moves anything.",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Spacer(Modifier.height(Spacing.s3))
                            ListRow(title = "You choose each folder",
                                subtitle = "Access is per-folder and revocable in Settings",
                                icon = { Icon(Icons.Filled.CheckCircle, null,
                                    tint = MaterialTheme.colorScheme.primary) })
                            ListRow(title = "On-device only",
                                subtitle = "Names and metadata never leave the phone",
                                icon = { Icon(Icons.Filled.Lock, null,
                                    tint = MaterialTheme.colorScheme.primary) })
                            ListRow(title = "Every run is undoable",
                                subtitle = "Moves are recorded with an undo recipe",
                                icon = { Icon(Icons.Filled.Undo, null,
                                    tint = MaterialTheme.colorScheme.primary) })
                        }
                    }
                    Spacer(Modifier.height(Spacing.s3))
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(Spacing.s4)) {
                            Text("About the Downloads folder",
                                style = MaterialTheme.typography.titleMedium)
                            Text(
                                "Android doesn't allow granting the Downloads root itself " +
                                    "(\"To protect your privacy, choose another folder\"). " +
                                    "The picker opens inside Downloads — create or pick a " +
                                    "subfolder such as Downloads/FileMind.",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    Spacer(Modifier.height(Spacing.s4))
                    Button(onClick = { treePicker.launch(initialTreeUri) },
                        modifier = Modifier.fillMaxWidth().height(52.dp)) {
                        Text("Choose a folder to grant")
                    }
                    TextButton(onClick = { vm.dismissAccess() },
                        modifier = Modifier.fillMaxWidth()) {
                        Text("Not now")
                    }
                }
            }

            // ---- Step 2: folder scope list
            OrgStep.Source -> {
                Column(Modifier.weight(1f).verticalScroll(rememberScrollState())) {
                    SectionHeader("Pick a folder to organize")
                    state.grantedTrees.forEach { tree ->
                        ListRow(
                            title = shortPath(tree, 40),
                            subtitle = "Access granted · SAF folder",
                            icon = { Icon(Icons.Filled.Folder, null,
                                tint = MaterialTheme.colorScheme.primary) },
                            trailing = {
                                StatusChip(com.filemind.app.components.chipSpec(
                                    "success", "Connected", Icons.Filled.CheckCircle))
                            },
                            onClick = { vm.startScan(tree, shortPath(tree, 40)) })
                    }
                    ListRow(
                        title = "App storage",
                        subtitle = "FileMind's own files area — no permission needed",
                        icon = { Icon(Icons.Filled.Folder, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                        onClick = {
                            vm.startScan(
                                (app.getExternalFilesDir(null) ?: app.filesDir).absolutePath,
                                "App storage")
                        })
                    Spacer(Modifier.height(Spacing.s2))
                    ListRow(
                        title = "Grant another folder",
                        subtitle = "Open the system picker — for Downloads pick a subfolder",
                        icon = { Icon(Icons.Filled.FolderOpen, null,
                            tint = MaterialTheme.colorScheme.primary) },
                        onClick = { treePicker.launch(initialTreeUri) })

                    state.error?.let {
                        Spacer(Modifier.height(Spacing.s3))
                        FriendlyError("Couldn't start", it)
                    }
                }
            }

            // ---- Step 3: analyze (scan progress)
            is OrgStep.Scanning -> {
                Spacer(Modifier.height(Spacing.s6))
                Text("Analyzing ${step.rootLabel}",
                    style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(Spacing.s2))
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s2))
                Text("${step.found} files found",
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s4))
                OutlinedButton(onClick = { vm.cancel() }) { Text("Cancel") }
            }

            // ---- Step 4: plan page (summary + toggles + safety + moves)
            is OrgStep.Preview -> {
                val moves = step.plan.items.count { it.operation == OperationType.Move }
                val asks = step.plan.items.count { it.operation == OperationType.Ask }
                val untouched = step.analyzed.size - step.plan.items.size
                val bySource = step.analyzed.associateBy { it.candidate.sourceUri }

                Column(Modifier.weight(1f).verticalScroll(rememberScrollState())) {
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(Spacing.s4)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Filled.DriveFileMove, null,
                                    tint = MaterialTheme.colorScheme.primary)
                                Spacer(Modifier.width(Spacing.s3))
                                Text("Plan — ${state.sourceLabel ?: "folder"}",
                                    style = MaterialTheme.typography.titleMedium)
                            }
                            Text(
                                buildString {
                                    append("${step.plan.items.size} planned action(s)")
                                    append(", $moves moving")
                                    if (asks > 0) append(", $asks to Smart Inbox")
                                    if (untouched > 0) append(", $untouched left in place")
                                    if (step.plan.dryRun) append(" · dry-run preview")
                                },
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }

                    Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s2)) {
                        Column(Modifier.padding(Spacing.s3)) {
                            SwitchRow(
                                title = "Preview only",
                                subtitle = "Dry run — the full pipeline runs, nothing moves",
                                checked = state.dryRun,
                                onCheckedChange = {
                                    vm.setDryRun(it); vm.buildPlan(step.analyzed)
                                },
                                icon = { Icon(Icons.Filled.PlayArrow, null,
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant) })
                            SwitchRow(
                                title = "Keep folder structure",
                                subtitle = "Preserve relative subfolders under the destination",
                                checked = state.preserveTree,
                                onCheckedChange = {
                                    vm.setPreserveTree(it); vm.buildPlan(step.analyzed)
                                },
                                icon = { Icon(Icons.Filled.Folder, null,
                                    tint = MaterialTheme.colorScheme.onSurfaceVariant) })
                        }
                    }

                    // safety card — ok / warn / error variants (mockup lines)
                    when {
                        !step.safety.allowed -> FriendlyError("Blocked",
                            step.safety.blockers.joinToString("\n")
                                .ifBlank { "This plan is not safe to run." })
                        step.safety.warnings.isNotEmpty() -> Card(
                            Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor =
                                MaterialTheme.colorScheme.semantic.statusWarning.copy(alpha = 0.12f))) {
                            Column(Modifier.padding(Spacing.s3)) {
                                step.safety.warnings.forEach {
                                    Text("⚠ $it", style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                        else -> Card(Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor =
                                MaterialTheme.colorScheme.semantic.statusSuccess.copy(alpha = 0.12f))) {
                            Text("Safety check passed — protected paths untouched, " +
                                "every move has an undo recipe.",
                                style = MaterialTheme.typography.bodyMedium,
                                modifier = Modifier.padding(Spacing.s3))
                        }
                    }

                    SectionHeader("Planned moves")
                    if (step.plan.items.isEmpty()) {
                        EmptyState("Nothing to move",
                            "Every file was left in place — files without a recognizable " +
                                "type or confident target stay put. Medium-confidence " +
                                "items wait in the Smart Inbox.")
                    } else {
                        step.plan.items.forEach { item ->
                            val a = bySource[item.source]
                            ListRow(
                                title = app.backend.paths.fileName(item.source),
                                subtitle = if (item.operation == OperationType.Ask || item.destination == null)
                                    item.reason else "→ ${shortPath(item.destination!!, 60)}",
                                icon = {
                                    Icon(
                                        if (item.operation == OperationType.Ask)
                                            Icons.Filled.Warning else Icons.Filled.DriveFileMove,
                                        null,
                                        tint = if (item.operation == OperationType.Ask)
                                            MaterialTheme.colorScheme.semantic.statusWarning
                                        else MaterialTheme.colorScheme.onSurfaceVariant)
                                },
                                trailing = {
                                    if (item.operation == OperationType.Ask) {
                                        StatusChip(com.filemind.app.components.chipSpec(
                                            "warning", "Review", Icons.Filled.Warning))
                                    } else if (a != null) {
                                        ConfidenceBar(a.classification.confidence)
                                    }
                                },
                                onClick = { a?.let { why = it } })
                        }
                    }
                }
                Spacer(Modifier.height(Spacing.s2))
                Button(
                    enabled = step.safety.allowed && step.plan.items.isNotEmpty(),
                    onClick = {
                        if (step.plan.dryRun) vm.execute(step.plan)
                        else confirm = step
                    },
                    modifier = Modifier.fillMaxWidth().height(52.dp)) {
                    Text(if (step.plan.dryRun) "Run preview" else "Run this plan")
                }
                TextButton(onClick = { vm.reset() }, modifier = Modifier.fillMaxWidth()) {
                    Text("Pick another folder")
                }
            }

            // ---- Step 5: run (stage chip + determinate bar + live log)
            is OrgStep.Executing -> {
                Card(Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(Spacing.s4)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("Running…", style = MaterialTheme.typography.titleMedium,
                                modifier = Modifier.weight(1f))
                            StatusChip(operationChip(
                                runCatching {
                                    OperationStatus.valueOf(step.stage)
                                }.getOrDefault(OperationStatus.Executing)))
                        }
                        Spacer(Modifier.height(Spacing.s2))
                        val total = step.plan.items.size
                        LinearProgressIndicator(
                            progress = {
                                if (total > 0) step.done / total.toFloat() else 0f
                            },
                            modifier = Modifier.fillMaxWidth())
                        Spacer(Modifier.height(Spacing.s1))
                        Text("${step.done}/$total item(s)",
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Spacer(Modifier.height(Spacing.s2))
                        step.log.forEach { line ->
                            Text("• $line", style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
                Spacer(Modifier.height(Spacing.s4))
                OutlinedButton(onClick = { vm.cancel() }) { Text("Cancel") }
            }

            // ---- result / dry-run result
            is OrgStep.Done -> {
                Column(Modifier.verticalScroll(rememberScrollState())) {
                    if (step.wasDryRun) {
                        Card(Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor =
                                MaterialTheme.colorScheme.semantic.statusSuccess.copy(alpha = 0.12f))) {
                            Text("Dry run complete. The full pipeline ran; nothing was moved.",
                                style = MaterialTheme.typography.bodyMedium,
                                modifier = Modifier.padding(Spacing.s3))
                        }
                        Spacer(Modifier.height(Spacing.s3))
                        step.record.items.take(12).forEach { item ->
                            ListRow(
                                title = app.backend.paths.fileName(item.originalLocation),
                                subtitle = item.resultingLocation?.let { "→ ${shortPath(it, 56)}" }
                                    ?: item.error?.message ?: "",
                                icon = { Icon(Icons.Filled.Check, null,
                                    tint = MaterialTheme.colorScheme.semantic.statusSuccess) })
                        }
                        Spacer(Modifier.height(Spacing.s4))
                        Button(onClick = { vm.setDryRun(false); vm.reset() },
                            modifier = Modifier.fillMaxWidth().height(52.dp)) {
                            Text("Run for real")
                        }
                        TextButton(onClick = { vm.reset() },
                            modifier = Modifier.fillMaxWidth()) { Text("Start over") }
                    } else {
                        Card(Modifier.fillMaxWidth()) {
                            Column(Modifier.padding(Spacing.s4)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text(step.record.status.name,
                                        style = MaterialTheme.typography.titleMedium,
                                        modifier = Modifier.weight(1f))
                                    StatusChip(operationChip(step.record.status))
                                }
                                val done = step.record.items.count {
                                    it.status == OperationStatus.Committed }
                                val failed = step.record.items.count {
                                    it.status == OperationStatus.Failed ||
                                        it.status == OperationStatus.NeedsAttention }
                                Text("$done item(s) moved · recorded in Activity with an undo recipe" +
                                    (if (failed > 0) " · $failed need attention" else ""),
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                        step.record.error?.let {
                            Spacer(Modifier.height(Spacing.s2))
                            FriendlyError("Operation error", it.message)
                        }
                        state.error?.let {
                            Spacer(Modifier.height(Spacing.s2))
                            FriendlyError("Notice", it)
                        }
                        Spacer(Modifier.height(Spacing.s4))
                        if (step.record.items.any {
                            it.status == OperationStatus.Committed && it.undoRecipe != null }) {
                            OutlinedButton(onClick = { vm.undo(step.record.operationId) },
                                modifier = Modifier.fillMaxWidth().height(52.dp)) {
                                Icon(Icons.Filled.Undo, null)
                                Spacer(Modifier.width(Spacing.s2))
                                Text("Undo this run")
                            }
                            Spacer(Modifier.height(Spacing.s2))
                        }
                        Button(onClick = { nav.navigate(Routes.Activity) },
                            modifier = Modifier.fillMaxWidth().height(52.dp)) {
                            Text("Done")
                        }
                    }
                }
            }

            is OrgStep.Failed -> {
                Spacer(Modifier.height(Spacing.s4))
                FriendlyError(step.title, step.detail,
                    "Check the source and permissions, then try again.")
                Spacer(Modifier.height(Spacing.s4))
                OutlinedButton(onClick = { vm.reset() }) { Text("Back to start") }
            }
        }
    }

    confirm?.let { step ->
        ConfirmSheet(
            plan = step.plan,
            safety = step.safety,
            onConfirm = {
                confirm = null
                vm.execute(step.plan)
            },
            onDismiss = { confirm = null })
    }

    why?.let { WhySheet(analyzed = it, onDismiss = { why = null }) }
}

/**
 * "Run in progress" leave guard — shown when the user tries to exit the
 * organizer mid-execute (system Back, or shell navigation via AppShell).
 * The run lives in the app-scoped OrganizerViewModel, so leaving is safe:
 * the record lands in Activity either way.
 */
@Composable
fun RunInProgressLeaveDialog(
    done: Int, total: Int,
    onKeepRunning: () -> Unit, onCancelRun: () -> Unit, onStay: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onStay,
        title = { Text("Run in progress") },
        text = {
            Text("$done of $total item(s) done. The run can keep going in " +
                "the background — the result lands in Activity.")
        },
        confirmButton = {
            TextButton(onClick = onKeepRunning) { Text("Keep running") }
        },
        dismissButton = {
            Row {
                TextButton(onClick = onCancelRun) { Text("Cancel run") }
                TextButton(onClick = onStay) { Text("Stay") }
            }
        })
}

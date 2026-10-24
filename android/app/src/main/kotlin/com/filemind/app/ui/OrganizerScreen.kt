package com.filemind.app.ui

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
import androidx.compose.material.icons.filled.FolderOpen
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
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
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.ConfirmSheet
import com.filemind.app.components.EmptyState
import com.filemind.app.components.FriendlyError
import com.filemind.app.components.PermissionExplainer
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.SummaryStrip
import com.filemind.app.components.WhySheet
import com.filemind.app.components.decisionChip
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.app.vm.OrgStep
import com.filemind.app.vm.OrganizerViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.app.AnalyzedFile
import com.filemind.core.contracts.OperationStatus

/**
 * Spec flow: Select source → Choose action → Pre-check → Resolve restrictions
 * → Confirm if risky → Execute → Progress → Result → Next action.
 */
@Composable
fun OrganizerScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { OrganizerViewModel(it) }
    val state by vm.state.collectAsState()

    var explainer by remember { mutableStateOf(false) }
    var confirm by remember { mutableStateOf<OrgStep.Preview?>(null) }
    var why by remember { mutableStateOf<AnalyzedFile?>(null) }

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
        when (val step = state.step) {

            is OrgStep.Source -> {
                SectionHeader("Choose a source")
                Text(
                    "FileMind analyzes files before anything moves. You always " +
                        "see the plan and confirm first.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))

                Card(modifier = Modifier.fillMaxWidth(), onClick = { explainer = true }) {
                    Row(Modifier.padding(Spacing.s4),
                        verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Filled.FolderOpen, contentDescription = null)
                        Spacer(Modifier.width(Spacing.s3))
                        Column(Modifier.weight(1f)) {
                            Text("Pick a folder", style = MaterialTheme.typography.titleMedium)
                            Text("Grant access to any folder on this device",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = null)
                    }
                }

                Spacer(Modifier.height(Spacing.s3))
                Card(modifier = Modifier.fillMaxWidth(), onClick = {
                    vm.startScan(
                        (app.getExternalFilesDir(null) ?: app.filesDir).absolutePath,
                        "App storage")
                }) {
                    Row(Modifier.padding(Spacing.s4),
                        verticalAlignment = Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("App storage", style = MaterialTheme.typography.titleMedium)
                            Text("FileMind's own files area — no permission needed",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = null)
                    }
                }

                if (state.grantedTrees.isNotEmpty()) {
                    Spacer(Modifier.height(Spacing.s4))
                    SectionHeader("Granted folders")
                    state.grantedTrees.forEach { tree ->
                        Card(modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s1),
                            onClick = { vm.startScan(tree, shortPath(tree)) }) {
                            Row(Modifier.padding(Spacing.s3),
                                verticalAlignment = Alignment.CenterVertically) {
                                Text(shortPath(tree, 56),
                                    style = MaterialTheme.typography.bodyMedium,
                                    maxLines = 1, modifier = Modifier.weight(1f))
                                Icon(Icons.AutoMirrored.Filled.ArrowForward, contentDescription = null)
                            }
                        }
                    }
                }

                state.error?.let {
                    Spacer(Modifier.height(Spacing.s3))
                    FriendlyError("Couldn't start", it)
                }
            }

            is OrgStep.Scanning -> {
                Spacer(Modifier.height(Spacing.s6))
                Text("Scanning ${step.rootLabel}",
                    style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(Spacing.s2))
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s2))
                Text("${step.found} files found",
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s4))
                OutlinedButton(onClick = { vm.cancel() }) { Text("Cancel") }
            }

            is OrgStep.Review -> {
                val actionable = step.analyzed.filter {
                    it.decision.kind == com.filemind.core.contracts.DecisionKind.Organize ||
                        it.decision.kind == com.filemind.core.contracts.DecisionKind.Review }
                SectionHeader("Review suggestions (${step.analyzed.size} files)")
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("${actionable.size} actionable",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.weight(1f))
                    Button(
                        enabled = actionable.isNotEmpty(),
                        onClick = { vm.buildPlan(step.analyzed) }) {
                        Text("Preview plan")
                    }
                }
                Spacer(Modifier.height(Spacing.s2))
                LazyColumn(verticalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                    items(step.analyzed) { a ->
                        Card(modifier = Modifier.fillMaxWidth(), onClick = { why = a }) {
                            Row(Modifier.padding(Spacing.s3),
                                verticalAlignment = Alignment.CenterVertically) {
                                Column(Modifier.weight(1f)) {
                                    Text(a.candidate.displayName,
                                        style = MaterialTheme.typography.bodyLarge,
                                        maxLines = 1)
                                    Text(
                                        buildString {
                                            append(a.classification.explanation)
                                            a.classification.subject?.let { append(" · $it") }
                                            a.classification.project?.let { append(" · $it") }
                                        },
                                        style = MaterialTheme.typography.labelMedium,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                                        maxLines = 2)
                                }
                                StatusChip(decisionChip(a.decision.kind))
                            }
                        }
                    }
                }
            }

            is OrgStep.Preview -> {
                Column(Modifier.weight(1f).verticalScroll(rememberScrollState())) {
                    SectionHeader("Plan preview")
                    if (!step.safety.allowed) {
                        FriendlyError("Blocked",
                            step.safety.blockers.joinToString("\n").ifBlank { "This plan is not safe to run." },
                            "Change the source or rules, then try again.")
                        Spacer(Modifier.height(Spacing.s3))
                    }
                    if (step.safety.warnings.isNotEmpty()) {
                        Card(Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.semantic
                                    .statusWarning.copy(alpha = 0.12f))) {
                            Column(Modifier.padding(Spacing.s3)) {
                                step.safety.warnings.forEach {
                                    Text("• $it", style = MaterialTheme.typography.bodyMedium)
                                }
                            }
                        }
                        Spacer(Modifier.height(Spacing.s3))
                    }
                    step.plan.items.forEach { item ->
                        Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                            Column(Modifier.padding(Spacing.s3)) {
                                Text("${item.operation}: ${shortPath(item.source)}",
                                    style = MaterialTheme.typography.bodyLarge, maxLines = 1)
                                item.destination?.let {
                                    Text("→ ${shortPath(it, 60)}",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                                Text(item.reason,
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                }
                Spacer(Modifier.height(Spacing.s2))
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = { vm.reset() }) { Text("Back") }
                    Button(
                        enabled = step.safety.allowed && step.plan.items.isNotEmpty(),
                        onClick = { confirm = step }) {
                        Text("Continue")
                    }
                }
            }

            is OrgStep.Executing -> {
                Spacer(Modifier.height(Spacing.s6))
                Text("Organizing files", style = MaterialTheme.typography.titleMedium)
                Spacer(Modifier.height(Spacing.s2))
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                Text("${step.plan.items.size} action(s)",
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s4))
                OutlinedButton(onClick = { vm.cancel() }) { Text("Cancel") }
            }

            is OrgStep.Done -> {
                Column(Modifier.verticalScroll(rememberScrollState())) {
                    SectionHeader("Result")
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        StatusChip(operationChip(step.record.status))
                        Spacer(Modifier.width(Spacing.s3))
                        Text(formatInstant(step.record.createdAt),
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Spacer(Modifier.height(Spacing.s3))
                    val done = step.record.items.count {
                        it.status == OperationStatus.Committed }
                    val failed = step.record.items.count {
                        it.status == OperationStatus.Failed ||
                            it.status == OperationStatus.NeedsAttention }
                    SummaryStrip(listOf(
                        done.toString() to "done",
                        failed.toString() to "failed",
                        step.record.items.size.toString() to "total"))
                    if (failed > 0) {
                        Spacer(Modifier.height(Spacing.s2))
                        Text("$failed item(s) need attention — see Activity for detail.",
                            color = MaterialTheme.colorScheme.semantic.statusWarning)
                    }
                    step.record.error?.let {
                        Spacer(Modifier.height(Spacing.s2))
                        FriendlyError("Operation error", it.message)
                    }
                    Spacer(Modifier.height(Spacing.s4))
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                        TextButton(onClick = { nav.navigate(Routes.Activity) }) {
                            Text("View Activity")
                        }
                        if (step.record.items.any { it.undoRecipe != null }) {
                            OutlinedButton(onClick = { vm.undo(step.record.operationId) }) {
                                Text("Undo")
                            }
                            Spacer(Modifier.width(Spacing.s2))
                        }
                        Button(onClick = { vm.reset() }) { Text("Done") }
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

    if (explainer) {
        PermissionExplainer(
            title = "Folder access needed",
            body = "Android requires you to pick each folder FileMind can work in. " +
                "The next screen opens the system folder picker — choose the folder " +
                "you want organized. FileMind never touches folders you haven't granted.",
            onContinue = {
                explainer = false
                treePicker.launch(null)
            },
            onDismiss = { explainer = false })
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

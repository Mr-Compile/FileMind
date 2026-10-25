package com.filemind.app.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.clickable
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.FilterList
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.core.app.AnalyzedFile
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.OperationType
import com.filemind.core.contracts.SafetyResult

/** "Why?" explainability sheet — signals, score contributions, confidence. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WhySheet(analyzed: AnalyzedFile, onDismiss: () -> Unit) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            Modifier.fillMaxWidth().verticalScroll(rememberScrollState())
                .padding(Spacing.s4)
        ) {
            Text("Why this suggestion", style = MaterialTheme.typography.titleLarge)
            Spacer(Modifier.height(Spacing.s1))
            Text(analyzed.candidate.displayName,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(Spacing.s3))

            val band = when {
                analyzed.classification.confidence >= 95 -> "High"
                analyzed.classification.confidence >= 70 -> "Medium"
                else -> "Low"
            }
            Text("Confidence: ${"%.0f".format(analyzed.classification.confidence)} ($band)",
                style = MaterialTheme.typography.titleMedium)
            LinearProgressIndicator(
                progress = { (analyzed.classification.confidence / 100f).toFloat().coerceIn(0f, 1f) },
                modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s2)
            )
            Text(analyzed.classification.explanation,
                style = MaterialTheme.typography.bodyMedium)
            Spacer(Modifier.height(Spacing.s3))

            Text("Signals", style = MaterialTheme.typography.titleMedium)
            analyzed.classification.evidence.forEach { ev ->
                Row(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Text("+${"%.0f".format(ev.scoreContribution)}",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.semantic.statusInfo,
                        modifier = Modifier.weight(0.2f))
                    Column(Modifier.weight(0.8f)) {
                        Text(ev.detail, style = MaterialTheme.typography.bodyMedium)
                        Text("${ev.kind} · ${ev.source}",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            if (analyzed.classification.warnings.isNotEmpty()) {
                Spacer(Modifier.height(Spacing.s3))
                Text("Warnings", style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.semantic.statusWarning)
                analyzed.classification.warnings.forEach {
                    Text("• $it", style = MaterialTheme.typography.bodyMedium)
                }
            }
            Spacer(Modifier.height(Spacing.s4))
        }
    }
}

/** Required confirm step — counts, deletion statement, undo availability. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ConfirmSheet(
    plan: ActionPlan,
    safety: SafetyResult,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit
) {
    val moves = plan.items.count { it.operation == OperationType.Move }
    val copies = plan.items.count { it.operation == OperationType.Copy }
    val renames = plan.items.count { it.operation == OperationType.Rename }
    val folders = plan.items.count { it.operation == OperationType.CreateFolder }

    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
            Text("Confirm organization", style = MaterialTheme.typography.titleLarge)
            Spacer(Modifier.height(Spacing.s3))
            SummaryStrip(listOf(
                moves.toString() to "moves",
                copies.toString() to "copies",
                renames.toString() to "renames",
                folders.toString() to "folders"
            ))
            Spacer(Modifier.height(Spacing.s3))

            Text("0 deletions — FileMind does not delete files.",
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.SemiBold)
            Text(
                if (plan.reversibleEstimate)
                    "Every action has an undo recipe — you can undo this run afterward."
                else
                    "Some actions cannot be undone automatically — review them above before confirming.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            if (safety.warnings.isNotEmpty()) {
                Spacer(Modifier.height(Spacing.s2))
                safety.warnings.forEach {
                    Text("• $it", style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.semantic.statusWarning)
                }
            }
            Spacer(Modifier.height(Spacing.s4))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TextButton(onClick = onDismiss) { Text("Cancel") }
                Button(onClick = onConfirm) { Text("Confirm") }
            }
            Spacer(Modifier.height(Spacing.s4))
        }
    }
}

/** A single selectable option inside a [FilterGroup]. */
data class FilterOption(val value: String, val label: String, val supporting: String? = null)

/** One titled single-select section inside [FilterSheet]. */
data class FilterGroup(val title: String, val key: String, val options: List<FilterOption>)

/**
 * Filter/sort entry point — an outlined button carrying the count of active
 * (non-default) filters so the collapsed state stays glanceable.
 */
@Composable
fun FilterButton(activeCount: Int, onClick: () -> Unit, modifier: Modifier = Modifier) {
    OutlinedButton(onClick = onClick, modifier = modifier) {
        BadgedBox(badge = {
            if (activeCount > 0) Badge { Text(activeCount.toString()) }
        }) {
            Icon(Icons.Filled.FilterList, contentDescription = null)
        }
        Text(
            "Filters",
            modifier = Modifier.padding(start = Spacing.s2)
        )
    }
}

/**
 * M3 modal bottom sheet for filters — the phone-native replacement for
 * horizontally scrolling chip strips. Each [FilterGroup] is a single-select
 * section; taps apply immediately so the list updates live behind the sheet.
 * [extraContent] hosts free-text controls (e.g. an extension field) stacked
 * vertically — never side-by-side fields that would force horizontal space.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FilterSheet(
    title: String,
    groups: List<FilterGroup>,
    selected: Map<String, String>,
    onSelect: (groupKey: String, value: String) -> Unit,
    onClear: () -> Unit,
    onDismiss: () -> Unit,
    extraContent: (@Composable () -> Unit)? = null
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            Modifier.fillMaxWidth().verticalScroll(rememberScrollState())
                .padding(bottom = Spacing.s4)
        ) {
            Row(
                Modifier.fillMaxWidth().padding(horizontal = Spacing.s4),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(title, style = MaterialTheme.typography.titleLarge,
                    modifier = Modifier.weight(1f))
                TextButton(onClick = onClear) { Text("Clear all") }
            }
            extraContent?.let {
                Spacer(Modifier.height(Spacing.s2))
                Column(Modifier.padding(horizontal = Spacing.s4)) { it() }
            }
            groups.forEach { group ->
                Spacer(Modifier.height(Spacing.s2))
                Text(group.title,
                    style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(horizontal = Spacing.s4))
                group.options.forEach { opt ->
                    val isOn = selected[group.key] == opt.value
                    ListItem(
                        headlineContent = { Text(opt.label) },
                        supportingContent = opt.supporting?.let { { Text(it) } },
                        trailingContent = {
                            if (isOn) Icon(Icons.Filled.Check,
                                contentDescription = "Selected",
                                tint = MaterialTheme.colorScheme.primary)
                        },
                        modifier = Modifier.clickable { onSelect(group.key, opt.value) }
                    )
                }
                HorizontalDivider()
            }
            Row(
                Modifier.fillMaxWidth().padding(Spacing.s4),
                horizontalArrangement = Arrangement.End
            ) {
                Button(onClick = onDismiss) { Text("Done") }
            }
        }
    }
}

/** Explain-why explainer shown before the SAF folder picker. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PermissionExplainer(
    title: String,
    body: String,
    onContinue: () -> Unit,
    onDismiss: () -> Unit
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(Modifier.fillMaxWidth().padding(Spacing.s4)) {
            Text(title, style = MaterialTheme.typography.titleLarge)
            Spacer(Modifier.height(Spacing.s2))
            Text(body, style = MaterialTheme.typography.bodyMedium)
            Spacer(Modifier.height(Spacing.s4))
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                TextButton(onClick = onDismiss) { Text("Not now") }
                Button(onClick = onContinue) { Text("Choose folder") }
            }
            Spacer(Modifier.height(Spacing.s4))
        }
    }
}

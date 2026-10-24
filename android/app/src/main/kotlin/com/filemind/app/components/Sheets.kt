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
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
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

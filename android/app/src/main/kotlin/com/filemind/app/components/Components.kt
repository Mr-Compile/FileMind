package com.filemind.app.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Block
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.FolderOff
import androidx.compose.material.icons.filled.HourglassTop
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.filemind.app.theme.Radius
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.OperationStatus

/** A shared status vocabulary chip — always icon + label + color together. */
data class ChipSpec(val label: String, val icon: ImageVector, val color: Color)

@Composable
private fun chipColor(kind: String): Color {
    val s = MaterialTheme.colorScheme.semantic
    return when (kind) {
        "info" -> s.statusInfo
        "success" -> s.statusSuccess
        "warning" -> s.statusWarning
        "error" -> s.statusError
        "destructive" -> s.statusDestructive
        "offline" -> s.statusOffline
        else -> s.statusNeutral
    }
}

@Composable
fun chipSpec(kind: String, label: String, icon: ImageVector): ChipSpec =
    ChipSpec(label, icon, chipColor(kind))

/** Decision kind → shared chip (SHARED_UX_SYSTEM status vocabulary). */
@Composable
fun decisionChip(kind: DecisionKind): ChipSpec = when (kind) {
    DecisionKind.Organize -> chipSpec("success", "Ready", Icons.Filled.CheckCircle)
    DecisionKind.Review -> chipSpec("warning", "Needs Review", Icons.Filled.Warning)
    DecisionKind.Ignore -> chipSpec("neutral", "Ignored", Icons.Filled.FolderOff)
    DecisionKind.NoOp -> chipSpec("neutral", "No action", Icons.Filled.Block)
}

/** OperationStatus → shared chip (12-state op model). */
@Composable
fun operationChip(status: OperationStatus): ChipSpec = when (status) {
    OperationStatus.Planned -> chipSpec("info", "Ready", Icons.Filled.Schedule)
    OperationStatus.Validating -> chipSpec("info", "Checking", Icons.Filled.Sync)
    OperationStatus.Executing -> chipSpec("info", "Running", Icons.Filled.PlayCircle)
    OperationStatus.Verifying -> chipSpec("info", "Verifying", Icons.Filled.Sync)
    OperationStatus.Committed -> chipSpec("success", "Completed", Icons.Filled.CheckCircle)
    OperationStatus.Failed -> chipSpec("error", "Failed", Icons.Filled.Error)
    OperationStatus.RollingBack -> chipSpec("warning", "Rolling back", Icons.Filled.Sync)
    OperationStatus.RolledBack -> chipSpec("warning", "Undone", Icons.Filled.CheckCircle)
    OperationStatus.NeedsAttention -> chipSpec("warning", "Needs attention", Icons.Filled.Warning)
}

@Composable
fun plannedChip(): ChipSpec = chipSpec("offline", "Planned", Icons.Filled.HourglassTop)

@Composable
fun StatusChip(spec: ChipSpec, modifier: Modifier = Modifier) {
    Surface(
        color = spec.color.copy(alpha = 0.14f),
        shape = RoundedCornerShape(50),
        modifier = modifier
    ) {
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier.padding(horizontal = Spacing.s2, vertical = Spacing.s1)
        ) {
            Icon(spec.icon, contentDescription = null,
                tint = spec.color, modifier = Modifier.size(14.dp))
            Spacer(Modifier.width(Spacing.s1))
            Text(spec.label, color = spec.color,
                style = MaterialTheme.typography.labelMedium)
        }
    }
}

@Composable
fun EmptyState(
    title: String,
    body: String,
    actionLabel: String? = null,
    onAction: (() -> Unit)? = null,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier.fillMaxWidth().padding(Spacing.s6),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Icon(Icons.Filled.Info, contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(40.dp))
        Spacer(Modifier.height(Spacing.s3))
        Text(title, style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.height(Spacing.s1))
        Text(body, style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        if (actionLabel != null && onAction != null) {
            Spacer(Modifier.height(Spacing.s4))
            Button(onClick = onAction) { Text(actionLabel) }
        }
    }
}

@Composable
fun LoadingBox(label: String = "Loading", modifier: Modifier = Modifier) {
    Column(
        modifier = modifier.fillMaxWidth().padding(Spacing.s6),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        CircularProgressIndicator()
        Spacer(Modifier.height(Spacing.s3))
        Text(label, style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

@Composable
fun SectionHeader(title: String, modifier: Modifier = Modifier) {
    Text(title, style = MaterialTheme.typography.titleMedium,
        fontWeight = FontWeight.SemiBold,
        modifier = modifier.padding(vertical = Spacing.s2))
}

@Composable
fun FriendlyError(
    title: String,
    detail: String,
    recovery: String? = null,
    modifier: Modifier = Modifier
) {
    Card(
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.semantic.statusError.copy(alpha = 0.10f)),
        modifier = modifier.fillMaxWidth()
    ) {
        Row(Modifier.padding(Spacing.s4), verticalAlignment = Alignment.Top) {
            Icon(Icons.Filled.Error, contentDescription = null,
                tint = MaterialTheme.colorScheme.semantic.statusError)
            Spacer(Modifier.width(Spacing.s3))
            Column {
                Text(title, style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.semantic.statusError)
                Text(detail, style = MaterialTheme.typography.bodyMedium)
                if (recovery != null) {
                    Text(recovery, style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
fun SummaryStrip(items: List<Pair<String, String>>, modifier: Modifier = Modifier) {
    Row(modifier = modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
        items.forEach { (count, label) ->
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text(count, style = MaterialTheme.typography.titleLarge,
                    fontWeight = FontWeight.Bold)
                Text(label, style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

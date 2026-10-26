package com.filemind.app.components

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Block
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.FolderOff
import androidx.compose.material.icons.filled.FolderZip
import androidx.compose.material.icons.filled.HourglassTop
import androidx.compose.material.icons.filled.Image
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.InsertDriveFile
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.MusicNote
import androidx.compose.material.icons.filled.PlayCircle
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Terminal
import androidx.compose.material.icons.filled.TextFields
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
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.filemind.app.theme.Radius
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.OperationStatus

/** Shared snackbar channel — mirrors the mockup's FM.toast feedback. */
val LocalSnackbar = androidx.compose.runtime.staticCompositionLocalOf<
    androidx.compose.material3.SnackbarHostState> {
    error("No SnackbarHostState provided")
}

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

/** Section label — mirrors the mockup's .a-sec (uppercase caption label). */
@Composable
fun SectionHeader(title: String, modifier: Modifier = Modifier) {
    Text(title.uppercase(), style = MaterialTheme.typography.labelMedium,
        fontWeight = FontWeight.Bold,
        letterSpacing = 0.8.sp,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = modifier.padding(horizontal = Spacing.s1)
            .padding(top = Spacing.s2))
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

/** Pill search field — mirrors the mockup's .a-searchbar (48dp / 56dp lg). */
@Composable
fun SearchField(
    value: String,
    onValueChange: (String) -> Unit,
    placeholder: String,
    modifier: Modifier = Modifier,
    large: Boolean = false
) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceVariant,
        shape = RoundedCornerShape(50),
        modifier = modifier.height(if (large) 56.dp else 48.dp)
    ) {
        Row(
            Modifier.fillMaxSize().padding(horizontal = Spacing.s4),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(Icons.Filled.Search, contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(Spacing.s3))
            androidx.compose.foundation.text.BasicTextField(
                value = value,
                onValueChange = onValueChange,
                singleLine = true,
                textStyle = MaterialTheme.typography.bodyLarge.copy(
                    color = MaterialTheme.colorScheme.onSurface),
                modifier = Modifier.weight(1f),
                decorationBox = { inner ->
                    if (value.isEmpty()) {
                        Text(placeholder,
                            style = MaterialTheme.typography.bodyLarge,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    inner()
                }
            )
        }
    }
}

/** Flat list row — mirrors the mockup's .a-row (no card per item). */
@OptIn(ExperimentalFoundationApi::class)
@Composable
fun ListRow(
    title: String,
    modifier: Modifier = Modifier,
    subtitle: String? = null,
    icon: (@Composable () -> Unit)? = null,
    trailing: (@Composable () -> Unit)? = null,
    selected: Boolean = false,
    onClick: (() -> Unit)? = null,
    onLongClick: (() -> Unit)? = null
) {
    val bg = if (selected)
        MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
    else Color.Transparent
    val clickable = Modifier
        .fillMaxWidth()
        .heightIn(min = 64.dp)
        .background(bg, RoundedCornerShape(Radius.medium))
        .let { base ->
            if (onClick != null || onLongClick != null) {
                base.combinedClickable(onClick = { onClick?.invoke() },
                    onLongClick = { onLongClick?.invoke() })
            } else base
        }
    Row(
        modifier.then(clickable).padding(horizontal = Spacing.s2, vertical = Spacing.s1),
        verticalAlignment = Alignment.CenterVertically
    ) {
        if (icon != null) {
            icon()
            Spacer(Modifier.width(Spacing.s3))
        }
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge,
                fontWeight = FontWeight.Medium, maxLines = 1,
                overflow = TextOverflow.Ellipsis)
            if (subtitle != null) {
                Text(subtitle, style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis)
            }
        }
        trailing?.let { Spacer(Modifier.width(Spacing.s2)); it() }
    }
}

/** Confidence bar + percent — mirrors the mockup's FM.confBar. */
@Composable
fun ConfidenceBar(confidence: Double, modifier: Modifier = Modifier) {
    Row(modifier = modifier, verticalAlignment = Alignment.CenterVertically) {
        LinearProgressIndicator(
            progress = { (confidence / 100f).toFloat().coerceIn(0f, 1f) },
            modifier = Modifier.width(56.dp)
        )
        Spacer(Modifier.width(Spacing.s1))
        Text("${"%.0f".format(confidence)}%",
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
    }
}

/** Kind → icon for file rows (mockup parity: FM.fileIcon). */
fun fileKindIcon(extension: String): ImageVector = when (extension.lowercase()) {
    "jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "heic", "svg" -> Icons.Filled.Image
    "mp4", "mkv", "avi", "mov", "wmv", "webm", "m4v" -> Icons.Filled.PlayCircle
    "mp3", "wav", "flac", "aac", "ogg", "m4a", "wma" -> Icons.Filled.MusicNote
    "zip", "rar", "7z", "tar", "gz", "bz2" -> Icons.Filled.FolderZip
    "pdf", "doc", "docx", "txt", "rtf", "md", "odt",
    "xls", "xlsx", "csv", "ods", "ppt", "pptx", "odp", "key" -> Icons.Filled.Description
    "exe", "msi", "dll", "bat", "cmd", "com" -> Icons.Filled.Terminal
    "ttf", "otf", "woff", "woff2" -> Icons.Filled.TextFields
    else -> Icons.Filled.InsertDriveFile
}

package com.filemind.app.ui

import android.os.StatFs
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
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
import androidx.compose.material.icons.filled.Badge
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.DriveFileMove
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Badge
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.ListRow
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.semantic
import com.filemind.app.vm.HomeViewModel
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.OperationStatus

/** Refresh data whenever the screen resumes (post-organize changes). */
@Composable
private fun OnResume(onResumed: () -> Unit) {
    val owner = LocalLifecycleOwner.current
    DisposableEffect(owner) {
        val obs = LifecycleEventObserver { _, e ->
            if (e == Lifecycle.Event.ON_RESUME) onResumed()
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs) }
    }
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun HomeScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { HomeViewModel(it) }
    val state by vm.state.collectAsState()
    OnResume { vm.refresh() }

    val storage = remember {
        val dir = app.getExternalFilesDir(null) ?: app.filesDir
        StatFs(dir.absolutePath).let { it.totalBytes to it.availableBytes }
    }

    androidx.compose.foundation.layout.Box(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState())
                .padding(Spacing.s4)
        ) {
            if (state.loading) { LoadingBox("Preparing dashboard"); return@Column }

            // Hero
            Text("Your files, sorted themselves.",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold)
            Text("Offline-first organizer · on-device intelligence",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(Spacing.s3))

            // This-device storage card
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(Spacing.s4)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Filled.PhoneAndroid, contentDescription = null,
                            tint = MaterialTheme.colorScheme.primary)
                        Spacer(Modifier.width(Spacing.s3))
                        Text("This device", style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.weight(1f))
                        val (total, free) = storage
                        val pct = if (total > 0)
                            ((total - free) * 100 / total).toInt() else 0
                        StatusChip(if (pct > 85)
                            chipSpec("warning", "Nearly full", Icons.Filled.Warning)
                        else chipSpec("success", "Healthy", Icons.Filled.CheckCircle))
                    }
                    Spacer(Modifier.height(Spacing.s3))
                    val (total, free) = storage
                    val used = (total - free).coerceAtLeast(0)
                    LinearProgressIndicator(
                        progress = { if (total > 0) used / total.toFloat() else 0f },
                        modifier = Modifier.fillMaxWidth())
                    Spacer(Modifier.height(Spacing.s2))
                    Text("${formatBytes(used)} used of ${formatBytes(total)} · " +
                        "Folder access: ${state.grantCount} tree(s) granted",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            // Running-now card
            state.activeOps.firstOrNull()?.let { op ->
                Spacer(Modifier.height(Spacing.s3))
                Card(Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(Spacing.s4)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("Running now", style = MaterialTheme.typography.titleMedium,
                                modifier = Modifier.weight(1f))
                            StatusChip(operationChip(op.status))
                        }
                        val done = op.items.count {
                            it.status == OperationStatus.Committed ||
                                it.status == OperationStatus.Failed
                        }
                        Text("$done/${op.items.size} item(s)",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Spacer(Modifier.height(Spacing.s2))
                        LinearProgressIndicator(
                            progress = {
                                if (op.items.isNotEmpty())
                                    done / op.items.size.toFloat() else 0f
                            },
                            modifier = Modifier.fillMaxWidth())
                    }
                }
            }

            Spacer(Modifier.height(Spacing.s3))

            // Organize + Smart Inbox cards
            Card(onClick = { nav.navigate(Routes.Organizer) },
                modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.padding(Spacing.s4),
                    verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Filled.DriveFileMove, null,
                        tint = MaterialTheme.colorScheme.primary)
                    Spacer(Modifier.width(Spacing.s3))
                    Column(Modifier.weight(1f)) {
                        Text("Organize", style = MaterialTheme.typography.titleMedium)
                        Text("Scan a folder, preview the plan, approve — " +
                            "every step is reversible.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Spacer(Modifier.height(Spacing.s2))
            Card(onClick = { nav.navigate(Routes.Activity) },
                modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.padding(Spacing.s4),
                    verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Filled.Warning, null,
                        tint = MaterialTheme.colorScheme.semantic.statusWarning)
                    Spacer(Modifier.width(Spacing.s3))
                    Column(Modifier.weight(1f)) {
                        Text("Smart Inbox", style = MaterialTheme.typography.titleMedium)
                        Text(if (state.inboxCount > 0)
                            "${state.inboxCount} file(s) need your call."
                        else "Nothing waiting for review.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (state.inboxCount > 0) {
                        Badge { Text("${state.inboxCount}") }
                    } else {
                        Icon(Icons.Filled.ChevronRight, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
            Spacer(Modifier.height(Spacing.s2))
            Card(onClick = { nav.navigate("devices") },
                modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.padding(Spacing.s4),
                    verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Filled.Devices, null,
                        tint = MaterialTheme.colorScheme.primary)
                    Spacer(Modifier.width(Spacing.s3))
                    Column(Modifier.weight(1f)) {
                        Text("Devices", style = MaterialTheme.typography.titleMedium)
                        Text("This device — storage and folder access",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    StatusChip(chipSpec("success", "Connected", Icons.Filled.CheckCircle))
                }
            }
            Spacer(Modifier.height(Spacing.s2))

            // Cleanup opportunities (computed from the index)
            Card(onClick = { nav.navigate(Routes.Tools) },
                modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(Spacing.s4)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Filled.ContentCopy, null,
                            tint = MaterialTheme.colorScheme.primary)
                        Spacer(Modifier.width(Spacing.s3))
                        Text("Cleanup opportunities",
                            style = MaterialTheme.typography.titleMedium)
                    }
                    Spacer(Modifier.height(Spacing.s2))
                    Text("${state.dupePairs} likely duplicate pair(s)",
                        style = MaterialTheme.typography.bodyMedium)
                    Text("${state.bigFiles} file(s) over 3 GB",
                        style = MaterialTheme.typography.bodyMedium)
                    Text("${state.indexedCount} file(s) indexed",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }

            Spacer(Modifier.height(Spacing.s3))

            // Quick chips
            FlowRow(horizontalArrangement = Arrangement.spacedBy(Spacing.s2)) {
                listOf(
                    Triple("Search", Icons.Filled.Search, Routes.Search),
                    Triple("Tools", Icons.Filled.ContentCopy, Routes.Tools),
                    Triple("Devices", Icons.Filled.Devices, "devices"),
                    Triple("Help", Icons.Filled.Info, Routes.Help)
                ).forEach { (label, icon, dest) ->
                    OutlinedButton(onClick = { nav.navigate(dest) }) {
                        Icon(icon, null, modifier = Modifier.height(18.dp))
                        Spacer(Modifier.width(Spacing.s1))
                        Text(label)
                    }
                }
            }

            // Recent activity
            SectionHeader("Recent activity")
            if (state.recentOps.isEmpty()) {
                EmptyState(
                    title = "Nothing organized yet",
                    body = "Run the organizer on a folder and results will appear here.",
                    actionLabel = "Open organizer",
                    onAction = { nav.navigate(Routes.Organizer) })
            } else {
                state.recentOps.take(3).forEach { op ->
                    ListRow(
                        title = "Organize · ${op.items.size} item(s)",
                        subtitle = formatInstant(op.createdAt),
                        icon = { Icon(Icons.Filled.History, null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                        trailing = { StatusChip(operationChip(op.status)) },
                        onClick = { nav.navigate(Routes.Activity) })
                }
            }
            Spacer(Modifier.height(Spacing.s12))
        }

        ExtendedFloatingActionButton(
            onClick = { nav.navigate(Routes.Organizer) },
            icon = { Icon(Icons.Filled.DriveFileMove, null) },
            text = { Text("Organize") },
            modifier = Modifier.align(Alignment.BottomEnd).padding(Spacing.s4))
    }
}

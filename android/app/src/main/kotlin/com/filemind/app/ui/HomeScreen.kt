package com.filemind.app.ui

import android.net.Uri
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.EmptyState
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.operationChip
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.HomeViewModel
import com.filemind.app.vm.backendViewModel
import android.os.StatFs

@Composable
fun HomeScreen(app: FileMindApp, nav: NavHostController) {
    val vm = backendViewModel(app) { HomeViewModel(it) }
    val state by vm.state.collectAsState()

    val storage = remember {
        val dir = app.getExternalFilesDir(null) ?: app.filesDir
        StatFs(dir.absolutePath).let { it.totalBytes to it.availableBytes }
    }

    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState())
            .padding(Spacing.s4)
    ) {
        if (state.loading) { LoadingBox("Preparing dashboard"); return@Column }

        // Storage overview card
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(Spacing.s4)) {
                Text("Storage", style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(Spacing.s2))
                val (total, free) = storage
                val used = (total - free).coerceAtLeast(0)
                LinearProgressIndicator(
                    progress = { if (total > 0) used / total.toFloat() else 0f },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(Spacing.s2))
                Text("${formatBytes(used)} used of ${formatBytes(total)}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

        Spacer(Modifier.height(Spacing.s3))

        // Status cards row
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(Spacing.s3)) {
            Card(modifier = Modifier.weight(1f), onClick = { nav.navigate("devices") }) {
                Column(Modifier.padding(Spacing.s4)) {
                    Text("Folder access", style = MaterialTheme.typography.titleMedium)
                    Text("${state.grantCount} granted",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Card(modifier = Modifier.weight(1f), onClick = { nav.navigate(Routes.Files) }) {
                Column(Modifier.padding(Spacing.s4)) {
                    Text("Indexed files", style = MaterialTheme.typography.titleMedium)
                    Text("${state.indexedCount}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        Spacer(Modifier.height(Spacing.s3))

        // Quick actions
        SectionHeader("Quick actions")
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(Spacing.s3)) {
            Card(modifier = Modifier.weight(1f), onClick = { nav.navigate(Routes.Organizer) }) {
                Column(Modifier.padding(Spacing.s4)) {
                    Text("Organize", style = MaterialTheme.typography.titleMedium)
                    Text("Scan a folder and review suggestions",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
            Card(modifier = Modifier.weight(1f), onClick = { nav.navigate(Routes.Activity) }) {
                Column(Modifier.padding(Spacing.s4)) {
                    Text("Review inbox", style = MaterialTheme.typography.titleMedium)
                    Text("${state.inboxCount} need decisions",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        Spacer(Modifier.height(Spacing.s3))

        // Active operations
        if (state.activeOps.isNotEmpty()) {
            SectionHeader("Active operations")
            state.activeOps.forEach { op ->
                Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Row(Modifier.padding(Spacing.s3),
                        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("${op.items.size} item(s)",
                                style = MaterialTheme.typography.bodyLarge)
                            Text(formatInstant(op.createdAt),
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        StatusChip(operationChip(op.status))
                    }
                }
            }
            Spacer(Modifier.height(Spacing.s3))
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
            state.recentOps.forEach { op ->
                Card(modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s1),
                    onClick = { nav.navigate(Routes.Activity) }) {
                    Row(Modifier.padding(Spacing.s3),
                        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text("${op.items.size} item(s)",
                                style = MaterialTheme.typography.bodyLarge)
                            Text(formatInstant(op.createdAt),
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        StatusChip(operationChip(op.status))
                        Icon(Icons.Filled.ChevronRight, contentDescription = null,
                            tint = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
    }
}

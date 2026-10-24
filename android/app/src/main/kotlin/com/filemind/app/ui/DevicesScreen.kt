package com.filemind.app.ui

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
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import com.filemind.app.FileMindApp
import com.filemind.app.components.LoadingBox
import com.filemind.app.components.SectionHeader
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.DevicesViewModel
import com.filemind.app.vm.backendViewModel

@Composable
fun DevicesScreen(app: FileMindApp) {
    val vm = backendViewModel(app) { DevicesViewModel(it) }
    val state by vm.state.collectAsState()

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {
        if (state.loading) { LoadingBox("Checking device"); return@Column }

        SectionHeader("This device")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(Spacing.s4)) {
                Text(state.model, style = MaterialTheme.typography.titleMedium)
                Text("Android API ${state.sdk}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s3))
                val used = (state.totalBytes - state.freeBytes).coerceAtLeast(0)
                LinearProgressIndicator(
                    progress = {
                        if (state.totalBytes > 0) used / state.totalBytes.toFloat() else 0f
                    },
                    modifier = Modifier.fillMaxWidth())
                Spacer(Modifier.height(Spacing.s1))
                Text("${formatBytes(used)} used of ${formatBytes(state.totalBytes)}",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Folder access (SAF grants)")
        Text("FileMind can only reach folders you grant. Revoke any grant here or " +
            "in system Settings → Apps → FileMind.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(Spacing.s2))
        if (state.grants.isEmpty()) {
            Text("No folders granted yet — the organizer asks for access before scanning.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        } else {
            state.grants.forEach { g ->
                Card(Modifier.fillMaxWidth().padding(vertical = Spacing.s1)) {
                    Row(Modifier.padding(Spacing.s3),
                        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                        Column(Modifier.weight(1f)) {
                            Text(shortPath(g.treeUri, 56),
                                style = MaterialTheme.typography.bodyMedium, maxLines = 1)
                            Text(
                                buildString {
                                    append(if (g.canRead) "read" else "no-read")
                                    append(if (g.canWrite) " + write" else "")
                                    if (!g.persisted) append(" · not persisted")
                                },
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        TextButton(onClick = { vm.revoke(g.treeUri) }) { Text("Revoke") }
                    }
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Capabilities")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(Spacing.s4)) {
                Text("• SAF folders: read/write/move/rename while the grant lives")
                Text("• MediaStore: read/write/rename/move own media rows")
                Text("• Share target: received files go to the Smart Inbox")
                Text("• Cross-provider moves are refused (same-provider only)",
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

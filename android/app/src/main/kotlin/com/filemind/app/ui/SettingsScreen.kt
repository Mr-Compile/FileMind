package com.filemind.app.ui

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.platform.LocalContext
import androidx.navigation.NavHostController
import com.filemind.app.FileMindApp
import com.filemind.app.components.PermissionExplainer
import com.filemind.app.components.SectionHeader
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.ThemeMode
import com.filemind.core.contracts.OrganizationMode

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(app: FileMindApp, nav: NavHostController) {
    val context = LocalContext.current
    val settings = app.settingsViewModel
    val theme by settings.themeMode.collectAsState()
    val orgMode by settings.orgMode.collectAsState()
    var notifExplainer by remember { mutableStateOf(false) }

    val notifPermission = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { }

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {

        SectionHeader("General")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(Spacing.s4)) {
                Text("Organization mode", style = MaterialTheme.typography.titleMedium)
                Text("How the organizer treats confident suggestions.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
                Spacer(Modifier.height(Spacing.s2))
                /* Segmented row fills width — no horizontal scroll on phones. */
                SingleChoiceSegmentedButtonRow(Modifier.fillMaxWidth()) {
                    listOf(OrganizationMode.Manual, OrganizationMode.Smart,
                        OrganizationMode.Automatic).forEachIndexed { i, m ->
                        SegmentedButton(
                            selected = orgMode == m,
                            onClick = { settings.setOrgMode(m) },
                            shape = SegmentedButtonDefaults.itemShape(i, 3),
                            label = { Text(m.name) })
                    }
                }
                Spacer(Modifier.height(Spacing.s1))
                Text("AI-assisted and local-only modes are planned — the AI " +
                    "provider isn't shipped on Android yet.",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Appearance")
        Card(Modifier.fillMaxWidth()) {
            SingleChoiceSegmentedButtonRow(
                Modifier.fillMaxWidth().padding(Spacing.s4)) {
                ThemeMode.entries.forEachIndexed { i, m ->
                    SegmentedButton(
                        selected = theme == m,
                        onClick = { settings.setTheme(m) },
                        shape = SegmentedButtonDefaults.itemShape(i, ThemeMode.entries.size),
                        label = { Text(m.name) })
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Permissions")
        Card(modifier = Modifier.fillMaxWidth(), onClick = { nav.navigate("devices") }) {
            Row(Modifier.padding(Spacing.s4),
                verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Folder access", style = MaterialTheme.typography.titleMedium)
                    Text("Manage granted folders (SAF)",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Icon(Icons.Filled.ChevronRight, contentDescription = null)
            }
        }
        Spacer(Modifier.height(Spacing.s2))
        Card(Modifier.fillMaxWidth()) {
            Row(Modifier.padding(Spacing.s4),
                verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Notifications", style = MaterialTheme.typography.titleMedium)
                    val granted = Build.VERSION.SDK_INT < 33 ||
                        context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) ==
                            PackageManager.PERMISSION_GRANTED
                    Text(if (granted) "Allowed" else "Not granted",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                if (Build.VERSION.SDK_INT >= 33 &&
                    context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) !=
                        PackageManager.PERMISSION_GRANTED) {
                    TextButton(onClick = { notifExplainer = true }) { Text("Allow") }
                }
            }
        }

        Spacer(Modifier.height(Spacing.s4))
        SectionHeader("Help & About")
        Card(modifier = Modifier.fillMaxWidth(), onClick = { nav.navigate(Routes.Help) }) {
            Row(Modifier.padding(Spacing.s4),
                verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Help & About", style = MaterialTheme.typography.titleMedium)
                    Text("About, guides, diagnostics, credits",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Icon(Icons.Filled.ChevronRight, contentDescription = null)
            }
        }
        Spacer(Modifier.height(Spacing.s4))
    }

    if (notifExplainer) {
        PermissionExplainer(
            title = "Operation notifications",
            body = "FileMind posts progress for long-running organize jobs and " +
                "alerts when a run needs attention. Nothing else is sent.",
            onContinue = {
                notifExplainer = false
                notifPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
            },
            onDismiss = { notifExplainer = false })
    }
}

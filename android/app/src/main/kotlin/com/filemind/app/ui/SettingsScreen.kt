package com.filemind.app.ui

import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Devices
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Rule
import androidx.compose.material.icons.filled.Storage
import androidx.compose.material.icons.filled.Tune
import androidx.compose.material.icons.filled.Update
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
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
import com.filemind.app.components.ListRow
import com.filemind.app.components.PermissionExplainer
import com.filemind.app.components.PickerOption
import com.filemind.app.components.PickerSheet
import com.filemind.app.components.SectionHeader
import com.filemind.app.components.StatusChip
import com.filemind.app.components.chipSpec
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.theme.ThemeMode
import com.filemind.app.vm.backendViewModel
import com.filemind.core.contracts.OrganizationMode

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(app: FileMindApp, nav: NavHostController) {
    val context = LocalContext.current
    val settings = app.settingsViewModel
    val theme by settings.themeMode.collectAsState()
    val orgMode by settings.orgMode.collectAsState()
    val grantsVm = backendViewModel(app) { com.filemind.app.vm.DevicesViewModel(it) }
    val devices by grantsVm.state.collectAsState()

    var notifExplainer by remember { mutableStateOf(false) }
    var showThemePicker by remember { mutableStateOf(false) }
    var showModePicker by remember { mutableStateOf(false) }
    var showFolderPicker by remember { mutableStateOf(false) }

    val notifPermission = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { }

    val notifGranted = Build.VERSION.SDK_INT < 33 ||
        context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) ==
            PackageManager.PERMISSION_GRANTED

    val version = remember {
        runCatching {
            context.packageManager.getPackageInfo(context.packageName, 0)
                .versionName ?: "dev"
        }.getOrDefault("dev")
    }

    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
        .padding(Spacing.s4)) {

        // General
        SectionHeader("General")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Appearance",
                    subtitle = when (theme) {
                        ThemeMode.System -> "Follow system"
                        ThemeMode.Light -> "Light"
                        ThemeMode.Dark -> "Dark"
                    },
                    icon = { Icon(Icons.Filled.DarkMode, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { showThemePicker = true })
                ListRow(
                    title = "Default folder",
                    subtitle = devices.grants.firstOrNull()?.let { shortPath(it.treeUri, 40) }
                        ?: "None granted yet",
                    icon = { Icon(Icons.Filled.Folder, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { showFolderPicker = true })
            }
        }

        // Organization
        SectionHeader("Organization")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Organization mode",
                    subtitle = when (orgMode) {
                        OrganizationMode.Manual -> "Manual — you approve every run"
                        OrganizationMode.Smart -> "Smart — confident moves run after preview"
                        OrganizationMode.Automatic -> "Automatic — runs without asking"
                        OrganizationMode.AiAssisted -> "AI-assisted — provider not shipped yet"
                        OrganizationMode.LocalOnly -> "Local-only — on-device rules only"
                    },
                    icon = { Icon(Icons.Filled.Tune, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { showModePicker = true })
                ListRow(
                    title = "Rules",
                    subtitle = "Toggle organization rules on or off",
                    icon = { Icon(Icons.Filled.Rule, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { nav.navigate(Routes.Tools) })
            }
        }

        // Notifications
        SectionHeader("Notifications")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Operation alerts",
                    subtitle = if (notifGranted)
                        "Allowed — progress and attention alerts for long runs"
                    else "Not granted — organize runs complete silently",
                    icon = { Icon(Icons.Filled.Notifications, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = {
                        if (!notifGranted && Build.VERSION.SDK_INT >= 33) {
                            TextButton(onClick = { notifExplainer = true }) {
                                Text("Allow")
                            }
                        } else {
                            StatusChip(chipSpec("success", "On",
                                Icons.Filled.Notifications))
                        }
                    })
            }
        }

        // Privacy & access
        SectionHeader("Privacy & access")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Folder access",
                    subtitle = "${devices.grants.size} folder(s) granted — " +
                        "FileMind only touches folders you pick",
                    icon = { Icon(Icons.Filled.Folder, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { nav.navigate("devices") })
                ListRow(
                    title = "On-device only",
                    subtitle = "No cloud account — names and metadata stay on this phone",
                    icon = { Icon(Icons.Filled.Lock, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) })
            }
        }

        // Advanced
        SectionHeader("Advanced")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Devices & diagnostics",
                    subtitle = "Device info, storage, granted folders",
                    icon = { Icon(Icons.Filled.Devices, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { nav.navigate("devices") })
                // Same Advanced row as the Windows settings — the real Room DB.
                ListRow(
                    title = "Local database",
                    subtitle = "filemind.db (Room) · schema v2",
                    icon = { Icon(Icons.Filled.Storage, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) })
            }
        }

        // Updates
        SectionHeader("Updates")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "App updates",
                    subtitle = "No auto-update channel — new versions install manually",
                    icon = { Icon(Icons.Filled.Update, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) })
            }
        }

        // Help & About
        SectionHeader("Help & About")
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(vertical = Spacing.s1)) {
                ListRow(
                    title = "Help & guides",
                    subtitle = "How organizing, the inbox, and undo work",
                    icon = { Icon(Icons.Filled.Info, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    trailing = { Icon(Icons.Filled.ChevronRight, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) },
                    onClick = { nav.navigate(Routes.Help) })
                ListRow(
                    title = "FileMind",
                    subtitle = "Version $version · offline-first file organizer",
                    icon = { Icon(Icons.Filled.Info, null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant) })
            }
        }
        Spacer(Modifier.height(Spacing.s12))
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

    if (showThemePicker) {
        PickerSheet(
            title = "Appearance",
            options = listOf(
                PickerOption(ThemeMode.System.name, "Follow system",
                    "Match the device theme"),
                PickerOption(ThemeMode.Light.name, "Light"),
                PickerOption(ThemeMode.Dark.name, "Dark")),
            selected = theme.name,
            onPick = { settings.setTheme(ThemeMode.valueOf(it)) },
            onDismiss = { showThemePicker = false })
    }

    if (showModePicker) {
        PickerSheet(
            title = "Organization mode",
            options = listOf(
                PickerOption(OrganizationMode.Manual.name, "Manual",
                    "You approve every organize run"),
                PickerOption(OrganizationMode.Smart.name, "Smart",
                    "Confident moves run after a preview"),
                PickerOption(OrganizationMode.Automatic.name, "Automatic",
                    "Runs without asking — undo stays available"),
                PickerOption(OrganizationMode.AiAssisted.name, "AI-assisted",
                    "Planned — the AI provider isn't shipped on Android yet"),
                PickerOption(OrganizationMode.LocalOnly.name, "Local-only",
                    "On-device rules only — no remote features")),
            selected = orgMode.name,
            onPick = { settings.setOrgMode(OrganizationMode.valueOf(it)) },
            onDismiss = { showModePicker = false })
    }

    if (showFolderPicker) {
        PickerSheet(
            title = "Default folder",
            options = devices.grants.map {
                PickerOption(it.treeUri, shortPath(it.treeUri, 44), "Granted folder")
            },
            selected = devices.grants.firstOrNull()?.treeUri,
            onPick = { },
            onDismiss = { showFolderPicker = false })
    }
}

package com.filemind.app.ui

import android.content.Intent
import android.os.Build
import android.provider.Settings
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
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.ClipEntry
import androidx.compose.ui.platform.LocalClipboard
import androidx.compose.ui.platform.LocalContext
import kotlinx.coroutines.launch
import androidx.navigation.NavHostController
import com.filemind.app.BuildConfig
import com.filemind.app.FileMindApp
import com.filemind.app.components.SectionHeader
import com.filemind.app.nav.Routes
import com.filemind.app.theme.Spacing
import com.filemind.app.vm.backendViewModel
import com.filemind.app.vm.DevicesViewModel

/** Compact Help & About destinations (crossplatform-ux/04_HELP_ABOUT). */
object HelpScreens {

    @Composable
    private fun Screen(body: @Composable () -> Unit) {
        Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState())
            .padding(Spacing.s4)) { body() }
    }

    @Composable
    private fun Item(title: String, sub: String, onClick: () -> Unit) {
        Card(modifier = Modifier.fillMaxWidth().padding(vertical = Spacing.s1), onClick = onClick) {
            Row(Modifier.padding(Spacing.s3),
                verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(title, style = MaterialTheme.typography.titleMedium)
                    Text(sub, style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                Icon(Icons.Filled.ChevronRight, contentDescription = null)
            }
        }
    }

    @Composable
    fun Hub(nav: NavHostController) {
        Screen {
            Item("About FileMind", "Version, purpose, links") { nav.navigate(Routes.HelpAbout) }
            Item("Getting Started", "Your first organize run") { nav.navigate(Routes.HelpGettingStarted) }
            Item("User Guide", "Tasks and features") { nav.navigate(Routes.HelpGuide) }
            Item("What's New", "Release highlights") { nav.navigate(Routes.HelpWhatsNew) }
            Item("Troubleshooting", "Common problems and fixes") { nav.navigate(Routes.HelpTroubleshooting) }
            Item("Diagnostics", "Support info and health checks") { nav.navigate(Routes.HelpDiagnostics) }
            Item("Feedback", "Report a problem or suggest an idea") { nav.navigate(Routes.HelpFeedback) }
            Item("Credits", "The people and project") { nav.navigate(Routes.HelpCredits) }
            Item("Licenses", "Third-party notices") { nav.navigate(Routes.HelpLicenses) }
        }
    }

    @Composable
    fun About(nav: NavHostController) {
        Screen {
            Text("FileMind", style = MaterialTheme.typography.headlineLarge)
            Text("Version ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})",
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(Spacing.s3))
            Text("FileMind organizes your files with rules you control — " +
                "it analyzes first, shows the plan, and only acts after you confirm. " +
                "Everything runs on your device; nothing is uploaded.",
                style = MaterialTheme.typography.bodyMedium)
            Spacer(Modifier.height(Spacing.s3))
            Item("Getting Started", "Learn the workflow") { nav.navigate(Routes.HelpGettingStarted) }
            Item("What's New", "Latest changes") { nav.navigate(Routes.HelpWhatsNew) }
            Item("Diagnostics", "Support information") { nav.navigate(Routes.HelpDiagnostics) }
            Item("Licenses", "Open-source notices") { nav.navigate(Routes.HelpLicenses) }
        }
    }

    @Composable
    fun GettingStarted(nav: NavHostController) {
        Screen {
            SectionHeader("How FileMind works")
            listOf(
                "1. Pick a folder" to "Grant access to a folder from the Organizer. Android asks you to confirm — FileMind can only see folders you grant.",
                "2. Analyze" to "FileMind scans and suggests where each file belongs, with a confidence score and the signals behind it.",
                "3. Review" to "Check the plan. Anything unclear lands in Needs Review instead of moving.",
                "4. Confirm" to "See exact counts — and the promise that nothing gets deleted.",
                "5. Result & Undo" to "Watch progress, then undo the whole run from Activity if needed."
            ).forEach { (t, b) ->
                Spacer(Modifier.height(Spacing.s2))
                Text(t, style = MaterialTheme.typography.titleMedium)
                Text(b, style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Spacer(Modifier.height(Spacing.s4))
            Button(onClick = { nav.navigate(Routes.Organizer) }) { Text("Start organizing") }
        }
    }

    @Composable
    fun Guide(nav: NavHostController) {
        Screen {
            SectionHeader("User guide")
            listOf(
                "Browsing" to "Files lists everything FileMind has indexed. Long-press to select, tap for details.",
                "Search" to "Search covers file name and path in the index. Content search is planned.",
                "Organizing" to "The organizer analyzes, previews, then executes after your confirm. Smart Inbox catches files FileMind isn't sure about.",
                "Duplicates" to "Planned — duplicate detection isn't on Android yet.",
                "Activity & Undo" to "Activity shows running and finished operations with undo where supported.",
                "Privacy" to "No account, no telemetry. Diagnostics never include file names or paths unless you opt in.",
                "Settings" to "Organization mode and appearance live under Settings."
            ).forEach { (t, b) ->
                Spacer(Modifier.height(Spacing.s2))
                Text(t, style = MaterialTheme.typography.titleMedium)
                Text(b, style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }

    @Composable
    fun WhatsNew() {
        Screen {
            SectionHeader("What's new")
            Text("First Android release — the full FileMind experience: " +
                "organize flow, activity center, smart inbox, and help, running " +
                "entirely on your device.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    @Composable
    fun Troubleshooting(nav: NavHostController) {
        val context = LocalContext.current
        Screen {
            SectionHeader("Common problems")
            listOf(
                "A folder stopped being reachable" to "The grant may have been revoked. Re-check folder access under Settings → Permissions.",
                "A scan finds nothing" to "The folder may be empty or the grant may cover a different tree. Pick the folder again from Organizer.",
                "Items need attention" to "Open Activity — failed items keep their reason and can be undone where a recipe exists.",
                "Files didn't move" to "Manual mode only previews until you confirm. Nothing moves without an explicit confirm."
            ).forEach { (t, b) ->
                Spacer(Modifier.height(Spacing.s2))
                Text(t, style = MaterialTheme.typography.titleMedium)
                Text(b, style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Spacer(Modifier.height(Spacing.s4))
            Item("Diagnostics", "Copy safe support info") { nav.navigate(Routes.HelpDiagnostics) }
            TextButton(onClick = {
                context.startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
                    .setData(android.net.Uri.fromParts("package", context.packageName, null)))
            }) { Text("Open system app settings") }
        }
    }

    @Composable
    fun Diagnostics(app: FileMindApp) {
        val vm = backendViewModel(app) { DevicesViewModel(it) }
        val state by vm.state.collectAsState()
        val clipboard = LocalClipboard.current
        val scope = rememberCoroutineScope()
        var includePaths by remember { mutableStateOf(false) }
        var copied by remember { mutableStateOf(false) }

        Screen {
            SectionHeader("Diagnostics")
            Text("Safe by default: no file names or paths are included unless " +
                "you opt in below.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(Spacing.s3))
            Card(Modifier.fillMaxWidth()) {
                Column(Modifier.padding(Spacing.s4)) {
                    Text("App version: ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})")
                    Text("Device: ${state.model}")
                    Text("Android SDK: ${state.sdk}")
                    Text("Storage free: ${formatBytes(state.freeBytes)} / ${formatBytes(state.totalBytes)}")
                    Text("Folder grants: ${state.grants.size}")
                }
            }
            Spacer(Modifier.height(Spacing.s2))
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("Include granted folder identifiers",
                    modifier = Modifier.weight(1f),
                    style = MaterialTheme.typography.bodyMedium)
                androidx.compose.material3.Switch(
                    checked = includePaths, onCheckedChange = { includePaths = it })
            }
            Spacer(Modifier.height(Spacing.s3))
            Button(onClick = {
                val info = buildString {
                    appendLine("FileMind ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE})")
                    appendLine("Device: ${state.model} · SDK ${state.sdk}")
                    appendLine("Storage: ${formatBytes(state.freeBytes)} free of ${formatBytes(state.totalBytes)}")
                    appendLine("Folder grants: ${state.grants.size}")
                    if (includePaths) {
                        state.grants.forEach { appendLine("grant: ${it.docId}") }
                    }
                }
                scope.launch {
                    clipboard.setClipEntry(ClipEntry(android.content.ClipData
                        .newPlainText("FileMind diagnostics", info)))
                }
                copied = true
            }) { Text(if (copied) "Copied" else "Copy diagnostic info") }
        }
    }

    @Composable
    fun Feedback() {
        Screen {
            SectionHeader("Feedback")
            Text("Feedback collection isn't wired yet — this release keeps " +
                "everything local. Use Diagnostics → Copy diagnostic info and " +
                "share it through your usual channel.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    @Composable
    fun Credits() {
        Screen {
            SectionHeader("Credits")
            Text("FileMind — local-first file organization.",
                style = MaterialTheme.typography.bodyMedium)
            Text("Built with Jetpack Compose, Room, and WorkManager.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }

    @Composable
    fun Licenses() {
        Screen {
            SectionHeader("Licenses")
            listOf(
                "AndroidX libraries" to "Apache License 2.0",
                "Jetpack Compose" to "Apache License 2.0",
                "Kotlin" to "Apache License 2.0",
                "Room / WorkManager" to "Apache License 2.0"
            ).forEach { (t, l) ->
                Spacer(Modifier.height(Spacing.s2))
                Text(t, style = MaterialTheme.typography.titleMedium)
                Text(l, style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

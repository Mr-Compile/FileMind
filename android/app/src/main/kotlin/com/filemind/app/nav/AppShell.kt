package com.filemind.app.nav

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.BrightnessAuto
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Folder
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.adaptive.navigationsuite.NavigationSuiteScaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.filemind.app.FileMindApp
import com.filemind.app.ui.ActivityScreen
import com.filemind.app.ui.DevicesScreen
import com.filemind.app.ui.FilesScreen
import com.filemind.app.ui.HelpScreens
import com.filemind.app.ui.HomeScreen
import com.filemind.app.ui.OrganizerScreen
import com.filemind.app.ui.SearchScreen
import com.filemind.app.ui.SettingsScreen
import com.filemind.app.ui.ToolsScreen

private val titles = mapOf(
    Routes.Home to "FileMind",
    Routes.Files to "Files",
    Routes.Search to "Search",
    Routes.Tools to "Tools",
    Routes.Activity to "Activity",
    Routes.Settings to "Settings",
    Routes.Organizer to "Organizer",
    Routes.Help to "Help & About",
    Routes.HelpAbout to "About FileMind",
    Routes.HelpGettingStarted to "Getting Started",
    Routes.HelpGuide to "User Guide",
    Routes.HelpWhatsNew to "What's New",
    Routes.HelpTroubleshooting to "Troubleshooting",
    Routes.HelpDiagnostics to "Diagnostics",
    Routes.HelpFeedback to "Feedback",
    Routes.HelpCredits to "Credits",
    Routes.HelpLicenses to "Licenses",
    "devices" to "Devices"
)

private val navIcons = mapOf(
    Routes.Home to Icons.Filled.Home,
    Routes.Files to Icons.Filled.Folder,
    Routes.Search to Icons.Filled.Search,
    Routes.Activity to Icons.Filled.History,
    Routes.Settings to Icons.Filled.Settings
)

private val navLabels = mapOf(
    Routes.Home to "Home",
    Routes.Files to "Files",
    Routes.Search to "Search",
    Routes.Activity to "Activity",
    Routes.Settings to "Settings"
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppShell(app: FileMindApp) {
    val nav = rememberNavController()
    val backEntry by nav.currentBackStackEntryAsState()
    val route = backEntry?.destination?.route ?: Routes.Home
    val snackbar = remember { SnackbarHostState() }
    val theme by app.settingsViewModel.themeMode.collectAsState()
    var inboxCount by remember { mutableIntStateOf(0) }

    // Open-inbox badge — refreshed on route change (mockup's #opcount).
    LaunchedEffect(route) {
        inboxCount = app.backend.smartInbox.getOpen().value?.size ?: 0
    }

    CompositionLocalProvider(com.filemind.app.components.LocalSnackbar provides snackbar) {
    NavigationSuiteScaffold(
        navigationSuiteItems = {
            Routes.primary.forEach { dest ->
                item(
                    selected = route == dest,
                    onClick = {
                        nav.navigate(dest) {
                            popUpTo(nav.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(navIcons[dest]!!, contentDescription = navLabels[dest]) },
                    label = { Text(navLabels[dest]!!) }
                )
            }
        }
    ) {
        Scaffold(
            snackbarHost = { SnackbarHost(snackbar) },
            topBar = {
                // No Up affordance in the app bar — Android's system Back
                // (gesture/button) unwinds the NavController stack.
                TopAppBar(
                    title = { Text(titles[route] ?: "FileMind") },
                    actions = {
                        IconButton(onClick = { nav.navigate(Routes.Search) }) {
                            Icon(Icons.Filled.Search, contentDescription = "Search")
                        }
                        IconButton(onClick = { nav.navigate(Routes.Activity) }) {
                            BadgedBox(badge = {
                                if (inboxCount > 0) Badge { Text("$inboxCount") }
                            }) {
                                Icon(Icons.Filled.History,
                                    contentDescription = "Operation center")
                            }
                        }
                        IconButton(onClick = {
                            app.settingsViewModel.setTheme(when (theme) {
                                com.filemind.app.theme.ThemeMode.System ->
                                    com.filemind.app.theme.ThemeMode.Light
                                com.filemind.app.theme.ThemeMode.Light ->
                                    com.filemind.app.theme.ThemeMode.Dark
                                com.filemind.app.theme.ThemeMode.Dark ->
                                    com.filemind.app.theme.ThemeMode.System
                            })
                        }) {
                            Icon(when (theme) {
                                com.filemind.app.theme.ThemeMode.System ->
                                    Icons.Filled.BrightnessAuto
                                com.filemind.app.theme.ThemeMode.Light ->
                                    Icons.Filled.LightMode
                                com.filemind.app.theme.ThemeMode.Dark ->
                                    Icons.Filled.DarkMode
                            }, contentDescription = "Change theme")
                        }
                    }
                )
            }
        ) { padding ->
            NavHost(
                navController = nav,
                startDestination = Routes.Home,
                modifier = Modifier.fillMaxSize().padding(padding)
            ) {
                composable(Routes.Home) { HomeScreen(app, nav) }
                composable(Routes.Files) { FilesScreen(app, nav) }
                composable(Routes.Search) { SearchScreen(app) }
                composable(Routes.Tools) { ToolsScreen(app, nav) }
                composable(Routes.Activity) { ActivityScreen(app, nav) }
                composable(Routes.Settings) { SettingsScreen(app, nav) }
                composable(Routes.Organizer) { OrganizerScreen(app, nav) }
                composable(Routes.Help) { HelpScreens.Hub(nav) }
                composable("devices") { DevicesScreen(app) }
                composable(Routes.HelpAbout) { HelpScreens.About(nav) }
                composable(Routes.HelpGettingStarted) { HelpScreens.GettingStarted(nav) }
                composable(Routes.HelpGuide) { HelpScreens.Guide(nav) }
                composable(Routes.HelpWhatsNew) { HelpScreens.WhatsNew() }
                composable(Routes.HelpTroubleshooting) { HelpScreens.Troubleshooting(nav) }
                composable(Routes.HelpDiagnostics) { HelpScreens.Diagnostics(app) }
                composable(Routes.HelpFeedback) { HelpScreens.Feedback() }
                composable(Routes.HelpCredits) { HelpScreens.Credits() }
                composable(Routes.HelpLicenses) { HelpScreens.Licenses() }
            }
        }
    }
    }
}

package com.filemind.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import com.filemind.app.nav.AppShell
import com.filemind.app.theme.FileMindTheme
import com.filemind.app.theme.ThemeMode
import com.filemind.app.vm.SettingsViewModel

class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        val app = application as FileMindApp
        setContent {
            val settings = app.settingsViewModel
            val theme by settings.themeMode.collectAsState()
            FileMindTheme(mode = theme) {
                AppShell(app = app)
            }
        }
    }
}

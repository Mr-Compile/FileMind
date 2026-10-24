package com.filemind.app

import android.app.Application
import com.filemind.app.vm.SettingsViewModel
import com.filemind.platform.FileMindAndroid

/** Process entry point. Service graph stays manual — FileMindAndroid.from(). */
class FileMindApp : Application() {

    val backend: FileMindAndroid by lazy { FileMindAndroid.from(this) }
    val settingsViewModel: SettingsViewModel by lazy { SettingsViewModel(this) }

    override fun onCreate() {
        super.onCreate()
        backend.notifications.ensureChannels()
    }
}

package com.filemind.app

import android.app.Application
import com.filemind.app.vm.OrganizerViewModel
import com.filemind.app.vm.SettingsViewModel
import com.filemind.platform.FileMindAndroid

/** Process entry point. Service graph stays manual — FileMindAndroid.from(). */
class FileMindApp : Application() {

    val backend: FileMindAndroid by lazy { FileMindAndroid.from(this) }
    val settingsViewModel: SettingsViewModel by lazy { SettingsViewModel(this) }
    // App-scoped so an organizer session survives navigation — leaving and
    // re-entering the organizer returns to the exact step (PLAN-028).
    val organizerViewModel: OrganizerViewModel by lazy { OrganizerViewModel(this) }

    override fun onCreate() {
        super.onCreate()
        backend.notifications.ensureChannels()
    }
}

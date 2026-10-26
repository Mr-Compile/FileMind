package com.filemind.app

import android.app.Application
import com.filemind.app.vm.OrganizerViewModel
import com.filemind.app.vm.SettingsViewModel
import com.filemind.platform.FileMindAndroid
import kotlinx.coroutines.launch

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
        // Reconcile interrupted runs: ops left in Planned/Executing/... by a
        // dead process are not "running" — park them as NeedsAttention so the
        // Activity screen shows them for review/undo instead.
        kotlinx.coroutines.CoroutineScope(
            kotlinx.coroutines.SupervisorJob() + kotlinx.coroutines.Dispatchers.IO
        ).launch {
            val live = setOf(
                com.filemind.core.contracts.OperationStatus.Planned,
                com.filemind.core.contracts.OperationStatus.Validating,
                com.filemind.core.contracts.OperationStatus.Executing,
                com.filemind.core.contracts.OperationStatus.Verifying,
                com.filemind.core.contracts.OperationStatus.RollingBack)
            backend.operationRepository.getIncomplete().value
                ?.filter { it.status in live }
                ?.forEach { op ->
                    backend.operationRepository.save(
                        op.copy(status = com.filemind.core.contracts.OperationStatus.NeedsAttention))
                }
        }
    }
}

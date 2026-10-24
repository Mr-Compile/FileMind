package com.filemind.platform.share

import android.app.Activity
import android.os.Bundle
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

/**
 * Headless share-target entry point. Hosts no UI: it routes the incoming
 * share into Smart Inbox items and finishes immediately. Android UI is out
 * of scope for PLAN-010 — this exists only because share-targets must be
 * activities.
 */
class ShareIntakeActivity : Activity() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val incoming = intent
        scope.launch {
            ShareIntake(applicationContext).route(incoming)
            withContext(Dispatchers.Main) { finish() }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        scope.cancel()
    }
}

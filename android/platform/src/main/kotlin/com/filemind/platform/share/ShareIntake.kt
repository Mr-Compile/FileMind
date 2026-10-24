package com.filemind.platform.share

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import com.filemind.core.app.Notification
import com.filemind.core.app.SmartInboxItem
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import com.filemind.platform.FileMindAndroid
import java.time.Instant
import java.util.UUID

/**
 * Routes ACTION_SEND / ACTION_SEND_MULTIPLE into FileMind. Shared content
 * URIs arrive with transient read permission (FLAG_GRANT_READ_URI_PERMISSION)
 * — we persist it when the system allows, and always drop items into the
 * Smart Inbox so the scan pipeline can pick them up later.
 */
object ShareRouting {
    fun isShareAction(action: String?): Boolean =
        action == Intent.ACTION_SEND || action == Intent.ACTION_SEND_MULTIPLE
}

class ShareIntake(private val context: Context) {

    /**
     * Extract shared URIs from [intent], persist read grants where possible,
     * and enqueue each item into the Smart Inbox. Returns the URIs routed.
     */
    suspend fun route(intent: Intent): Result<List<String>> = runCatching {
        if (!ShareRouting.isShareAction(intent.action)) {
            return@runCatching Result.fail<List<String>>(FileMindError.validation(
                "Not a share intent: ${intent.action}"))
        }
        val uris = extractUris(intent)
        val services = FileMindAndroid.from(context)

        for (uri in uris) {
            // Transient share grants can't always be persisted — best effort.
            if (intent.flags and Intent.FLAG_GRANT_READ_URI_PERMISSION != 0) {
                runCatching {
                    context.contentResolver.takePersistableUriPermission(
                        uri, Intent.FLAG_GRANT_READ_URI_PERMISSION)
                }
            }
            services.smartInbox.add(SmartInboxItem(
                id = UUID.randomUUID().toString().replace("-", ""),
                path = uri.toString(),
                reason = "Shared into FileMind (${intent.action})",
                classification = ClassificationResult.unknown("share intake"),
                at = Instant.now(),
                resolved = false))
        }

        if (uris.isNotEmpty()) {
            services.notifications.notify(Notification(
                title = "FileMind inbox",
                body = "${uris.size} shared file(s) added to Smart Inbox."))
        }
        Result.ok(uris.map { it.toString() })
    }.getOrElse {
        Result.fail(FileMindError.io("share intake failed: ${it.message}"))
    }

    private fun extractUris(intent: Intent): List<Uri> {
        val out = mutableListOf<Uri>()
        if (Build.VERSION.SDK_INT >= 33) {
            intent.getParcelableExtra(Intent.EXTRA_STREAM, Uri::class.java)?.let { out.add(it) }
            intent.getParcelableArrayListExtra(Intent.EXTRA_STREAM, Uri::class.java)
                ?.let { out.addAll(it) }
        } else {
            @Suppress("DEPRECATION")
            (intent.getParcelableExtra<Uri>(Intent.EXTRA_STREAM))?.let { out.add(it) }
            @Suppress("DEPRECATION")
            (intent.getParcelableArrayListExtra<Uri>(Intent.EXTRA_STREAM))?.let { out.addAll(it) }
        }
        // Text shares with a content:// URL in EXTRA_TEXT.
        intent.getStringExtra(Intent.EXTRA_TEXT)?.let { text ->
            text.split(Regex("\\s+"))
                .firstOrNull { it.startsWith("content://") || it.startsWith("file://") }
                ?.let { out.add(Uri.parse(it)) }
        }
        return out.distinct()
    }
}

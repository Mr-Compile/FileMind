package com.filemind.platform.notify

import android.app.NotificationChannel
import android.app.NotificationManager
import android.content.Context
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import com.filemind.core.app.Notification
import com.filemind.core.app.NotificationService
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import java.util.concurrent.atomic.AtomicInteger

/**
 * NotificationService port over NotificationManagerCompat. Channel ids and
 * routing come from NotificationContracts so the contract stays testable.
 */
class AndroidNotificationService(private val context: Context) : NotificationService {

    private val ids = AtomicInteger(1000)

    fun ensureChannels() {
        val nm = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        for (spec in NotificationContracts.channels) {
            val importance = when (spec.importance) {
                NotificationContracts.Importance.Low -> NotificationManager.IMPORTANCE_LOW
                NotificationContracts.Importance.Default -> NotificationManager.IMPORTANCE_DEFAULT
                NotificationContracts.Importance.High -> NotificationManager.IMPORTANCE_HIGH
            }
            nm.createNotificationChannel(
                NotificationChannel(spec.id, spec.name, importance).apply {
                    description = spec.description
                })
        }
    }

    override suspend fun notify(notification: Notification): Result<Boolean> = runCatching {
        ensureChannels()
        val channelId = when {
            notification.operationId != null -> NotificationContracts.CHANNEL_OPERATIONS
            else -> NotificationContracts.CHANNEL_REVIEW
        }
        val n = NotificationCompat.Builder(context, channelId)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle(notification.title)
            .setContentText(notification.body)
            .setAutoCancel(true)
            .build()
        NotificationManagerCompat.from(context).notify(
            notification.operationId?.hashCode() ?: ids.incrementAndGet(), n)
        Result.ok(true)
    }.getOrElse {
        Result.fail(FileMindError.io("notify failed: ${it.message}"))
    }

    /** Progress notification for a running worker (ongoing, operations channel). */
    fun progress(title: String, body: String, workId: String): android.app.Notification {
        ensureChannels()
        return NotificationCompat.Builder(context, NotificationContracts.CHANNEL_OPERATIONS)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle(title)
            .setContentText(body)
            .setOngoing(true)
            .setProgress(0, 0, true)
            .build()
            .also { }
    }
}

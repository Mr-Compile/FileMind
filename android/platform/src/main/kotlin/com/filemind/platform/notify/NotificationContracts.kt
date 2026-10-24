package com.filemind.platform.notify

/**
 * Notification channel contract — the M3 deliverable. Kept free of Android
 * types so unit tests can assert the contract on the JVM.
 */
object NotificationContracts {

    /** Long-running scan/organize progress; low importance, no sound. */
    const val CHANNEL_OPERATIONS = "filemind.operations"

    /** Smart Inbox review prompts ("n files need your decision"). */
    const val CHANNEL_REVIEW = "filemind.review"

    /** Failures and needs-attention outcomes. */
    const val CHANNEL_ALERTS = "filemind.alerts"

    data class ChannelSpec(
        val id: String,
        val name: String,
        val importance: Importance,
        val description: String,
        /** Foreground-service style persistent notification vs one-shot. */
        val ongoing: Boolean
    )

    enum class Importance { Low, Default, High }

    val channels = listOf(
        ChannelSpec(CHANNEL_OPERATIONS, "File operations", Importance.Low,
            "Progress and completion of scans and organize jobs.", ongoing = true),
        ChannelSpec(CHANNEL_REVIEW, "Review queue", Importance.Default,
            "Files FileMind could not organize confidently.", ongoing = false),
        ChannelSpec(CHANNEL_ALERTS, "Operation problems", Importance.High,
            "Blocked plans, failed operations, and items needing attention.", ongoing = false)
    )

    fun channelFor(kind: Kind): ChannelSpec = when (kind) {
        Kind.Progress -> channels.first { it.id == CHANNEL_OPERATIONS }
        Kind.Review -> channels.first { it.id == CHANNEL_REVIEW }
        Kind.Alert -> channels.first { it.id == CHANNEL_ALERTS }
    }

    enum class Kind { Progress, Review, Alert }
}

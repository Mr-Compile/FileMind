package com.filemind.app.ui

import android.net.Uri
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

fun formatBytes(bytes: Long): String = when {
    bytes >= 1L shl 30 -> "%.1f GB".format(bytes / (1L shl 30).toDouble())
    bytes >= 1L shl 20 -> "%.1f MB".format(bytes / (1L shl 20).toDouble())
    bytes >= 1L shl 10 -> "%.1f KB".format(bytes / (1L shl 10).toDouble())
    else -> "$bytes B"
}

private val timeFmt = DateTimeFormatter.ofPattern("MMM d, HH:mm")
    .withZone(ZoneId.systemDefault())

fun formatInstant(at: Instant): String = timeFmt.format(at)

/** Friendly short label for a URI source (SAF docId tail or file path). */
fun shortPath(uri: String, max: Int = 42): String {
    val decoded = Uri.decode(uri.substringAfterLast('/').ifBlank { uri })
    val tail = decoded.substringAfterLast(':').ifBlank { decoded }
    return if (tail.length <= max) tail else "…" + tail.takeLast(max - 1)
}

package com.filemind.app.theme

import androidx.compose.ui.graphics.Color

/**
 * Canonical brand tokens — provenance: branding/colors.json (schemaVersion 1).
 * Verbatim values; parity with the JSON is enforced by the C# parity suite on
 * the Windows side and by review here.
 */
object BrandColors {
    val Primary = Color(0xFF2563EB)
    val Secondary = Color(0xFF4F46E5)
    val Accent = Color(0xFF7C3AED)
    val Gradient = listOf(Primary, Secondary, Accent)

    object Light {
        val Background = Color(0xFFF8FAFC)
        val Surface = Color(0xFFFFFFFF)
        val SurfaceElevated = Color(0xFFF1F5F9)
        val TextPrimary = Color(0xFF0F172A)
        val TextSecondary = Color(0xFF64748B)
        val Border = Color(0xFFE2E8F0)
    }

    object Dark {
        val Background = Color(0xFF0B1120)
        val Surface = Color(0xFF111827)
        val SurfaceElevated = Color(0xFF1E293B)
        val TextPrimary = Color(0xFFF8FAFC)
        val TextSecondary = Color(0xFF94A3B8)
        val Border = Color(0xFF334155)
    }

    object Semantic {
        val Success = Color(0xFF16A34A)
        val Info = Color(0xFF0284C7)
        val Warning = Color(0xFFD97706)
        val Error = Color(0xFFDC2626)
        val Destructive = Color(0xFFB91C1C)
    }
}

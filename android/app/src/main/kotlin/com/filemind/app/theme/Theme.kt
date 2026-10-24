package com.filemind.app.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.ColorScheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

/** Semantic tokens the M3 ColorScheme has no slot for (spec: foundations/02). */
@Immutable
data class FileMindSemantic(
    val surfaceElevated: Color,
    val textDisabled: Color,
    val statusInfo: Color,
    val statusSuccess: Color,
    val statusWarning: Color,
    val statusError: Color,
    val statusDestructive: Color,
    val statusOffline: Color,
    val statusNeutral: Color
)

private val LightSemantic = FileMindSemantic(
    surfaceElevated = BrandColors.Light.SurfaceElevated,
    textDisabled = BrandColors.Light.TextSecondary.copy(alpha = 0.55f),
    statusInfo = BrandColors.Semantic.Info,
    statusSuccess = BrandColors.Semantic.Success,
    statusWarning = BrandColors.Semantic.Warning,
    statusError = BrandColors.Semantic.Error,
    statusDestructive = BrandColors.Semantic.Destructive,
    statusOffline = Color(0xFF64748B),
    statusNeutral = Color(0xFF64748B)
)

private val DarkSemantic = FileMindSemantic(
    surfaceElevated = BrandColors.Dark.SurfaceElevated,
    textDisabled = BrandColors.Dark.TextSecondary.copy(alpha = 0.55f),
    statusInfo = Color(0xFF38BDF8),
    statusSuccess = Color(0xFF4ADE80),
    statusWarning = Color(0xFFFBBF24),
    statusError = Color(0xFFF87171),
    statusDestructive = Color(0xFFEF4444),
    statusOffline = Color(0xFF94A3B8),
    statusNeutral = Color(0xFF94A3B8)
)

val LocalFileMindSemantic = staticCompositionLocalOf { LightSemantic }

private val LightScheme = lightColorScheme(
    primary = BrandColors.Primary,
    secondary = BrandColors.Secondary,
    tertiary = BrandColors.Accent,
    error = BrandColors.Semantic.Error,
    background = BrandColors.Light.Background,
    surface = BrandColors.Light.Surface,
    surfaceVariant = BrandColors.Light.SurfaceElevated,
    onBackground = BrandColors.Light.TextPrimary,
    onSurface = BrandColors.Light.TextPrimary,
    onSurfaceVariant = BrandColors.Light.TextSecondary,
    outline = BrandColors.Light.Border
)

private val DarkScheme = darkColorScheme(
    primary = Color(0xFF60A5FA),
    secondary = Color(0xFF818CF8),
    tertiary = Color(0xFFA78BFA),
    error = Color(0xFFF87171),
    background = BrandColors.Dark.Background,
    surface = BrandColors.Dark.Surface,
    surfaceVariant = BrandColors.Dark.SurfaceElevated,
    onBackground = BrandColors.Dark.TextPrimary,
    onSurface = BrandColors.Dark.TextPrimary,
    onSurfaceVariant = BrandColors.Dark.TextSecondary,
    outline = BrandColors.Dark.Border
)

enum class ThemeMode { System, Light, Dark }

@Composable
fun FileMindTheme(mode: ThemeMode = ThemeMode.System, content: @Composable () -> Unit) {
    val dark = when (mode) {
        ThemeMode.System -> isSystemInDarkTheme()
        ThemeMode.Light -> false
        ThemeMode.Dark -> true
    }
    CompositionLocalProvider(
        LocalFileMindSemantic provides if (dark) DarkSemantic else LightSemantic
    ) {
        MaterialTheme(
            colorScheme = if (dark) DarkScheme else LightScheme,
            typography = FileMindTypography,
            content = content
        )
    }
}

val ColorScheme.semantic: FileMindSemantic
    @Composable get() = LocalFileMindSemantic.current

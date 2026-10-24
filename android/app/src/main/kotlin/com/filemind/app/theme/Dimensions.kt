package com.filemind.app.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Spec: foundations/02-design-tokens.md — spacing 4..48, radius 6/10/14/18. */
object Spacing {
    val s1 = 4.dp; val s2 = 8.dp; val s3 = 12.dp; val s4 = 16.dp
    val s5 = 20.dp; val s6 = 24.dp; val s8 = 32.dp; val s10 = 40.dp; val s12 = 48.dp
}

object Radius {
    val small = 6.dp; val medium = 10.dp; val large = 14.dp; val hero = 18.dp
}

object Elevation {
    val page = 0.dp; val card = 1.dp; val menu = 2.dp; val dialog = 3.dp
}

/** M3 default scale — Roboto is the platform family (branding pack). */
val FileMindTypography = Typography(
    headlineLarge = androidx.compose.ui.text.TextStyle(fontSize = 28.sp),
    headlineMedium = androidx.compose.ui.text.TextStyle(fontSize = 24.sp),
    titleLarge = androidx.compose.ui.text.TextStyle(fontSize = 20.sp),
    titleMedium = androidx.compose.ui.text.TextStyle(fontSize = 16.sp),
    bodyLarge = androidx.compose.ui.text.TextStyle(fontSize = 16.sp),
    bodyMedium = androidx.compose.ui.text.TextStyle(fontSize = 14.sp),
    labelLarge = androidx.compose.ui.text.TextStyle(fontSize = 14.sp),
    labelMedium = androidx.compose.ui.text.TextStyle(fontSize = 12.sp)
)

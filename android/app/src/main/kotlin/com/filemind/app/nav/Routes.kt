package com.filemind.app.nav

/** Canonical shared route IDs (crossplatform-ux SHARED_UX_SYSTEM IA). */
object Routes {
    const val Home = "home"
    const val Files = "files"
    const val Search = "search"
    const val Tools = "tools"
    const val Activity = "activity"
    const val Settings = "settings"
    const val Organizer = "organizer"
    const val Help = "help"

    const val HelpAbout = "help/about"
    const val HelpGettingStarted = "help/getting-started"
    const val HelpGuide = "help/guide"
    const val HelpWhatsNew = "help/whats-new"
    const val HelpTroubleshooting = "help/troubleshooting"
    const val HelpDiagnostics = "help/diagnostics"
    const val HelpFeedback = "help/feedback"
    const val HelpCredits = "help/credits"
    const val HelpLicenses = "help/licenses"

    /** Primary destinations shown in the adaptive nav suite. */
    val primary = listOf(Home, Files, Search, Activity, Settings)
}

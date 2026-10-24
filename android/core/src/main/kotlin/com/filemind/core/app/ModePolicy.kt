package com.filemind.core.app

import com.filemind.core.contracts.DecisionKind
import com.filemind.core.contracts.OrganizationMode

enum class ItemRoute {
    Execute, SmartInbox, PreviewOnly, Skip
}

class ModePolicy(val mode: OrganizationMode) {
    companion object {
        const val SettingsKey = "organization.mode"

        fun tryParse(value: String): OrganizationMode? =
            OrganizationMode.entries.firstOrNull {
                it.name.equals(value.replace("-", ""), ignoreCase = true)
            }

        fun display(mode: OrganizationMode): String = when (mode) {
            OrganizationMode.AiAssisted -> "ai-assisted"
            OrganizationMode.LocalOnly -> "local-only"
            else -> mode.name.lowercase()
        }
    }

    fun routeFor(analyzed: AnalyzedFile, apply: Boolean = false): ItemRoute {
        if (analyzed.decision.kind == DecisionKind.NoOp || analyzed.decision.kind == DecisionKind.Ignore) {
            return ItemRoute.Skip
        }

        return when {
            mode == OrganizationMode.Manual && !apply -> ItemRoute.PreviewOnly
            mode == OrganizationMode.Automatic -> ItemRoute.Execute
            analyzed.decision.kind == DecisionKind.Review || analyzed.classification.needsReview ->
                ItemRoute.SmartInbox
            else -> ItemRoute.Execute
        }
    }

    val executionRequiresApply: Boolean get() = mode == OrganizationMode.Manual
}

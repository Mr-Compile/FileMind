package com.filemind.core.rules

import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.Rule
import com.filemind.core.contracts.RuleAction
import com.filemind.core.contracts.RuleActionKind

data class RuleEvaluation(
    val matchedRuleIds: List<String>,
    val proposedActions: List<RuleAction>,
    val conflicts: List<String>,
    val trace: List<String>,
    val needsReview: Boolean = false,
    val winningRuleId: String? = null
)

class RuleEngine {
    fun evaluate(
        candidate: FileCandidate,
        classification: ClassificationResult,
        rules: List<Rule>
    ): RuleEvaluation {
        val trace = mutableListOf<String>()
        val ordered = rules
            .filter { it.enabled }
            .sortedWith(
                compareByDescending<Rule> { it.priority }
                    .thenByDescending { it.conditionCount }
                    .thenBy { it.id }
            )

        val matched = mutableListOf<Rule>()
        for (rule in ordered) {
            val hit = ConditionEvaluator.evaluate(rule.conditionRoot, candidate, classification)
            trace.add(
                if (hit) "MATCH ${rule.id} '${rule.name}' (priority=${rule.priority}, conditions=${rule.conditionCount})"
                else "SKIP  ${rule.id} '${rule.name}'"
            )
            if (!hit) continue
            matched.add(rule)
            if (rule.stopProcessing) {
                trace.add("STOP  ${rule.id} stop-processing=true")
                break
            }
        }

        val actions = mutableListOf<RuleAction>()
        val conflicts = mutableListOf<String>()
        val destinations = mutableListOf<Pair<String, String>>()

        for (rule in matched) {
            for (action in rule.actions) {
                if ((action.kind == RuleActionKind.Move || action.kind == RuleActionKind.RouteToCollection) &&
                    action.destinationTemplate != null
                ) {
                    val existing = destinations.firstOrNull { it.second != action.destinationTemplate }
                    if (existing != null) {
                        conflicts.add(
                            "Rules '${existing.first}' and '${rule.id}' propose different destinations " +
                                "('${existing.second}' vs '${action.destinationTemplate}')."
                        )
                        continue
                    }
                    destinations.add(rule.id to action.destinationTemplate)
                }
                actions.add(action)
            }
        }

        val needsReview = conflicts.isNotEmpty()
        if (needsReview) trace.add("REVIEW conflicts detected; no silent winner chosen.")

        return RuleEvaluation(
            matchedRuleIds = matched.map { it.id },
            proposedActions = actions,
            conflicts = conflicts,
            trace = trace,
            needsReview = needsReview,
            winningRuleId = matched.firstOrNull()?.id
        )
    }
}

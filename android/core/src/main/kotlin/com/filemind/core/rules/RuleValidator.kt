package com.filemind.core.rules

import com.filemind.core.contracts.ConditionGroup
import com.filemind.core.contracts.ConditionField
import com.filemind.core.contracts.ConditionOperator
import com.filemind.core.contracts.Rule
import com.filemind.core.contracts.RuleActionKind
import com.filemind.core.contracts.RuleLimits
import com.filemind.core.errors.ErrorCode
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result

object RuleValidator {
    const val CurrentSchemaVersion = 1

    fun validate(rule: Rule): Result<Rule> {
        val errors = mutableListOf<String>()

        if (rule.id.isBlank()) errors.add("Rule.Id is required.")
        if (rule.name.isBlank()) errors.add("Rule.Name is required.")
        if (rule.schemaVersion > CurrentSchemaVersion) {
            return Result.fail(FileMindError(
                ErrorCode.UnsupportedOperation,
                "Rule schema version ${rule.schemaVersion} is newer than supported $CurrentSchemaVersion.",
                retrySafe = false, requiresReview = true))
        }
        if (rule.schemaVersion < 1) errors.add("Rule.SchemaVersion must be >= 1.")
        if (rule.conditionRoot.depth > RuleLimits.MaxConditionNestingDepth) {
            errors.add("Condition nesting depth ${rule.conditionRoot.depth} exceeds max ${RuleLimits.MaxConditionNestingDepth}.")
        }
        if (rule.conditionRoot.conditions.isEmpty() && rule.conditionRoot.groups.isEmpty()) {
            errors.add("Rule must have at least one condition.")
        }
        if (rule.actions.isEmpty()) errors.add("Rule must have at least one action.")

        for (action in rule.actions) {
            if ((action.kind == RuleActionKind.Move || action.kind == RuleActionKind.Copy ||
                    action.kind == RuleActionKind.CreateFolder) &&
                action.destinationTemplate.isNullOrBlank()
            ) {
                errors.add("Action ${action.kind} requires DestinationTemplate.")
            }
            if (action.kind == RuleActionKind.Rename && action.namingTemplate.isNullOrBlank()) {
                errors.add("Rename action requires NamingTemplate.")
            }
            if (action.kind == RuleActionKind.RouteToCollection && action.collection.isNullOrBlank()) {
                errors.add("RouteToCollection action requires Collection.")
            }
        }

        validateGroup(rule.conditionRoot)?.let { errors.add(it) }

        return if (errors.isEmpty()) {
            Result.ok(rule)
        } else {
            Result.fail(FileMindError.validation(errors.joinToString(" ")))
        }
    }

    private fun validateGroup(group: ConditionGroup): String? {
        for (c in group.conditions) {
            if (c.value.isBlank() && c.operator != ConditionOperator.NotContains) {
                return "Condition on ${c.field} requires a value."
            }
            if (c.field == ConditionField.Metadata && c.metadataKey.isNullOrBlank()) {
                return "Metadata condition requires MetadataKey."
            }
        }
        for (g in group.groups) {
            validateGroup(g)?.let { return it }
        }
        return null
    }
}

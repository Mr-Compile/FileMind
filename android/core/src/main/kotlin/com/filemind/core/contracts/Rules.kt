@file:UseSerializers(InstantSerializer::class)

package com.filemind.core.contracts

import java.time.Instant
import kotlinx.serialization.Serializable
import kotlinx.serialization.UseSerializers

object RuleLimits {
    const val MaxConditionNestingDepth = 3
}

enum class ConditionField {
    Filename, Extension, MimeType, Keyword, SourceFolder, SourceScope, Size,
    CreatedAt, ModifiedAt, Metadata, ExtractedContent, Subject, Project, Collection, Category
}

enum class ConditionOperator {
    Equals, NotEquals, Contains, NotContains, StartsWith, EndsWith, Regex,
    GreaterThan, LessThan, In
}

enum class LogicalOperator {
    And, Or
}

@Serializable
data class ConditionGroup(
    val operator: LogicalOperator,
    val conditions: List<RuleCondition> = emptyList(),
    val groups: List<ConditionGroup> = emptyList()
) {
    val depth: Int
        get() = 1 + (if (groups.isEmpty()) 0 else groups.maxOf { it.depth })
}

@Serializable
data class RuleCondition(
    val field: ConditionField,
    val operator: ConditionOperator,
    val value: String,
    val metadataKey: String? = null
) {
    val specificity: Int
        get() = when (this.field) {
            ConditionField.Subject, ConditionField.Project -> 4
            ConditionField.Filename -> 3
            ConditionField.Keyword, ConditionField.Metadata, ConditionField.ExtractedContent -> 2
            else -> 1
        }
}

enum class RuleActionKind {
    Move, Copy, Rename, Tag, Ignore, Notify, Ask, CreateFolder, RouteToCollection
}

@Serializable
data class RuleAction(
    val kind: RuleActionKind,
    val destinationTemplate: String? = null,
    val namingTemplate: String? = null,
    val collection: String? = null,
    val tag: String? = null,
    val conflictPolicy: ConflictPolicy = ConflictPolicy.KeepBoth
)

enum class RuleProvenance {
    Builtin, User, Imported, AiSuggested, Learned
}

@Serializable
data class Rule(
    val id: String,
    val name: String,
    val enabled: Boolean = true,
    val priority: Int = 0,
    val conditionRoot: ConditionGroup,
    val actions: List<RuleAction>,
    val stopProcessing: Boolean = false,
    val mode: String = "all",
    val createdAt: Instant? = null,
    val updatedAt: Instant? = null,
    val schemaVersion: Int,
    val provenance: RuleProvenance = RuleProvenance.User
) {
    val conditionCount: Int
        get() = countConditions(conditionRoot)

    private fun countConditions(g: ConditionGroup): Int =
        g.conditions.size + g.groups.sumOf { countConditions(it) }
}

@Serializable
data class Decision(
    val kind: DecisionKind,
    val candidate: FileCandidate,
    val classification: ClassificationResult,
    val destinationTemplate: String? = null,
    val namingTemplate: String? = null,
    val conflictPolicy: ConflictPolicy = ConflictPolicy.KeepBoth,
    val reasons: List<String> = emptyList()
)

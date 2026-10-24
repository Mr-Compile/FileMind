package com.filemind.core.rules

import com.filemind.core.classification.Normalizer
import com.filemind.core.classification.NormalizedTokens
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.ConditionField
import com.filemind.core.contracts.ConditionGroup
import com.filemind.core.contracts.ConditionOperator
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.LogicalOperator
import com.filemind.core.contracts.RuleCondition
import com.filemind.core.contracts.utcTicks

object ConditionEvaluator {
    fun evaluate(group: ConditionGroup, candidate: FileCandidate, classification: ClassificationResult): Boolean =
        if (group.operator == LogicalOperator.And) {
            group.conditions.all { evaluateCondition(it, candidate, classification) } &&
                group.groups.all { evaluate(it, candidate, classification) }
        } else {
            group.conditions.any { evaluateCondition(it, candidate, classification) } ||
                group.groups.any { evaluate(it, candidate, classification) }
        }

    private fun evaluateCondition(c: RuleCondition, candidate: FileCandidate, classification: ClassificationResult): Boolean {
        val normalizedValue = Normalizer.normalize(c.value)
        val compare = comparison(c.field, candidate, classification, c)

        return when (c.operator) {
            ConditionOperator.Equals -> compare != null && Normalizer.equivalent(compare, c.value)
            ConditionOperator.NotEquals -> compare == null || !Normalizer.equivalent(compare, c.value)
            ConditionOperator.Contains -> contains(candidate, c.field, normalizedValue, classification)
            ConditionOperator.NotContains -> !contains(candidate, c.field, normalizedValue, classification)
            ConditionOperator.StartsWith -> compare != null &&
                Normalizer.normalize(compare).joined.startsWith(normalizedValue.joined)
            ConditionOperator.EndsWith -> compare != null &&
                Normalizer.normalize(compare).joined.endsWith(normalizedValue.joined)
            // JVM regex has no built-in timeout (C# uses a 2s guard); evaluation
            // semantics match for all non-pathological patterns.
            ConditionOperator.Regex -> compare != null &&
                Regex(c.value, RegexOption.IGNORE_CASE).containsMatchIn(compare)
            ConditionOperator.GreaterThan -> compare != null && compareNumeric(candidate, c, classification) > 0
            ConditionOperator.LessThan -> compare != null && compareNumeric(candidate, c, classification) < 0
            ConditionOperator.In -> compare != null &&
                c.value.split(',').map { it.trim() }.any { Normalizer.equivalent(compare, it) }
        }
    }

    private fun comparison(field: ConditionField, f: FileCandidate, classification: ClassificationResult, c: RuleCondition): String? =
        when (field) {
            ConditionField.Filename -> f.displayName
            ConditionField.Extension -> f.extension
            ConditionField.MimeType -> f.mimeType
            ConditionField.SourceFolder -> f.sourceUri
            ConditionField.SourceScope -> f.sourceScope.name
            ConditionField.Size -> f.sizeBytes.toString()
            ConditionField.Subject -> classification.subject
            ConditionField.Project -> classification.project
            ConditionField.Collection -> classification.collection
            ConditionField.Category -> classification.category
            ConditionField.Metadata -> c.metadataKey?.let { f.metadata[it] }
            else -> null
        }

    private fun contains(f: FileCandidate, field: ConditionField, needle: NormalizedTokens, classification: ClassificationResult): Boolean {
        val hay: String? = when (field) {
            ConditionField.Filename -> Normalizer.normalize(f.displayName).joined
            ConditionField.Keyword -> Normalizer.normalize(f.displayName).joined
            ConditionField.Extension -> Normalizer.normalize(f.extension).joined
            ConditionField.MimeType -> Normalizer.normalize(f.mimeType ?: "").joined
            ConditionField.SourceFolder -> Normalizer.normalize(f.sourceUri).joined
            ConditionField.ExtractedContent -> Normalizer.normalize(f.extractedTextRef ?: "").joined
            ConditionField.Metadata -> null
            ConditionField.Subject -> classification.subject
            ConditionField.Project -> classification.project
            ConditionField.Collection -> classification.collection
            ConditionField.Category -> classification.category
            else -> null
        }

        if (field == ConditionField.Metadata) return false
        if (needle.joined.isEmpty()) return false
        if (field == ConditionField.Keyword) {
            val compact = Normalizer.normalize(f.displayName).tokens.joinToString("")
            return compact.contains(needle.joined.replace(" ", "")) ||
                hay!!.contains(needle.joined)
        }
        return hay != null && hay.contains(needle.joined)
    }

    private fun compareNumeric(f: FileCandidate, c: RuleCondition, classification: ClassificationResult): Int {
        val actual: Double = when (c.field) {
            ConditionField.Size -> f.sizeBytes.toDouble()
            ConditionField.CreatedAt -> f.createdAt.utcTicks().toDouble()
            ConditionField.ModifiedAt -> f.modifiedAt.utcTicks().toDouble()
            else -> Double.NaN
        }
        if (actual.isNaN()) return 0
        val expected = c.value.toDoubleOrNull() ?: return 0
        return actual.compareTo(expected)
    }
}

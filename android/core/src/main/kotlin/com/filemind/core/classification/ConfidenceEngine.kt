package com.filemind.core.classification

import com.filemind.core.contracts.ConfidenceBand
import com.filemind.core.contracts.Evidence

data class ConfidencePolicy(
    val highThreshold: Double = 95.0,
    val mediumThreshold: Double = 70.0,
    val maxScore: Double = 100.0,
    val kindCaps: Map<String, Double> = mapOf(
        "subject-keyword" to 15.0,
        "project-keyword" to 12.0,
        "extension-signal" to 10.0,
        "mime-signal" to 8.0,
        "content-signal" to 25.0,
        "ai-signal" to 30.0
    )
) {
    companion object {
        val Default = ConfidencePolicy()
    }
}

class ConfidenceEngine(private val policy: ConfidencePolicy = ConfidencePolicy.Default) {
    fun score(evidence: List<Evidence>): Double {
        val capped = evidence
            .groupBy { it.kind }
            .entries
            .sumOf { (kind, group) ->
                val sum = group.sumOf { it.scoreContribution }
                policy.kindCaps[kind]?.let { minOf(sum, it) } ?: sum
            }
        return capped.coerceIn(0.0, policy.maxScore)
    }

    fun band(score: Double): ConfidenceBand =
        if (score >= policy.highThreshold) ConfidenceBand.High
        else if (score >= policy.mediumThreshold) ConfidenceBand.Medium
        else ConfidenceBand.Low
}

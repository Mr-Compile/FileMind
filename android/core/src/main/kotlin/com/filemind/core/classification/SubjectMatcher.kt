package com.filemind.core.classification

import com.filemind.core.contracts.ClassifierSource
import com.filemind.core.contracts.Evidence

data class SubjectMatchResult(
    val subjectId: String?,
    val score: Double,
    val evidence: List<Evidence>,
    val warnings: List<String>
)

class SubjectMatcher(private val catalog: SubjectCatalog) {
    companion object {
        const val CodeMatchScore = 65.0
        const val NameAliasMatchScore = 45.0
        const val KeywordMatchScore = 15.0
        const val AmbiguityPenalty = 10.0
    }

    fun match(candidate: NormalizedTokens): SubjectMatchResult {
        data class Hit(val subject: SubjectEntry, val score: Double, val kind: String)
        val matches = mutableListOf<Hit>()

        for (subject in catalog.subjects) {
            if (subject.code != null &&
                containsCode(candidate, Normalizer.normalize(subject.code).joined)
            ) {
                matches.add(Hit(subject, CodeMatchScore, "subject-code"))
                continue
            }

            if (equivalentToAny(candidate, subject.name, subject.aliases)) {
                matches.add(Hit(subject, NameAliasMatchScore, "subject-alias"))
                continue
            }

            if (containsKeyword(candidate, subject.keywords)) {
                matches.add(Hit(subject, KeywordMatchScore, "subject-keyword"))
            }
        }

        val evidence = mutableListOf<Evidence>()
        val warnings = mutableListOf<String>()
        var subjectId: String? = null
        var score = 0.0

        if (matches.isNotEmpty()) {
            val best = matches.sortedWith(compareByDescending<Hit> { it.score }.thenBy { it.subject.id }).first()
            subjectId = best.subject.id
            score = best.score
            evidence.add(Evidence(best.kind, "Matched '${best.subject.name}'", best.score, ClassifierSource.Local))

            val ambiguous = matches.filter { it.subject.id != best.subject.id && it.score >= best.score }
            if (ambiguous.isNotEmpty()) {
                score -= AmbiguityPenalty
                warnings.add("Ambiguous subject match: ${ambiguous.joinToString(", ") { it.subject.name }} also matched.")
            }
        }

        return SubjectMatchResult(subjectId, score, evidence, warnings)
    }

    private fun containsCode(candidate: NormalizedTokens, normalizedCode: String): Boolean {
        if (normalizedCode.isEmpty()) return false
        val joined = candidate.joined
        if (joined.contains(normalizedCode)) return true
        val compactCode = normalizedCode.replace(" ", "")
        val compactCandidate = candidate.tokens.joinToString("")
        return compactCode.isNotEmpty() && compactCandidate.contains(compactCode)
    }

    private fun equivalentToAny(candidate: NormalizedTokens, name: String, aliases: List<String>): Boolean {
        val joined = candidate.joined
        val n = Normalizer.normalize(name).joined
        if (n.isNotEmpty() && joined.contains(n)) return true
        return aliases.any { a ->
            val na = Normalizer.normalize(a).joined
            na.isNotEmpty() && joined.contains(na)
        }
    }

    private fun containsKeyword(candidate: NormalizedTokens, keywords: List<String>): Boolean =
        keywords.any { k ->
            val nk = Normalizer.normalize(k).joined
            nk.isNotEmpty() && candidate.joined.contains(nk)
        }
}

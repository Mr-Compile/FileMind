package com.filemind.core.classification

import com.filemind.core.contracts.ClassifierSource
import com.filemind.core.contracts.Evidence

data class ProjectMatchResult(
    val projectId: String?,
    val score: Double,
    val evidence: List<Evidence>,
    val warnings: List<String>
)

class ProjectMatcher(private val catalog: ProjectCatalog) {
    companion object {
        const val AliasMatchScore = 55.0
        const val NameMatchScore = 55.0
        const val KeywordMatchScore = 12.0
    }

    fun match(candidate: NormalizedTokens): ProjectMatchResult {
        data class Hit(val project: ProjectEntry, val score: Double, val kind: String)

        val evidence = mutableListOf<Evidence>()
        val warnings = mutableListOf<String>()
        val matches = mutableListOf<Hit>()

        for (project in catalog.projects) {
            if (project.aliases.any { contains(candidate, it) }) {
                matches.add(Hit(project, AliasMatchScore, "project-alias"))
                continue
            }
            if (contains(candidate, project.name) ||
                (project.clientName != null && contains(candidate, project.clientName))
            ) {
                matches.add(Hit(project, NameMatchScore, "project-name"))
                continue
            }
            if (project.keywords.any { contains(candidate, it) }) {
                matches.add(Hit(project, KeywordMatchScore, "project-keyword"))
            }
        }

        var projectId: String? = null
        var score = 0.0
        if (matches.isNotEmpty()) {
            val best = matches.sortedWith(compareByDescending<Hit> { it.score }.thenBy { it.project.id }).first()
            projectId = best.project.id
            score = best.score
            evidence.add(Evidence(best.kind, "Matched '${best.project.name}'", best.score, ClassifierSource.Local))

            if (matches.any { it.project.id != best.project.id && it.score >= best.score }) {
                warnings.add("Multiple projects matched with equal strength.")
            }
        }

        return ProjectMatchResult(projectId, score, evidence, warnings)
    }

    private fun contains(candidate: NormalizedTokens, term: String): Boolean {
        val nt = Normalizer.normalize(term).joined
        return nt.isNotEmpty() && candidate.joined.contains(nt)
    }
}

package com.filemind.core.classification

/**
 * Infers subject groups from filename patterns without a configured catalog.
 * Filenames sharing a course-code-like token (`IT-412`, `IT421`, `CS 301`)
 * cluster into a canonical subject id `LETTERS-DIGITS` (`IT-412`), letting
 * school files route to School/{Subject}/{FileKind} on their own.
 * Deterministic and offline — inferred ids are ephemeral per scan, never
 * written to the subjects repository.
 */
class SubjectClusterer(private val minGroup: Int = 2) {
    companion object {
        // Cluster-backed inference is near code-match strength (catalog code = 65)
        // so clustered files reach the auto-organize band on their own.
        const val InferredScore = 60.0
        // Word-bounded: letters 2-5, optional separator, 3-4 digits + optional
        // letter suffix (IT-412, IT421, CS 301, MATH201B). Boundaries stop
        // matches inside longer words ("Acceptance-2026" must not hit "ance").
        private val CODE = Regex(
            "(?<![A-Za-z0-9])([A-Za-z]{2,5})[-_ ]?([0-9]{3,4}[A-Za-z]?)(?![A-Za-z0-9])",
            RegexOption.IGNORE_CASE)
        // Common machine-generated prefixes that are not course codes.
        private val DENIED = setOf(
            "img", "pxl", "vid", "dsc", "dscn", "screenshot", "screen", "photo",
            "wa", "wp", "mmexport", "received", "download", "doc", "file",
            "copy", "untitled", "final", "scan", "snap", "new", "temp", "mov")

        private fun key(m: MatchResult): String =
            "${m.groupValues[1]}-${m.groupValues[2]}".uppercase()
    }

    /** Returns canonical code → matching filename count for groups of >= minGroup. */
    fun detect(names: List<String>): Map<String, Int> {
        val groups = mutableMapOf<String, MutableSet<String>>()
        for (name in names) {
            for (m in CODE.findAll(name)) {
                if (m.groupValues[1].lowercase() in DENIED) continue
                groups.getOrPut(key(m)) { mutableSetOf() }.add(name)
            }
        }
        return groups.filter { it.value.size >= minGroup }
            .mapValues { it.value.size }
    }

    /** The detected code contained in [name], or null. */
    fun codeIn(name: String, detected: Set<String>): String? =
        CODE.findAll(name)
            .filter { it.groupValues[1].lowercase() !in DENIED }
            .map { key(it) }
            .firstOrNull { it in detected }
}

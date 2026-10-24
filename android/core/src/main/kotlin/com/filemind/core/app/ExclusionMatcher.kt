package com.filemind.core.app

/**
 * Port of FileMind.Application/Scanning/ExclusionMatcher.cs.
 * `**` matches across separators, `*` within a segment, `?` one char.
 * Kotlin's Regex.escape wraps in \Q...\E, so the glob is compiled char-by-char.
 */
class ExclusionMatcher(globPatterns: List<String>) {
    private val patterns: List<Regex> = globPatterns.map { globToRegex(it) }

    fun isExcluded(pathOrName: String): Boolean = patterns.any { it.matches(pathOrName) }

    private fun globToRegex(glob: String): Regex {
        val sb = StringBuilder("^")
        var i = 0
        while (i < glob.length) {
            when (val c = glob[i]) {
                '*' -> if (i + 1 < glob.length && glob[i + 1] == '*') {
                    sb.append(".*"); i++
                } else {
                    sb.append("[^/\\\\]*")
                }
                '?' -> sb.append(".")
                else -> sb.append(Regex.escape(c.toString()))
            }
            i++
        }
        return Regex(sb.append('$').toString(), RegexOption.IGNORE_CASE)
    }
}

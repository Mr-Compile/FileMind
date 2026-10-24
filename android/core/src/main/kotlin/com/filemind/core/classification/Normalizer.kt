package com.filemind.core.classification

import java.text.Normalizer as JavaNormalizer
import java.util.Locale

data class NormalizedTokens(val original: String, val tokens: List<String>) {
    val joined: String get() = tokens.joinToString(" ")
    val compact: String get() = tokens.joinToString("")
}

object Normalizer {
    private val separatorChars = setOf('-', '_', '.', ',', '(', ')', '[', ']', '{', '}')

    fun normalize(input: String): NormalizedTokens {
        if (input.isBlank()) return NormalizedTokens(input, emptyList())

        val normalized = JavaNormalizer.normalize(input, JavaNormalizer.Form.NFKC)
        val sb = StringBuilder(normalized.length)
        for (c in normalized) {
            when {
                c in separatorChars -> sb.append(' ')
                c.isWhitespace() -> sb.append(' ')
                else -> sb.append(c.lowercase(Locale.ROOT))
            }
        }

        val tokens = sb.toString()
            .split(' ')
            .map { it.trim() }
            .filter { it.isNotEmpty() }
        return NormalizedTokens(input, tokens)
    }

    fun equivalent(a: String, b: String): Boolean =
        normalize(a).compact == normalize(b).compact
}

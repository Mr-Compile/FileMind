package com.filemind.core.classification

import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.ClassifierSource
import com.filemind.core.contracts.ConfidenceBand
import com.filemind.core.contracts.Evidence
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.FileKind

object FileKindMap {
    private val map = mapOf(
        FileKind.Document to arrayOf("pdf", "doc", "docx", "txt", "rtf", "md", "odt"),
        FileKind.Spreadsheet to arrayOf("xls", "xlsx", "csv", "ods"),
        FileKind.Presentation to arrayOf("ppt", "pptx", "odp", "key"),
        FileKind.Image to arrayOf("jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "heic", "svg"),
        FileKind.Video to arrayOf("mp4", "mkv", "avi", "mov", "wmv", "webm", "m4v"),
        FileKind.Audio to arrayOf("mp3", "wav", "flac", "aac", "ogg", "m4a", "wma"),
        FileKind.Archive to arrayOf("zip", "rar", "7z", "tar", "gz", "bz2"),
        FileKind.Code to arrayOf("cs", "sln", "csproj", "js", "ts", "py", "java", "cpp", "c", "h", "rs", "go", "php", "html", "css", "json", "xml", "yml", "yaml", "sql", "sh", "ps1"),
        FileKind.Executable to arrayOf("exe", "msi", "dll", "bat", "cmd", "com"),
        FileKind.Font to arrayOf("ttf", "otf", "woff", "woff2")
    )

    fun fromExtension(extension: String): FileKind {
        val ext = extension.trimStart('.').lowercase()
        for ((kind, exts) in map) {
            if (exts.contains(ext)) return kind
        }
        return FileKind.Other
    }
}

class ClassifierEngine(
    private val subjectMatcher: SubjectMatcher,
    private val projectMatcher: ProjectMatcher,
    private val confidence: ConfidenceEngine
) {
    companion object {
        const val ContextOverTypeBonus = 20.0
        const val ContentMatchWeight = 0.35
    }

    fun classify(
        candidate: FileCandidate,
        extraEvidence: List<Evidence>? = null,
        contentTokens: NormalizedTokens? = null
    ): ClassificationResult {
        val evidence = mutableListOf<Evidence>()
        val warnings = mutableListOf<String>()

        val tokens = Normalizer.normalize(candidate.displayName)

        val fileKind = FileKindMap.fromExtension(candidate.extension)
        if (fileKind != FileKind.Other) {
            evidence.add(Evidence(
                "extension-signal",
                "Extension '${candidate.extension}' => $fileKind", 10.0, ClassifierSource.Local))
        }
        if (candidate.mimeType != null) {
            evidence.add(Evidence(
                "mime-signal",
                "MIME '${candidate.mimeType}'", 8.0, ClassifierSource.Local))
        }

        val subject = subjectMatcher.match(tokens)
        evidence.addAll(subject.evidence)
        warnings.addAll(subject.warnings)

        val project = projectMatcher.match(tokens)
        evidence.addAll(project.evidence)
        warnings.addAll(project.warnings)

        var subjectId = subject.subjectId
        var projectId = project.projectId

        if (contentTokens != null) {
            val csub = subjectMatcher.match(contentTokens)
            for (e in csub.evidence) {
                evidence.add(e.copy(
                    kind = "content-" + e.kind,
                    scoreContribution = e.scoreContribution * ContentMatchWeight))
            }
            warnings.addAll(csub.warnings)
            if (csub.subjectId != null) {
                if (subjectId == null) subjectId = csub.subjectId
                else if (csub.subjectId != subjectId) {
                    warnings.add("Content suggests subject '${csub.subjectId}' differing from filename '$subjectId'.")
                }
            }

            val cproj = projectMatcher.match(contentTokens)
            for (e in cproj.evidence) {
                evidence.add(e.copy(
                    kind = "content-" + e.kind,
                    scoreContribution = e.scoreContribution * ContentMatchWeight))
            }
            warnings.addAll(cproj.warnings)
            if (cproj.projectId != null) {
                if (projectId == null) projectId = cproj.projectId
                else if (cproj.projectId != projectId) {
                    warnings.add("Content suggests project '${cproj.projectId}' differing from filename '$projectId'.")
                }
            }
        }

        if (extraEvidence != null) evidence.addAll(extraEvidence)

        if (subjectId != null && fileKind != FileKind.Other) {
            evidence.add(Evidence(
                "context-over-type",
                "Subject context present; context beats generic type routing.",
                ContextOverTypeBonus, ClassifierSource.Local))
        }

        val score = confidence.score(evidence)
        val band = confidence.band(score)
        val explanation = buildExplanation(fileKind, subjectId, projectId, band, evidence)

        return ClassificationResult(
            fileKind = fileKind,
            subject = subjectId,
            project = projectId,
            evidence = evidence,
            confidence = score,
            sources = listOf(ClassifierSource.Local),
            explanation = explanation,
            warnings = warnings,
            needsReview = band == ConfidenceBand.Medium || warnings.isNotEmpty()
        )
    }

    private fun buildExplanation(
        kind: FileKind, subject: String?, project: String?, band: ConfidenceBand, evidence: List<Evidence>
    ): String {
        val parts = mutableListOf("kind=$kind")
        if (subject != null) parts.add("subject=$subject")
        if (project != null) parts.add("project=$project")
        parts.add("band=$band")
        parts.add("evidence=${evidence.size}")
        return parts.joinToString("; ")
    }
}

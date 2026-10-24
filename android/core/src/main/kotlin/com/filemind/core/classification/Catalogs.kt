package com.filemind.core.classification

data class SubjectEntry(
    val id: String,
    val name: String,
    val code: String?,
    val aliases: List<String>,
    val keywords: List<String>
)

data class ProjectEntry(
    val id: String,
    val name: String,
    val clientName: String?,
    val aliases: List<String>,
    val keywords: List<String>
)

interface SubjectCatalog {
    val subjects: List<SubjectEntry>
}

interface ProjectCatalog {
    val projects: List<ProjectEntry>
}

class InMemorySubjectCatalog(override val subjects: List<SubjectEntry>) : SubjectCatalog

class InMemoryProjectCatalog(override val projects: List<ProjectEntry>) : ProjectCatalog

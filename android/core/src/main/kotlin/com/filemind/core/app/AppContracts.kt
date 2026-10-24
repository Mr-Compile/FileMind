@file:UseSerializers(InstantSerializer::class)

package com.filemind.core.app

import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.InstantSerializer
import java.time.Instant
import kotlinx.serialization.Serializable
import kotlinx.serialization.UseSerializers

@Serializable
data class CollectionEntry(
    val id: String,
    val name: String,
    val destinationTemplate: String?,
    val categories: List<String>
)

@Serializable
data class SubjectLike(
    val id: String,
    val name: String,
    val code: String?,
    val aliases: List<String>,
    val keywords: List<String>
)

@Serializable
data class ProjectLike(
    val id: String,
    val name: String,
    val clientName: String?,
    val aliases: List<String>,
    val keywords: List<String>
)

@Serializable
data class SmartInboxItem(
    val id: String,
    val path: String,
    val reason: String,
    val classification: ClassificationResult,
    val at: Instant,
    val resolved: Boolean
)

@Serializable
data class JobRequest(val jobType: String, val payload: String, val priority: Int = 0)

@Serializable
data class Job(val id: String, val request: JobRequest, val status: String, val queuedAt: Instant)

@Serializable
data class AiSuggestion(
    val collection: String?,
    val category: String?,
    val subject: String?,
    val project: String?,
    val suggestedName: String?,
    val confidenceHint: Double,
    val explanation: String
)

@Serializable
data class AiRequestContext(
    val displayName: String,
    val extension: String,
    val metadata: Map<String, String>,
    val extractedSnippet: String?
)

@Serializable
data class AppEvent(val kind: String, val entityId: String, val at: Instant, val detail: String? = null)

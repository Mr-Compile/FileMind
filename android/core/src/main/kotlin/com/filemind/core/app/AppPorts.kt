@file:UseSerializers(InstantSerializer::class)

package com.filemind.core.app

import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.InstantSerializer
import com.filemind.core.errors.FileMindError
import com.filemind.core.errors.Result
import java.time.Instant
import kotlinx.coroutines.flow.Flow
import kotlinx.serialization.Serializable
import kotlinx.serialization.UseSerializers

interface Clock {
    val utcNow: Instant
}

interface NotificationService {
    suspend fun notify(notification: Notification): Result<Boolean>
}

@Serializable
data class Notification(val title: String, val body: String, val operationId: String? = null)

interface SecretStore {
    suspend fun get(key: String): Result<String?>
    suspend fun set(key: String, value: String): Result<Boolean>
    suspend fun delete(key: String): Result<Boolean>
}

@Serializable
data class WatchEvent(val path: String, val changeKind: String, val at: Instant)

interface WatcherEventSource {
    fun events(): Flow<WatchEvent>
    suspend fun startWatching(path: String): Result<Boolean>
    suspend fun stopWatching(path: String): Result<Boolean>
}

@Serializable
data class DriveEvent(val driveRoot: String, val kind: String, val at: Instant)

interface RemovableStorageEvents {
    fun events(): Flow<DriveEvent>
}

interface JobQueue {
    suspend fun enqueue(request: JobRequest): Result<Job>
    suspend fun dequeue(): Result<Job?>
    suspend fun complete(jobId: String): Result<Boolean>
    suspend fun fail(jobId: String, error: FileMindError): Result<Boolean>
}

@Serializable
data class SearchHit(val path: String, val collection: String?, val subject: String?, val rank: Double)

interface SearchIndex {
    suspend fun index(candidate: FileCandidate, extractedText: String?): Result<Boolean>
    suspend fun remove(path: String): Result<Boolean>
    suspend fun search(query: String, limit: Int): Result<List<SearchHit>>
}

interface AiPrivacyGate {
    fun prepare(candidate: FileCandidate): Result<AiRequestContext>
}

interface AiProvider {
    suspend fun suggest(context: AiRequestContext): Result<AiSuggestion>
    val isAvailable: Boolean
}

/**
 * Advisory AI escalation: called after deterministic classification when the
 * mode allows AI. Returns a rescored classification with AI evidence merged,
 * or null when AI did not contribute. Never mutates anything itself.
 */
interface AiAdvisor {
    suspend fun assist(candidate: FileCandidate, classification: ClassificationResult): Result<ClassificationResult?>
}

interface EventSink {
    suspend fun emit(appEvent: AppEvent)
}

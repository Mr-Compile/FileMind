package com.filemind.core.parity

import java.io.File
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

/** Locates <repo>/tests/fixtures regardless of the test working directory. */
object FixturePaths {
    val root: File by lazy {
        val prop = System.getProperty("filemind.repoRoot")?.let { File(it) }
        val base = prop ?: File(System.getProperty("user.dir"))
        generateSequence(base) { it.parentFile }
            .firstOrNull { File(it, "tests/fixtures/manifest.json").isFile }
            ?: error("tests/fixtures/manifest.json not found above $base")
    }

    fun file(name: String): File = File(File(root, "tests/fixtures"), name)
}

@Serializable
data class ManifestDoc(
    val version: Int,
    val catalog: ManifestCatalog,
    val exclusions: List<String> = emptyList(),
    val cases: List<ManifestCase>
)

@Serializable
data class ManifestCatalog(
    val subjects: List<ManifestSubject> = emptyList(),
    val projects: List<ManifestProject> = emptyList()
)

@Serializable
data class ManifestSubject(
    val id: String,
    val name: String,
    val code: String? = null,
    val aliases: List<String> = emptyList(),
    val keywords: List<String> = emptyList()
)

@Serializable
data class ManifestProject(
    val id: String,
    val name: String,
    val clientName: String? = null,
    val aliases: List<String> = emptyList(),
    val keywords: List<String> = emptyList()
)

@Serializable
data class ManifestCase(
    val path: String,
    val expectedFileKind: String? = null,
    val expectedSubject: String? = null,
    val expectedProject: String? = null,
    val expectedBand: String? = null,
    val expectedRoute: String? = null,
    val expectedExcluded: Boolean? = null,
    val duplicateGroup: String? = null,
    val note: String? = null
)

// --- rules-parity.json ---

@Serializable
data class RuleParityDoc(
    val version: Int,
    val rules: List<RuleJson>,
    val cases: List<RuleParityCase>
)

@Serializable
data class RuleJson(
    val schemaVersion: Int,
    val id: String,
    val name: String,
    val enabled: Boolean = true,
    val priority: Int = 0,
    val stopProcessing: Boolean = false,
    val mode: String = "all",
    val provenance: String = "User",
    val createdAt: String? = null,
    val updatedAt: String? = null,
    val conditionRoot: ConditionGroupJson,
    val actions: List<RuleActionJson>
)

@Serializable
data class ConditionGroupJson(
    val operator: String,
    val conditions: List<RuleConditionJson> = emptyList(),
    val groups: List<ConditionGroupJson> = emptyList()
)

@Serializable
data class RuleConditionJson(
    val field: String,
    val operator: String,
    val value: String,
    val metadataKey: String? = null
)

@Serializable
data class RuleActionJson(
    val kind: String,
    val destinationTemplate: String? = null,
    val namingTemplate: String? = null,
    val collection: String? = null,
    val tag: String? = null,
    val conflictPolicy: String = "KeepBoth"
)

@Serializable
data class RuleParityCase(
    val path: String,
    val sizeBytes: Long? = null,
    val expectedMatched: List<String> = emptyList(),
    val expectedWinningRule: String? = null,
    val expectedDecision: String? = null,
    val expectedConflicts: Int = 0,
    val expectedDestinationPrefix: String? = null,
    val note: String? = null
)

// --- profile-parity.json ---

@Serializable
data class ProfileParityDoc(
    val schemaVersion: Int,
    val rules: List<RuleJson> = emptyList(),
    val collections: List<ProfileCollection> = emptyList(),
    val subjects: List<ManifestSubject> = emptyList(),
    val projects: List<ManifestProject> = emptyList(),
    val namingTemplates: Map<String, String> = emptyMap(),
    val exclusions: List<String> = emptyList(),
    val preferences: ProfilePreferences? = null
)

@Serializable
data class ProfileCollection(
    val id: String,
    val name: String,
    val destinationTemplate: String? = null,
    val categories: List<String> = emptyList()
)

@Serializable
data class ProfilePreferences(
    val mode: String? = null,
    val highThreshold: Double? = null,
    val mediumThreshold: Double? = null,
    val conflictPolicy: String? = null
)

object ParityJson {
    val parser = Json { ignoreUnknownKeys = true }

    inline fun <reified T> load(name: String): T =
        parser.decodeFromString(FixturePaths.file(name).readText(Charsets.UTF_8))
}

package com.filemind.core.app

import com.filemind.core.classification.ClassifierEngine
import com.filemind.core.classification.Normalizer
import com.filemind.core.classification.SubjectClusterer
import com.filemind.core.contracts.ActionItem
import com.filemind.core.contracts.ClassifierSource
import com.filemind.core.contracts.ActionPlan
import com.filemind.core.contracts.ClassificationResult
import com.filemind.core.contracts.Decision
import com.filemind.core.contracts.Evidence
import com.filemind.core.contracts.FileCandidate
import com.filemind.core.contracts.OperationRecord
import com.filemind.core.contracts.OperationType
import com.filemind.core.contracts.SafetyResult
import com.filemind.core.errors.Result
import com.filemind.core.operations.ActionPlanner
import com.filemind.core.operations.PlanContext
import com.filemind.core.operations.TransactionEngine
import com.filemind.core.operations.UndoService
import com.filemind.core.ports.CandidateEnricher
import com.filemind.core.ports.FileSystemAdapter
import com.filemind.core.ports.OperationStore
import com.filemind.core.ports.PathNormalizer
import com.filemind.core.ports.ProtectedPathProvider
import com.filemind.core.rules.RuleEngine
import com.filemind.core.rules.RuleEvaluation
import com.filemind.core.safety.SafetyContext
import com.filemind.core.safety.SafetyEngine
import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.flow.Flow

data class AnalyzedFile(
    val candidate: FileCandidate,
    val classification: ClassificationResult,
    val rules: RuleEvaluation,
    val decision: Decision
)

data class IndexResult(val indexed: Int, val skipped: Int, val warnings: List<String>)

interface IndexingService {
    suspend fun index(candidates: List<FileCandidate>): Result<IndexResult>
}

interface ConfidenceCalibrator {
    suspend fun adjust(key: String, confidence: Double): Double
    suspend fun record(key: String, signal: CalibrationSignal)
}

enum class CalibrationSignal { Accepted, Rejected }

interface CorrectionService {
    suspend fun observeUndo(record: OperationRecord)
}

class OrganizerPipeline(
    private val scanner: FileScanner,
    private val classifier: ClassifierEngine,
    private val ruleEngine: RuleEngine,
    private val decisionMaker: DecisionMaker,
    private val planner: ActionPlanner,
    private val safety: SafetyEngine,
    private val transaction: TransactionEngine,
    private val undo: UndoService,
    private val fs: FileSystemAdapter,
    private val paths: PathNormalizer,
    private val protectedPaths: ProtectedPathProvider,
    private val rules: RuleRepository,
    private val collections: CollectionRepository,
    private val operations: OperationRepository,
    private val operationStore: OperationStore,
    private val indexing: IndexingService? = null,
    private val enricher: CandidateEnricher? = null,
    private val advisor: AiAdvisor? = null,
    private val enrichmentTextLimit: Int = 8192,
    private val calibrator: ConfidenceCalibrator? = null,
    private val corrections: CorrectionService? = null
) {
    fun scan(root: String, recursive: Boolean = true): Flow<FileCandidate> =
        scanner.scan(root, recursive)

    suspend fun analyze(candidates: List<FileCandidate>): Result<List<AnalyzedFile>> {
        val ruleList = rules.getAll()
        if (!ruleList.isSuccess) return Result.fail(ruleList.error!!)
        val collectionList = collections.getAll()
        if (!collectionList.isSuccess) return Result.fail(collectionList.error!!)

        // Inferred subjects: filename clusters sharing a course-code pattern
        // group into School/{Subject}/... without a configured catalog (F-P12).
        val clusterer = SubjectClusterer()
        val inferred = clusterer.detect(candidates.map { it.displayName })

        val results = ArrayList<AnalyzedFile>(candidates.size)
        for (candidate in candidates) {
            var effective = candidate
            var extraEvidence: List<Evidence>? = null
            var contentTokens: com.filemind.core.classification.NormalizedTokens? = null
            var enrichWarnings: List<String>? = null

            if (enricher != null) {
                val enr = enricher.enrich(candidate, fs)
                if (enr.isSuccess && enr.value != null) {
                    val ev = enr.value
                    effective = candidate.copy(
                        metadata = ev.metadata,
                        extractedTextRef = ev.extractedText ?: candidate.extractedTextRef
                    )
                    extraEvidence = ev.evidence
                    enrichWarnings = ev.warnings
                    if (ev.extractedText != null) {
                        contentTokens = Normalizer.normalize(
                            ev.extractedText.take(minOf(ev.extractedText.length, enrichmentTextLimit)))
                    }
                } else if (!enr.isSuccess) {
                    enrichWarnings = listOf("enrichment failed: ${enr.error!!.message}")
                }
            }

            val inferredCode = if (inferred.isNotEmpty())
                clusterer.codeIn(effective.displayName, inferred.keys) else null
            val evidenceIn = buildList {
                extraEvidence?.let { addAll(it) }
                if (inferredCode != null) add(Evidence(
                    "inferred-subject",
                    "Auto-grouped: ${inferred[inferredCode]} files share course code '$inferredCode'.",
                    SubjectClusterer.InferredScore, ClassifierSource.Local))
            }
            var classification = classifier.classify(
                effective, evidenceIn.ifEmpty { null }, contentTokens)
            if (!enrichWarnings.isNullOrEmpty()) {
                classification = classification.copy(
                    warnings = classification.warnings + enrichWarnings)
            }
            if (advisor != null) {
                val assisted = advisor.assist(effective, classification)
                if (assisted.isSuccess && assisted.value != null) classification = assisted.value!!
            }
            if (calibrator != null) {
                val ext = effective.extension.trimStart('.').lowercase()
                val key = if (ext.isNotEmpty()) "ext:$ext" else "global"
                val adjusted = calibrator.adjust(key, classification.confidence)
                if (adjusted != classification.confidence) {
                    classification = classification.copy(confidence = adjusted)
                }
            }
            if (classification.subject == null && inferredCode != null) {
                val bonus = if (classification.fileKind != com.filemind.core.contracts.FileKind.Other)
                    listOf(Evidence(
                        "context-over-type",
                        "Subject context present; context beats generic type routing.",
                        20.0, ClassifierSource.Local)) else emptyList()
                val bumped = minOf(100.0,
                    classification.confidence + bonus.sumOf { it.scoreContribution })
                classification = classification.copy(
                    subject = inferredCode,
                    confidence = bumped,
                    // NeedsReview was computed before the subject bonus — rebake
                    // it against the new band so High-confidence clusters organize.
                    needsReview = (bumped >= 70.0 && bumped < 95.0) || classification.warnings.isNotEmpty(),
                    evidence = classification.evidence + bonus)
            }
            val evaluation = ruleEngine.evaluate(effective, classification, ruleList.value!!)
            val decision = decisionMaker.decide(effective, classification, evaluation, collectionList.value!!)
            results.add(AnalyzedFile(effective, classification, evaluation, decision))
        }
        return Result.ok(results)
    }

    fun plan(
        analyzed: List<AnalyzedFile>, dryRun: Boolean,
        destinationRoot: String? = null, now: Instant? = null,
        sourceRoot: String? = null, preserveTree: Boolean = false
    ): Result<ActionPlan> {
        val items = mutableListOf<ActionItem>()
        var requiresReview = false

        val destBase: ((String) -> String)? =
            if (destinationRoot == null) null else ({ rel -> paths.combine(destinationRoot, rel) })

        for (a in analyzed) {
            val planned = planner.plan(a.decision, PlanContext(paths, destBase, now, sourceRoot, preserveTree))
            if (!planned.isSuccess) return Result.fail(planned.error!!)
            items.addAll(planned.value!!.items)
            requiresReview = requiresReview || planned.value.requiresReview
        }

        return Result.ok(ActionPlan(
            planId = UUID.randomUUID().toString().replace("-", ""),
            createdAt = now ?: Instant.now(),
            sourceDecision = "OrganizerPipeline",
            items = items,
            requiresReview = requiresReview,
            dryRun = dryRun,
            reversibleEstimate = items.all { it.undoRecipe != null }
        ))
    }

    suspend fun dryRun(plan: ActionPlan): Result<SafetyResult> {
        val context = buildSafetyContext(plan)
        return Result.ok(safety.validate(plan, context))
    }

    suspend fun execute(plan: ActionPlan): Result<OperationRecord> {
        val context = buildSafetyContext(plan)
        return transaction.execute(plan, context)
    }

    suspend fun undo(operationId: String): Result<OperationRecord> {
        val loaded = operationStore.load(operationId)
        if (!loaded.isSuccess) return Result.fail(loaded.error!!)

        val reverse = undo.buildReversePlan(loaded.value!!)
        if (!reverse.isSuccess) return Result.fail(reverse.error!!)

        val executed = execute(reverse.value!!)
        if (corrections != null && executed.isSuccess) {
            corrections.observeUndo(loaded.value)
        }
        if (calibrator != null && executed.isSuccess) {
            for (item in loaded.value!!.items.filter { it.operation == OperationType.Move }) {
                val ext = item.originalLocation.substringAfterLast('.', "").trimStart('.')
                if (ext.isNotEmpty()) {
                    calibrator.record("ext:${ext.lowercase()}", CalibrationSignal.Rejected)
                }
            }
        }
        return executed
    }

    suspend fun recentOperations(limit: Int): Result<List<OperationRecord>> =
        operations.getRecent(limit)

    suspend fun index(candidates: List<FileCandidate>): Result<IndexResult> =
        indexing?.index(candidates) ?: Result.ok(IndexResult(0, 0, emptyList()))

    private suspend fun buildSafetyContext(plan: ActionPlan): SafetyContext {
        val capabilities = mutableMapOf<String, com.filemind.core.contracts.PermissionCapabilities>()
        val freeSpace = mutableMapOf<String, Long>()

        for (item in plan.items) {
            if (!capabilities.containsKey(item.source)) {
                val caps = fs.checkCapabilities(item.source)
                if (caps.isSuccess) capabilities[item.source] = caps.value!!
            }
            if (item.destination != null) {
                val root = destinationRoot(item.destination)
                if (root != null && !freeSpace.containsKey(item.destination)) {
                    val free = fs.getFreeSpace(root)
                    if (free.isSuccess) freeSpace[item.destination] = free.value!!
                }
            }
        }

        return SafetyContext(
            protectedPaths,
            if (capabilities.isNotEmpty()) capabilities else null,
            if (freeSpace.isNotEmpty()) freeSpace else null
        )
    }

    /**
     * Free-space probe target: the topmost ancestor of the destination —
     * drive root on Windows, volume/docId root under SAF, volume under
     * MediaStore.
     */
    private fun destinationRoot(destination: String): String? {
        var current = paths.normalize(destination)
        var parent = paths.directoryOf(current)
        var guard = 0
        while (parent != null && parent != current && guard++ < 32) {
            current = parent
            parent = paths.directoryOf(current)
        }
        return current
    }
}

package com.filemind.platform

import android.content.Context
import com.filemind.core.app.DecisionMaker
import com.filemind.core.app.ExclusionMatcher
import com.filemind.core.app.FileScanner
import com.filemind.core.app.OrganizerPipeline
import com.filemind.core.classification.ClassifierEngine
import com.filemind.core.classification.ConfidenceEngine
import com.filemind.core.classification.InMemoryProjectCatalog
import com.filemind.core.classification.InMemorySubjectCatalog
import com.filemind.core.classification.ProjectEntry
import com.filemind.core.classification.ProjectMatcher
import com.filemind.core.classification.SubjectEntry
import com.filemind.core.classification.SubjectMatcher
import com.filemind.core.operations.ActionPlanner
import com.filemind.core.operations.TransactionEngine
import com.filemind.core.operations.UndoService
import com.filemind.core.rules.RuleEngine
import com.filemind.core.safety.SafetyEngine
import com.filemind.platform.db.FileMindDatabase
import com.filemind.platform.db.RoomAiAuditRepository
import com.filemind.platform.db.RoomAiUsageLedger
import com.filemind.platform.db.RoomCollectionRepository
import com.filemind.platform.db.RoomExclusionRepository
import com.filemind.platform.db.RoomFileIndexRepository
import com.filemind.platform.db.RoomJobRepository
import com.filemind.platform.db.RoomOperationRepository
import com.filemind.platform.db.RoomOperationStore
import com.filemind.platform.db.RoomProjectRepository
import com.filemind.platform.db.RoomProtectedPathRepository
import com.filemind.platform.db.RoomRuleRepository
import com.filemind.platform.db.RoomSettingsRepository
import com.filemind.platform.db.RoomSmartInboxRepository
import com.filemind.platform.db.RoomSubjectRepository
import com.filemind.platform.db.RoomWatchedFolderRepository
import com.filemind.platform.fs.AndroidFileSystemAdapter
import com.filemind.platform.fs.SafPathNormalizer
import com.filemind.platform.grants.UriGrantStore
import com.filemind.platform.notify.AndroidNotificationService
import com.filemind.platform.protect.AndroidProtectedPathProvider

/**
 * Manual service graph for the Android backend — no DI framework. One
 * instance per process (Application or worker entry points call [from]).
 *
 * Repositories read/written through the core app ports only; the engine is
 * constructed per use so catalogs/rules snapshot at invocation time.
 */
class FileMindAndroid private constructor(private val appContext: Context) {

    val db: FileMindDatabase by lazy { FileMindDatabase.build(appContext) }
    val paths = SafPathNormalizer()
    val grantStore by lazy { UriGrantStore(appContext, db) }

    // Repositories (Room-backed)
    val rules by lazy { RoomRuleRepository(db.rules()) }
    val collections by lazy { RoomCollectionRepository(db.collections()) }
    val subjects by lazy { RoomSubjectRepository(db.subjects()) }
    val projects by lazy { RoomProjectRepository(db.projects()) }
    val fileIndex by lazy { RoomFileIndexRepository(db.fileIndex()) }
    val operationRepository by lazy { RoomOperationRepository(db.operations()) }
    val operationStore by lazy { RoomOperationStore(operationRepository) }
    val smartInbox by lazy { RoomSmartInboxRepository(db.smartInbox()) }
    val exclusions by lazy { RoomExclusionRepository(db.exclusions(), defaultExclusions) }
    val protectedPathsRepo by lazy { RoomProtectedPathRepository(db.protectedPaths()) }
    val watchedFolders by lazy { RoomWatchedFolderRepository(db.watchedFolders()) }
    val settings by lazy { RoomSettingsRepository(db.settings()) }
    val jobs by lazy { RoomJobRepository(db.jobs()) }
    val aiAudit by lazy { RoomAiAuditRepository(db.aiAudit()) }
    val aiUsage by lazy { RoomAiUsageLedger(db.aiUsage()) }

    val notifications by lazy { AndroidNotificationService(appContext) }

    private suspend fun userProtected(): List<String> =
        protectedPathsRepo.getUserPaths().value ?: emptyList()

    /** FS adapter over SAF/MediaStore only — needs live user protected paths. */
    suspend fun fileSystem(): AndroidFileSystemAdapter =
        AndroidFileSystemAdapter(appContext, grantStore, userProtected())

    /** Full pipeline with catalogs/rules snapshotted from the repositories. */
    suspend fun pipeline(): OrganizerPipeline {
        val fs = fileSystem()
        val subjectEntries = (subjects.getAll().value ?: emptyList()).map {
            SubjectEntry(it.id, it.name, it.code, it.aliases, it.keywords)
        }
        val projectEntries = (projects.getAll().value ?: emptyList()).map {
            ProjectEntry(it.id, it.name, it.clientName, it.aliases, it.keywords)
        }
        val confidence = ConfidenceEngine()
        val classifier = ClassifierEngine(
            SubjectMatcher(InMemorySubjectCatalog(subjectEntries)),
            ProjectMatcher(InMemoryProjectCatalog(projectEntries)),
            confidence)
        val excl = ExclusionMatcher(exclusions.getPatterns().value ?: emptyList())
        return OrganizerPipeline(
            scanner = FileScanner(fs, paths, excl),
            classifier = classifier,
            ruleEngine = RuleEngine(),
            decisionMaker = DecisionMaker(confidence),
            planner = ActionPlanner(),
            safety = SafetyEngine(fs, paths),
            transaction = TransactionEngine(fs, operationStore, SafetyEngine(fs, paths)),
            undo = UndoService(fs),
            fs = fs,
            paths = paths,
            protectedPaths = AndroidProtectedPathProvider(userProtected()),
            rules = rules,
            collections = collections,
            operations = operationRepository,
            operationStore = operationStore)
    }

    companion object {
        @Volatile private var instance: FileMindAndroid? = null

        fun from(context: Context): FileMindAndroid =
            instance ?: synchronized(this) {
                instance ?: FileMindAndroid(context.applicationContext).also { instance = it }
            }

        /** Android-side defaults; the shared manifest list is layered on top by callers. */
        val defaultExclusions = listOf(
            "*.tmp", "*.part", "*.crdownload", "desktop.ini", "thumbs.db",
            "**/node_modules/**", "**/bin/**", "**/.git/**"
        )
    }
}

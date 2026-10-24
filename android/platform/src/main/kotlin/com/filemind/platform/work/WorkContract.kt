package com.filemind.platform.work

/**
 * WorkManager contract — work names, tags, and input/output data keys.
 * Pure constants so JVM tests can pin the contract.
 */
object WorkContract {
    const val WORK_SCAN = "filemind.scan"
    const val WORK_ORGANIZE = "filemind.organize"

    const val TAG_SCAN = "filemind:scan"
    const val TAG_ORGANIZE = "filemind:organize"

    // ScanWorker inputs
    const val IN_ROOT_URI = "rootUri"
    const val IN_RECURSIVE = "recursive"
    const val IN_SOURCE_SCOPE = "sourceScope" // hint used when root URI doesn't imply one

    // OrganizeWorker inputs
    const val IN_PLAN_JSON = "planJson"      // serialized ActionPlan
    const val IN_DRY_RUN = "dryRun"

    // Shared outputs
    const val OUT_STATUS = "status"          // committed | needs_attention | failed | cancelled
    const val OUT_MESSAGE = "message"

    // ScanWorker outputs
    const val OUT_SCANNED = "scanned"
    const val OUT_REVIEWED = "needsReview"
    const val OUT_INDEXED = "indexed"

    // OrganizeWorker outputs
    const val OUT_OPERATION_ID = "operationId"
    const val OUT_ITEMS_COMMITTED = "itemsCommitted"
    const val OUT_ITEMS_FAILED = "itemsFailed"

    const val STATUS_COMMITTED = "committed"
    const val STATUS_NEEDS_ATTENTION = "needs_attention"
    const val STATUS_FAILED = "failed"
    const val STATUS_CANCELLED = "cancelled"
}

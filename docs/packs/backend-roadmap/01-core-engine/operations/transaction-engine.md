# Transaction Engine
Lifecycle: Planned -> Validating -> Executing -> Verifying -> Committed. On failure, persist error and invoke rollback when safe. Batch operations are transaction groups with per-item states; partial failure must be visible and recoverable.

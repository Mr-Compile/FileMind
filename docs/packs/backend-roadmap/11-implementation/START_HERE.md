# Start Here — First Implementation Sprint

1. Create solution/repository boundaries from REPOSITORY_TREE.md.
2. Implement domain value objects and contracts: FileCandidate, ClassificationResult, ActionPlan, OperationResult, structured errors.
3. Implement normalization and subject/project matcher tests using fixtures.
4. Implement confidence policy.
5. Implement Rule schema + evaluator without filesystem access.
6. Implement Safety Engine against a fake FileSystemAdapter.
7. Implement Action Planner and transaction state machine against fakes.
8. Add SQLite schema/migrations only after domain contracts stabilize enough for persistence.
9. Build Windows adapter last in the sprint and run only against disposable fixture directories.

Do not start Groq, Android, watchers, or UI until the core exit gate passes.

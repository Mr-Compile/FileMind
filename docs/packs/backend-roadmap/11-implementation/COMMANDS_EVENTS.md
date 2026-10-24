# Commands and Events

## Commands
ScanFolder, AnalyzeFile, PlanOrganization, ExecutePlan, ExecuteBatch, UndoOperation, RollbackOperation, AddRule, UpdateRule, TestRule, ImportProfile, ExportProfile, AddWatchFolder, RemoveWatchFolder, Reindex, RequestAIClassification.

## Domain/Application Events
FileDiscovered, FileEnriched, ClassificationCompleted, RuleMatched, PlanCreated, PlanRejected, OperationStarted, OperationVerified, OperationCommitted, OperationFailed, RollbackStarted, RollbackCompleted, SmartInboxItemCreated, WatchEventReceived, JobQueued, JobFailed, AIRequestStarted, AIResponseValidated.

Events are facts, not hidden commands. Consumers must be idempotent where replay/retry is possible.

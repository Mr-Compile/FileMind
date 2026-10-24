# Backend Modules

## Domain
- Files: FileCandidate, FileIdentity, FileMetadata
- Classification: signals, labels, result, explanation
- Collections: collection/category/subject/project definitions
- Rules: rule, condition, action, priority, evaluator
- Confidence: scoring and thresholds
- Decisions: organize/review/ignore/no-op
- Actions: ActionPlan and ActionItem
- Safety: policies and validation results

## Application
- Scan service/use cases
- Classification orchestrator
- Organization orchestrator
- Rule management
- Smart Inbox
- History/undo
- Duplicate analysis
- Storage analysis
- Search
- Profile import/export
- AI orchestration
- Background jobs

## Infrastructure
- SQLite repositories
- File parsers/extractors
- Hashing
- Search index
- JSON schema serialization
- Groq provider
- logging/telemetry kept local by default

## Platform
### Windows
WindowsFileSystemAdapter, WindowsWatchService, WindowsNotificationService, WindowsCredentialStore, ExternalDriveService, ExplorerIntegration.
### Android
AndroidStorageAdapter, SafPermissionStore, MediaStoreAdapter, WorkManagerScheduler, AndroidNotificationService, AndroidKeystoreStore, ShareIntentAdapter.

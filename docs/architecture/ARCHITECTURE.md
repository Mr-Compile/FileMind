# Architecture

## Current Architecture (verified 2026-10-24, M0–M3 complete)

EXISTING — .NET 10 solution `FileMind.slnx` implementing the approved layering:

- `src/FileMind.Domain` — contracts (`FileCandidate`, `FileIdentity`, `ClassificationResult`/`Evidence`, `ActionPlan`/`ActionItem`/`UndoRecipe`, `SafetyResult`, `OperationRecord`, `Rule`/`ConditionGroup`/`Decision`), error model (`Result<T>`, `FileMindError`, `ErrorCode`), engines: `Normalizer`, `SubjectMatcher`, `ProjectMatcher`, `ConfidenceEngine` (95/70 bands), `ClassifierEngine` (context-over-type), `ConditionEvaluator`, `RuleValidator`, `RuleEngine` (priority + specificity + conflict detection), `SafetyEngine` (protected paths, capabilities, conflicts, free space), `ActionPlanner` (templates + `DestinationBase` rooting + undo recipes), `TransactionEngine` (Planned→Validating→Executing→Verifying→Committed/Failed/NeedsAttention with per-item persistence), `UndoService` (fingerprint-verified reverse plans), `RollbackService`, ports (`IFileSystemAdapter`, `IPathNormalizer`, `IProtectedPathProvider`, `IOperationStore`, `IContentExtractor`, `IMetadataExtractor`, `IHashService`).
- `src/FileMind.Application` — app ports (repositories, `IAIProvider`, `IAiPrivacyGate`, `IJobQueue`, `ISearchIndex`, `IEventSink`, `IClock`), `FileScanner` (FileEntry→FileCandidate, `MimeMap`, scope inference, `ExclusionMatcher` globs), `DecisionMaker` (rules→conflicts→bands→Organize/Review/Ignore/NoOp), `OrganizerPipeline` facade (scan→analyze→plan→dry-run safety→execute→undo).
- `src/FileMind.Infrastructure.Sqlite` — `FileMindDb` (open + `schema_migrations`, v1 schema) + `SqliteStore` implementing all repository interfaces incl. `IOperationStore`; JSON columns for condition trees/actions/operation items; pooling disabled.
- `src/FileMind.Platform.Windows` — `WindowsFileSystemAdapter` (real `System.IO`, deterministic `KeepBoth` suffixing, `Ask`→conflict error, exception→`FileMindError` mapping), `WindowsPathNormalizer`, `WindowsProtectedPaths` (system roots + user extras), `WindowsHashService` (SHA256 full/partial).
- `src/FileMind.Windows.Host` — console verbs `scan`/`analyze`/`plan`/`execute`/`undo`/`history`/`seed-demo`; `FILEMIND_DB`/`FILEMIND_DEST` env config.
- `tests/` — `FileMind.Domain.Tests` (77), `FileMind.Application.Tests` (wiring + Windows E2E on disposable dirs), `FileMind.Testing` (fake FS/clock/paths/AI/inbox stores), `FileMind.Windows.IntegrationTests` (excluded from solution — see its README re: Smart App Control).
- `schemas/` — versioned JSON schemas (`rule.schema.json`, `organizer-profile.schema.json`, draft 2020-12); embedded as test resources.

NOT IMPLEMENTED YET: watchers/automation, content extraction, Groq provider, Android, FTS5 search, UI, crash-recovery reconciler, `Intelligence.*` assemblies (empty placeholders).

## Approved Architecture Intent (PROPOSED until implemented)
Source: `docs/packs/foundation-pack/01-architecture/` and `docs/packs/backend-roadmap/`.

- Style: Clean Architecture + MVVM at clients + Ports/Adapters + job/event-driven processing.
- Layers: Presentation (WinUI 3 / Compose later) → Application (use cases, orchestration, jobs) → Domain (classification, rules, collections, confidence, decisions, safety, action plans) → Infrastructure (SQLite, parsers, hashing, search, Groq) → Platform adapters (Windows FS APIs / Android SAF+MediaStore).
- Core pipeline: `FileCandidate -> Normalize -> Local Signals -> Rule Engine -> Context Classifier -> Confidence -> optional AI -> Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History`.
- Solution layout: `src/FileMind.{Domain,Application,Infrastructure.Sqlite,Intelligence.Local,Intelligence.AI,Platform.Windows,Windows.Host}` + `tests/` per `docs/packs/backend-roadmap/11-implementation/REPOSITORY_TREE.md`.

## Boundaries (approved intent)
- Domain cannot import WinUI, Android, SAF, MediaStore, FileSystemWatcher, or OS-specific APIs.
- AI provider cannot execute filesystem actions. UI cannot move/copy/delete files.
- All mutations originate from an ActionPlan and pass the Safety Engine; all completed mutations emit history records.

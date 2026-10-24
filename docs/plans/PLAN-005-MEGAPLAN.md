# PLAN-005 — FileMind Windows Automation Megaplan (Backend Roadmap Phase 3 / PLAN-001 M4)

Status: IMPLEMENTED (verified 2026-10-24)
Related Feature: F-P4 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Verification Result

- M0–M4 implemented: `src/FileMind.Application/Watching/` (contracts, `WatcherEventQueue`, `DebounceService`, `FileStabilityChecker`, `WatchOrchestrator`, `WatchService`), `src/FileMind.Platform.Windows/` (`WindowsWatcherEventSource`, `WindowsRemovableStorageEvents`, `ConsoleNotificationService`, `SystemClock`), `src/FileMind.Infrastructure.Sqlite/SqliteJobQueue.cs`, host `watch` + `watch-status` verbs, startup recovery requeues `running` jobs.
- Tests: 17 new watch-pipeline tests in `tests/FileMind.Application.Tests/WatchPipelineTests.cs`; full suite 125/125 via `tools/sac-test-runner.fsx` (SAC blocks normal `dotnet test`).
- Host E2E verified: `watch add --mode automatic`, `watch list`, `watch-status`, `watch remove`, and `watch run --seconds` organizing a dropped `.txt` to `dest/Docs/Document/` with `[notify]` + `Committed` op; `.crdownload` never moved; unclassifiable file indexed-only (NoOp).
- Jobs: each due event persists a `watch-event` job (pending→running→done/failed); RetrySafe failures reschedule with exponential backoff (250ms·2^n, ≤5s, max 3) — retry scheduling covered by unit contract, natural-transient-failure injection UNVERIFIED.
- Removable-drive attach/detach pause/resume wired in `watch run` (`DriveInfo` polling); physical USB attach/detach UNVERIFIED (no device on dev machine).
- Remaining gaps (out of scope or future): Explorer/shell integration MISSING; tray/background-service host MISSING (PLAN-014); Android watchers MISSING (PLAN-010).

## Request / Goal

Implement Windows automation per `docs/packs/backend-roadmap/03-windows-automation/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M4): watch folders, FileSystemWatcher integration, event queue + debounce/coalescing, file-stability and download-completion checks, retries/backoff, job cancellation, startup/background host contracts, notifications, removable-drive events, and custom watched folders.

## Repository Audit

### Verified Existing State

- Application ports `EXISTING`: `IWatcherEventSource`, `IRemovableStorageEvents`, `IJobQueue`, `INotificationService`, `IClock`, `IJobRepository`, `IWatchedFolderRepository` (`src/FileMind.Application/Ports/{AppPorts,Repositories}.cs`).
- Pipeline `EXISTING`: scan → classify → rules → plan → safety → execute → verify → history via `OrganizerPipeline`; organization modes (`Manual/Smart/Automatic/AI-Assisted/Local-Only`) declared but mode policy wiring `INCOMPLETE` (F-P2).
- Smart Inbox repository exists; Review-decision persistence into it `INCOMPLETE`.
- Fixture corpus has `.crdownload`/`.part` cases (`tests/fixtures/corpus/downloads/`).
- No watcher implementation, event queue, debounce, stability check, job engine, notification impl, or removable-drive handling exists.

### Evidence / Relevant Files

- Spec: `docs/packs/backend-roadmap/03-windows-automation/PLAN.md`; QA matrix `04-backend-qa/QA_MATRIX.md`; contracts `01-core-engine/contracts/`.
- Ports: `src/FileMind.Application/Ports/AppPorts.cs`, `Repositories.cs`.
- Host: `src/FileMind.Windows.Host/Program.cs` (verb wiring pattern to extend).

### Documentation vs Code Differences

None — this phase is untouched intent. All work items are `MISSING` unless noted above.

## Findings

- Phase spec + port surface: `EXISTING`.
- All implementations: `MISSING`.
- Persisted watched folders: schema exists via `IWatchedFolderRepository` (`INCOMPLETE` — verify SQLite table presence during implementation audit).

## Global Constraints (inherited from PLAN-001 verbatim)

Watcher events are hints — re-stat the filesystem before acting. Never move `.crdownload`, `.part`, temporary/incomplete transfers, or actively changing files. Only `Decision + Safety + ActionPlan -> Platform Adapter -> Verify -> History` mutates. Mode policy decides Execute vs Smart Inbox; ambiguous defaults to review. All jobs idempotent, cancellable, retry-safe with structured logs. No UI dependency into domain/core.

## Proposed Changes / Implementation Phases

### Milestone 0 — Plan + Contract Freeze

Exit gate: watcher/job/notification contracts reviewed against existing ports; gaps documented.
- 0.1 Re-audit ports vs phase needs; add minimal contract surface only where missing (do not duplicate existing ports).
- 0.2 Define `WatchEvent` record + `EventKind` enum (Created/Changed/Renamed/Deleted/Error), `WatchJob` payload shapes.

### Milestone 1 — Event Pipeline (Windows)

Exit gate: FileSystemWatcher events → normalized queue → debounce → stability gate → pipeline entry, covered by unit tests with fake event source.
- 1.1 `WindowsWatcherEventSource` (`System.IO.FileSystemWatcher`): per-folder watchers, filter mapping, error/overflow events surfaced not swallowed.
- 1.2 `WatcherEventQueue`: bounded queue, coalescing (rename collapse, duplicate suppression), backpressure policy.
- 1.3 `DebounceService`: per-path debounce windows via `IClock`; burst coalescing.
- 1.4 `FileStabilityChecker`: size/hash-stability over N polls + write-lock probe; download-completion detection (`.crdownload`, `.part`, known temp extensions — reuse exclusion/temp-skip logic from `WindowsScanner`); re-stat before action.
- 1.5 Wiring: event → stability → `OrganizerPipeline` scan/analyze/plan → mode policy → execute or Smart Inbox.

### Milestone 2 — Jobs, Retry, Cancellation

Exit gate: `IJobQueue` impl persists jobs, survives restart, honors cancellation + backoff.
- 2.1 `SqliteJobQueue` on `IJobRepository` (new migration if schema missing): enqueue/dequeue/complete/fail; idempotent handlers.
- 2.2 Retry/backoff policy + `CancellationToken` throughout; partial-failure visibility per transaction-engine states.
- 2.3 `StartupRecoveryService` extension: reconcile interrupted jobs + stale operations on boot.

### Milestone 3 — Host/Background, Notifications, Removable Drives

Exit gate: host runs a `watch` verb; notifications emitted via contract; drive attach/detach handled.
- 3.1 Host `watch`/`watch-status` verbs + startup/background service contract (tray integration is a UI-phase concern; contract only).
- 3.2 `INotificationService` impl (console/log sink acceptable for backend phase) + notification kinds per spec.
- 3.3 `IRemovableStorageEvents` impl: drive attach/detach → pause/resume watchers, pending op reconciliation.
- 3.4 Watched-folder management use cases + repository impls (add/remove/list watched folders, per-folder mode).

### Milestone 4 — E2E + Hardening

Exit gate: unattended watch organizes a dropped file end-to-end on real NTFS fixture copy; `.crdownload` ignored until stable.
- 4.1 Fixture E2E: drop file → debounce → stability → auto-organize per mode; download-extension cases never moved while incomplete.
- 4.2 Failure paths: watcher overflow, folder deleted, drive removed mid-operation → NeedsReview/review states, no silent loss.

## Non-Goals

Tray/UI notification rendering (UI phase), Android watchers, AI integration, deletes.

## Files Expected to Change

`src/FileMind.Application/**` (watcher/jobs/notifications use cases), `src/FileMind.Platform.Windows/**` (watcher source, stability checker, removable events), `src/FileMind.Infrastructure.Sqlite/**` (job/watched-folder impls, migration if needed), `src/FileMind.Windows.Host/Program.cs`, `docs/features/FEATURES.md`, `docs/architecture/*`, `CHANGELOG.md`.

## New Files Required

`tests/**` watcher/stability/job suites + E2E fixture additions as needed. Migration `NNNN_jobs.sql` only if `IJobRepository` schema missing (verify first).

## Security / Privacy Impact

Watcher telemetry must not log file contents; only paths/metadata per redaction rules. Watched folders honor exclusions/protected paths.

## Error Handling / Edge Cases

Watcher buffer overflow, path deleted between event and stability check, drive removal mid-job, app crash mid-debounce (jobs persisted), permission denied on watched root. All → typed errors or NeedsReview, never silent.

## QA Plan

Unit tests (queue/debounce/stability/policies), integration (SqliteJobQueue, recovery), fixture E2E unattended organize, matrix rows from `04-backend-qa/QA_MATRIX.md` exercised where applicable. `dotnet build`/`dotnet test` green via `--artifacts-path` outside docroot (SAC constraint per `docs/development/BUILD.md`).

## Acceptance Criteria

- [ ] Watch event → normalized queue → debounce → stability → pipeline (tests green)
- [ ] `.crdownload`/`.part`/unstable files never moved; re-stat verified
- [ ] Jobs persist, cancel, retry with backoff, recover across restart
- [ ] Notifications + removable-drive events delivered via contracts
- [ ] Watched folders CRUD + per-folder mode persisted
- [ ] Host `watch` verb organizes dropped file end-to-end on fixture copy
- [ ] F-P4 status updated only after verification

## Risks / Regression Areas

FileSystemWatcher unreliability (overflow, antivirus interference) → overflow handling + rescan-on-overflow; debounce timers → `IClock` for determinism; watcher↔mode policy → review-first default.

## Documentation Updates

`FEATURES.md` F-P4, `docs/architecture/{MODULES,DATA_FLOW,PLATFORM}.md`, `docs/development/DEVELOPMENT.md` (watch usage), `CHANGELOG.md`.

## Commit Plan

`feat(application): watch event pipeline ports/use cases` → `feat(platform-windows): watcher source + stability checks` → `feat(infrastructure-sqlite): job queue + watched folders` → `feat(host): watch verbs` → `test(*):` suites → `docs(*):`.

## Approval

User approval required before implementation.

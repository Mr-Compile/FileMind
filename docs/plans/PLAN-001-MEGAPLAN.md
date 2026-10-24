# PLAN-001 — FileMind Backend Megaplan (Foundation Pack + Backend Roadmap)

Status: COMPLETED — M0–M3 verified 2026-10-24 (all acceptance criteria checked below); M4–M10 milestones were activated as separate plans (PLAN-006/008/009/011–018/020); per-feature truth lives in `docs/features/FEATURES.md`
Related Feature: F-P1 through F-P11 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal
Implement the FileMind Foundation Pack into this greenfield repository: extract the approved packs, scaffold the C#/.NET 10 backend, and deliver the backend milestones defined in `docs/packs/backend-roadmap/ROADMAP.md` — deterministic core engine first, Windows backend second, later phases behind their own approved plans.

## Repository Audit
### Verified Existing State
- Docs-only repo, single foundation commit. All `docs/**` were `TBD`/`UNVERIFIED` placeholders (from `file-organizer-docs-foundation.zip`). No code, solution, tests, or build config.
- `docs/_zips/` contained 4 packs; `FileMind_Foundation_Pack.zip` + `FileMind_Complete_Backend_Roadmap.zip` extracted to `docs/packs/` as approved intent sources.
- No .NET SDK on machine at plan time → Task 0.1 installs it. `FileMind.Windows.Host` is a console host for backend phases (WinUI deferred to UI pack).

### Evidence / Relevant Files
- `docs/packs/foundation-pack/` — product, architecture, boundaries, flows, routing, modules, contracts, data model, safety/privacy, phases, AI rules, DoD.
- `docs/packs/backend-roadmap/` — all of the above expanded: repo tree, START_HERE order, contract fields, engine specs, test-case suites, phase plans, commands/events, QA matrix.

### Existing Rules Found
`AGENTS.md` (workflow, plan-before-code, QA, doc, git rules) + auto-commit rule added under Task 0.2. Pack rules mirrored in `docs/rules/RULES.md`.

### Documentation vs Code Differences
Docs said UNVERIFIED; packs contain approved intent. Resolved by extracting packs and labeling all features `MISSING`/`PROPOSED` in `docs/features/FEATURES.md`.

## Findings
- Product/architecture/rules intent: `EXISTING` (extracted packs).
- All implementation: `MISSING`. .NET toolchain: `MISSING` (blocks build/test).
- Auto-commit rule: `MISSING` → added to `AGENTS.md` in M0.

## Global Constraints (verbatim from packs — apply to every task)
- AI may analyze/recommend; only `Decision + Safety + ActionPlan -> Platform Adapter -> Verify -> History` may mutate files.
- UI never mutates; domain never imports WinUI/Android/SAF/MediaStore/FileSystemWatcher/OS APIs.
- Never silently overwrite; conflict default = Keep Both with deterministic suffixing.
- Offline-first; Groq optional; AI failure never breaks local organization.
- Protected/system/app paths denied by default. No `delete` op in initial core.
- Errors: stable codes + safe messages + retry-safe + needs-review; never swallow FS errors.
- Versioned schemas (`schemaVersion`); numbered forward-only migrations; unknown newer versions fail safely.
- File identity is not path-only; revalidate before actions.
- No Rust FFI until contracts proven. No hard-coded API keys. Logs never contain document contents/secrets.
- Implement only the active phase; M4+ requires own approved plans (PLAN-002+).
- Auto-commit on verified feature/task completion (AGENTS.md).

## Proposed Changes / Implementation Phases

### Milestone 0 — Toolchain, Pack Extraction, Scaffold
Exit gate: `dotnet --version` >= 10.x; `dotnet build`/`dotnet test` green on wired empty solution.
- 0.1 `winget install Microsoft.DotNet.SDK.10`; verify.
- 0.2 Extract packs -> `docs/packs/`; add `## Auto-Commit` to `AGENTS.md`; update README + product/architecture/rules/features docs to point at packs. Commits: `docs(packs):`, `docs(agents):`.
- 0.3 This megaplan file. Commit `docs(plans):`.
- 0.4 `dotnet new sln` + classlibs `FileMind.{Domain,Application,Infrastructure.Sqlite,Intelligence.Local,Intelligence.AI,Platform.Windows}` + console `FileMind.Windows.Host` + xunit `{Domain,Application}.Tests` + `Windows.IntegrationTests`; project refs `Domain <- Application <- {Infra.Sqlite, Intelligence.*, Platform.Windows} <- Host`; `Directory.Build.props` (nullable, implicit usings, latest lang). Commit `chore(scaffold):`.
- 0.5 dotnet `.gitignore`. Commit `chore:`.

### Milestone 1 — Foundation Sprint: Contracts, Schemas, Fixtures
Exit gate: contracts compile; JSON schemas validate; fixture corpus + manifest; round-trip serialization tests pass; zero FS mutation.
- 1.1 Error model: `ErrorCode` enum (validation, permission, protected-path, conflict, not-found, locked, insufficient-space, unsupported, transient-IO, persistence, verification, rollback, AI/*, cancelled), `FileMindError` record (code, message, retry-safe, needs-review), `Result<T>`.
- 1.2 Contracts: `FileCandidate`, `FileIdentity`, `ClassificationResult`+`Evidence`, `ActionPlan`+`ActionItem`+`UndoRecipe`, `SafetyResult`, `OperationRecord`; enums (`OperationType`, `ConflictPolicy`, `OperationStatus`, `ConfidenceBand`, `ClassifierSource`, `SourceScope`); `Rule`/`RuleCondition`/`RuleAction` with AND/OR nesting <= 3.
- 1.3 `schemas/rules/rule.schema.json` + `schemas/profiles/organizer-profile.schema.json` with `schemaVersion`; `JsonSchema.Net` validation tests incl. fail-safe on newer versions.
- 1.4 `tests/fixtures/` corpus + `manifest.json`: school-it421 variants, work, development (node_modules/bin exclusions), downloads (.crdownload/.part), media, duplicates, messy-names.

### Milestone 2 — Core Engine (domain-only, fakes, zero I/O)
Exit gate: synthetic FileCandidates -> classify -> rules -> explainable ActionPlans -> unsafe rejections -> undo recipes; deterministic; no OS APIs.
- 2.1 Normalizer: Unicode NFKC, case-fold, whitespace/separator unify; `IT421`/`IT 421`/`IT-421`/`IT_421` equivalent.
- 2.2 SubjectMatcher + ProjectMatcher: code > alias > keyword; ambiguity lowers score; School vs Work contexts separate.
- 2.3 ConfidenceEngine: weighted evidence + caps; bands >=95/70-94/<70 configurable; never overrides safety.
- 2.4 RuleParser/Validator: JSON -> Rule; condition families; malformed -> typed error.
- 2.5 RuleEngine: priority -> specificity -> rule ID; stop-processing; conflicts -> NeedsReview; full trace.
- 2.6 ClassifierEngine pipeline; context-over-type (`IT421_Lecture_5.mp4` -> School/IT421/Lectures).
- 2.7 SafetyEngine: fixed validation sequence; protected paths deny-by-default; Keep Both suffixing; confirmations surfaced.
- 2.8 ActionPlanner: template resolution, immutable plans, undo recipes, no I/O.
- 2.9 TransactionEngine: Planned->Validating->Executing->Verifying->Committed; Failed/RollingBack/RolledBack/NeedsAttention; persisted transitions; batch partial-failure visibility.
- 2.10 UndoService + RollbackService: reconcile current FS; changed target -> NeedsReview; restart recovery.
- 2.11 Ports: `IFileSystemAdapter` (no Delete), `IContentExtractor`, `IMetadataExtractor`, `IHashService`, `IPathNormalizer`, `IProtectedPathProvider`; app ports `IClock`, `INotificationService`, `ISecretStore`, `IWatcherEventSource`, `IRemovableStorageEvents`, `IJobQueue`, `ISearchIndex`, `IAIProvider`, `IAiPrivacyGate`; repository ports (rules, collections, subjects, projects, file index, operations/history, smart inbox, exclusions, protected paths, watched folders, profiles, settings, jobs, AI audit). Fakes: `FakeFileSystemAdapter` (injectable failures), `InMemoryOperationStore`, `FakeClock`, `StubAiProvider`.

### Milestone 3 — Windows Backend
Exit gate: console host organizes disposable fixture dir: `scan -> preview -> execute -> verify -> history -> restart -> undo` on real NTFS.
- 3.1 `WindowsFileSystemAdapter` (System.IO; reparse-point policy; long-path safe; locked->ErrorCode.Locked; free space; post-op verify) + shared adapter contract test suite.
- 3.2 `WindowsFileIdentityProvider` (FileIndex+VolumeSerial, fallback fingerprint) + `WindowsScanner` (recursive, cancellation, progress, exclusions, stable-file check, temp-extension skip).
- 3.3 `Infrastructure.Sqlite`: `SqliteConnectionFactory` (WAL, FK), `MigrationRunner` (embedded `Migrations/NNNN_*.sql` -> `schema_migrations`, backup-aware), `0001_initial.sql` (25 tables + indexes; `file_content_index` FTS5 prepared), all repository impls via ADO.NET (no ORM).
- 3.4 Application: command dispatcher + use cases (`ScanFolder`, `AnalyzeFile`, `PlanOrganization`, `PreviewOrganization`, `ExecutePlan`, `UndoOperation`, rule + smart-inbox use cases), `IEventSink` facts, `StartupRecoveryService`, mode policies (Manual/Smart/Automatic/AI-Assisted/Local-Only).
- 3.5 Host: composition root + verbs `scan|preview|organize|undo|history|smart-inbox`; redacted local logging; `appsettings.json`.
- 3.6 Integration tests: temp-copy fixture E2E organize/verify/restart-recover/undo.

### Milestones 4–10 — Outlines (each expands to own approved plan before activation)
- M4 Windows Automation: FileSystemWatcher adapter, event queue/debounce, stability+download checks, job queue, background/startup contracts, notifications, removable drives. Gate: unattended watch organizes drops per mode.
- M5 Backend QA/Hardening: adapter contract suite, 10k/50k stress, bursts, failure injection, crash/recovery. Gate: no silent data-loss path.
- M6 Local File Intelligence: plugin extractors (PDF/DOCX/PPTX/TXT/spreadsheet/EXIF/audio/video/archive/code), bounded, fail-safe; smart rename after proven extraction.
- M7 Groq AI: IAIProvider/GroqProvider, privacy gate (per-field toggles, protected paths never sent), schema-validated responses, budgets/cache/audit, ISecretStore, offline fallback. Advisory only.
- M8 Android: Kotlin/Room/SAF/MediaStore/WorkManager/Keystore, same ports + fixture equivalence within platform permissions.
- M9 Search: FTS5 population (async/incremental), filters, SearchUseCase; NL via Groq optional; retrieval local.
- M10 Cross-Platform + Advanced: versioned `organizer-profile.json` export/import/migrate/backup/restore (no file sync); then learning/semantic/multi-provider as justified.

## Non-Goals
WinUI 3/UI design, Groq before M7, Android before M8, deletes, semantic index, file sync, Rust core, FS mutation from domain code.

## Files Expected to Change
`AGENTS.md`, `README.md`, `.gitignore`, `docs/product/*`, `docs/rules/RULES.md`, `docs/architecture/*` (as verified), `docs/features/FEATURES.md`, `docs/development/*` (as verified), `CHANGELOG.md`.

## New Files Required
`docs/packs/**` (done), `docs/plans/PLAN-001-MEGAPLAN.md` (this file), `FileMind.sln`, `Directory.Build.props`, `src/FileMind.*/**`, `tests/FileMind.*/**`, `tests/fixtures/**`, `schemas/rules/rule.schema.json`, `schemas/profiles/organizer-profile.schema.json`.

## Security / Privacy Impact
Privacy-by-design: no telemetry, redacted logs, AI opt-in with per-field gates, no hard-coded keys, protected paths deny-by-default. Enforced by ports + safety engine + tests.

## Error Handling / Edge Cases
Typed `FileMindError` everywhere; partial batch failure visible; crash recovery via persisted operation states; stale plans rejected; external modification -> NeedsReview.

## QA Plan
xunit unit tests per engine; adapter contract suite; SQLite migration tests; fixture E2E; per-milestone exit gates; boundary grep (no `System.IO` mutation in Domain/Application); `dotnet build` + `dotnet test` green. QA evidence recorded per `docs/qa/QA_CHECKLIST.md`.

## Acceptance Criteria
- [x] .NET 10 SDK verified (`dotnet --version` = 10.0.402)
- [x] M1: contracts + schemas + fixtures + serialization tests pass (77 domain tests green)
- [x] M2: full classification/rule/safety/undo case suites green against fakes; zero real-FS calls
- [x] M3: E2E organize->verify->restart->undo on temp fixture copy passes on NTFS (3 E2E tests + live host smoke)
- [x] Boundary proof: no `System.IO` mutation calls in Domain/Application projects (grep-verified 2026-10-24)
- [x] Feature statuses updated to `EXISTING` only for verified features

M0–M3 COMPLETE 2026-10-24. Environment note: Smart App Control enforcement on this machine requires `--artifacts-path` outside `C:\wamp64\www` for build/test; `FileMind.Windows.IntegrationTests` is excluded from the solution until binaries are signed or SAC is off (see `tests/FileMind.Windows.IntegrationTests/README.md`).

## Risks / Regression Areas
winget needs admin/network; fixture E2E restricted to `%TEMP%` copies (harness asserts); .NET 10 targeting pack availability verified post-install; WAMP docroot irrelevant; WinUI/Android toolchains deferred.

## Documentation Updates
Per milestone: `docs/architecture/*` from verified code, `FEATURES.md` statuses, `docs/development/{BUILD,DEVELOPMENT}.md` commands, `CHANGELOG.md`, QA evidence.

## Commit Plan
Small logical auto-commits per task on verified completion: `docs(packs/agents/plans):`, `chore(scaffold):`, `feat(domain):`, `feat(platform-windows):`, `feat(infra-sqlite):`, `feat(application):`, `feat(host):`, `test(*):`. No pushes without explicit request.

## Approval
Approved by user 2026-10-24. M0–M3 executable under this plan; M4+ requires expanded PLAN-002+ approvals.

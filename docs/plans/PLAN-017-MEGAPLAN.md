# PLAN-017 — FileMind PLAN-001 Residual Completion Megaplan (F-P1/F-P2/F-P3 Gaps)

Status: COMPLETED — all milestones implemented, unit-tested (108/108 suite green via `tools/sac-test-runner.fsx`), and host verbs verified end-to-end on a real temp fixture (scan→index/hash→analyze→inbox→mode→plan→execute→undo→rules round-trip)
Related Feature: F-P1, F-P2, F-P3 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Close the residual gaps inside PLAN-001's approved scope that left F-P1/F-P2/F-P3 `INCOMPLETE`: wire hashing/indexing into the scan pipeline, add the crash-recovery reconciler (`StartupRecoveryService`), persist Review decisions into Smart Inbox, implement organization mode policies (Manual/Smart/Automatic/AI-Assisted/Local-Only), and add rule preview/test + standalone rule-file import/export host verbs.

Numbering note: PLAN-016 was allocated to the Android-branding/mockup-logo plan by a concurrent session; this plan takes PLAN-017.

## Repository Audit

### Verified Existing State

- `IFileIndexRepository` implemented in `SqliteStore` (`file_index` table: path, stable_id, fingerprint, scan_id, payload_json, scanned_at) — **zero callers**; scan pipeline never indexes.
- `IHashService` + `WindowsHashService` EXISTING (`sha256:`/`sha256-partial:` 4 MiB boundary) — no caller.
- `FileCandidate.HashState` (`NotComputed/Partial/Full`) + `FileIdentity.ComputeFingerprint(size, mtime)` EXISTING.
- `IOperationStore.LoadIncompleteAsync` / `IOperationRepository.GetIncompleteAsync` EXISTING (non-terminal: not Committed/Failed/RolledBack) — no recovery service consumes them.
- `ISmartInboxRepository` implemented (`smart_inbox` table: id, path, reason, classification_json, at, resolved) — **zero callers**; `DecisionMaker` emits `DecisionKind.Review` decisions that are never persisted.
- `Rule.Mode` field exists; organization modes named (`Manual/Smart/Automatic/AI-Assisted/Local-Only`) in PLAN-001 M3.4 but **no enum/policy/wiring exists**.
- `ISettingsRepository` EXISTING (key/value) — unused by host for mode persistence.
- `RuleValidator.Validate` + `schemas/rules/rule.schema.json` + `IRuleRepository` (GetAll/Get/Upsert/Delete) EXISTING; `RuleEngine.Evaluate` + `RuleEvaluation` EXISTING — no host surface for rule test/import/export.
- Host verbs EXISTING: `scan|analyze|plan|execute|undo|history|seed-demo` — no `smart-inbox`, `rules`, or `mode` verbs (PLAN-001 M3.5 listed `smart-inbox`; absent).
- `TransactionEngine` persists per-item states through Planned→Validating→Executing→Verifying→Committed/NeedsAttention — crash mid-transition leaves non-terminal records needing reconciliation.

### Documentation vs Code Differences

- PLAN-001 M3.4/M3.5 promised mode policies, smart-inbox use cases, `StartupRecoveryService` — `INCOMPLETE` vs code; resolved by this plan.
- PLAN-001 M3.3 claimed "25 tables" and an embedded `Migrations/NNNN_*.sql` runner — actual: 13 tables via inline `FileMindDb.V1`. Migration discipline honored here by extending `FileMindDb.Migrate()` with a versioned `V2` constant (same forward-only numbered chain), not by introducing the unbuilt runner.

## Findings

- Repository/ports surface for all gaps: `EXISTING` — this is wiring + use cases, not new persistence.
- Index wiring, recovery service, Smart Inbox persistence, mode policy, rule verbs: `MISSING`.
- `file_index` has no content-hash column → `V2` migration adds `content_hash` + `hash_state` (also serves PLAN-008's declared need — PLAN-008 reuses, doesn't duplicate).

## Global Constraints (inherited)

- Only `Decision + Safety + ActionPlan -> Platform Adapter -> Verify -> History` mutates files. Indexing/recovery/inbox/modes never move or delete.
- Repair before replace; minimal scoped changes; deterministic; versioned schema; forward-only migration.
- Mode policies decide Execute vs Smart Inbox/preview; ambiguity always resolves toward review.
- Mockup-sync rule: new verbs + status changes update `mockup/` in the same change.

## Proposed Changes / Implementation Phases

### Milestone 0 — Plan + Migration
- 0.1 This plan file. Commit `docs(plans):`.
- 0.2 `FileMindDb` `V2` migration: `ALTER TABLE file_index ADD COLUMN content_hash TEXT` + `ADD COLUMN hash_state TEXT NOT NULL DEFAULT 'NotComputed'` (applied only when `schema_migrations` max < 2; `CurrentSchemaVersion`→2). Migration test: fresh DB reaches v2; pre-v1 rows default correctly.

### Milestone 1 — File Index Wiring (F-P1a)
Exit gate: scanning upserts `file_index` rows with stable_id reuse on moved files.
- 1.1 `FileIndexingService` (Application): per candidate → fingerprint `ComputeFingerprint(size,mtime)` → `FindByFingerprintAsync` to reuse existing stable_id when a file moved (same fingerprint, different path) else new `Guid` → partial content hash when `SizeBytes <= IndexHashSizeCap` (default 64 MiB; sets `hash_state` Partial/Full) → `UpsertAsync`. Per-file failures → skip + warning list, never fail the scan. Cancellable.
- 1.2 `SqliteStore` upsert extended for new columns (store content_hash/hash_state inside `payload_json`? NO — dedicated columns per M0.2; `FindByFingerprintAsync` unchanged).
- 1.3 `OrganizerPipeline` gets optional `IFileIndexRepository` + `IHashService` + indexing hook (`IndexAsync(candidates)`); host `scan`/`analyze`/`plan`/`execute` call index after scan. Tests: re-scan dedupes, moved file keeps stable_id, oversized file stays `NotComputed`.

### Milestone 2 — StartupRecoveryService (F-P1b)
Exit gate: interrupted operations reconcile to safe terminal states on startup.
- 2.1 `StartupRecoveryService` (Application): `LoadIncompleteAsync` → per record: no item ever Committed → `Failed` (interrupted before effect); any item Committed or status Executing/Verifying/RollingBack → `NeedsAttention`. Persists via `IOperationStore.SaveAsync`; returns `RecoveryReport` (counts, ids).
- 2.2 Host invokes recovery after store init for mutating verbs; prints `recovered N op(s): K needs-attention`.
- 2.3 Unit tests: pristine-planned → Failed; partial-executed → NeedsAttention; terminal states untouched.

### Milestone 3 — Smart Inbox Persistence (F-P2a)
Exit gate: Review decisions land in `smart_inbox`, deduped per open item.
- 3.1 `SmartInboxService` (Application): filters `DecisionKind.Review` → dedupe by path against `GetOpenAsync` → `AddAsync` (`Guid` id, reason join, classification payload).
- 3.2 Host verbs: `smart-inbox` (list open: path + reasons + confidence) and `smart-inbox resolve <id>`; analyze/plan/execute record review items.
- 3.3 Tests: review decision persisted once across repeated analyzes; resolve marks resolved.

### Milestone 4 — Organization Modes (F-P2b)
Exit gate: mode policy gates plan→execute deterministically and persists.
- 4.1 `OrganizationMode` enum in `Contracts/Enums.cs` (Manual, Smart, Automatic, AiAssisted, LocalOnly); `ModePolicy` (Application): Manual → plan preview only, `execute` requires `--apply`; Smart → `DecisionKind.Organize`+High executes, Review/others → Smart Inbox; Automatic → all safety-passed items execute; AiAssisted → Smart + reserved AI hook (no-op until PLAN-009); LocalOnly → Smart with AI hard-off.
- 4.2 `mode [get|set <mode>]` host verb persisting via `ISettingsRepository` (default `Manual`); `execute` consults policy; `plan` output shows active mode.
- 4.3 Tests: each mode outcome on fixture decisions; persistence round-trip.

### Milestone 5 — Rule Tooling (F-P3)
Exit gate: `rules list|show|test|import|export` verbs work read-only except import.
- 5.1 `rules list` / `rules show <id>` / `rules export <id> [--out file]` (schema-shaped JSON via `FileMindDb.JsonOptions`).
- 5.2 `rules test <id|path-to.json> <dir>`: loads rule (repo or file), evaluates each scanned candidate via `RuleEngine`, prints per-file match + proposed actions; zero mutation.
- 5.3 `rules import <file>`: deserialize → `RuleValidator.Validate` → upsert with `Provenance = Imported`; malformed → typed error; newer `schemaVersion` fails safe.
- 5.4 Tests: import valid/invalid/newer-version; test-command output shape on fixtures.

### Milestone 6 — Docs, Mockup Sync, Feature Statuses
- 6.1 `FEATURES.md` F-P1/F-P2 → EXISTING (verified), F-P3 → EXISTING except authoring UI (stays noted as UI-scope); `MODULES.md`/`DATA_FLOW.md` updated; `CHANGELOG.md`.
- 6.2 `mockup/`: console simulator gains `smart-inbox`/`rules`/`mode` verbs; Settings modes section becomes functional-truthful; Smart Inbox items reflect persistence. (Coordinate with in-flight PLAN-016 edits: re-read files before editing.)
- 6.3 PLAN-003 status line → COMPLETED (folded here — work verified by commit `b9a865c`+`0061a94`).

## Non-Goals

- Rule authoring UI / templates gallery (PLAN-014 UI scope), AI evidence merging (PLAN-009), watch-mode behaviors (PLAN-005 consumes `ModePolicy` + Smart Inbox), dedupe hashing beyond index-time partial pass (PLAN-008), FTS5 (PLAN-015).

## Files Expected to Change

`src/FileMind.Infrastructure.Sqlite/{FileMindDb,SqliteStore}.cs`, `src/FileMind.Application/**` (indexing, recovery, inbox, mode services), `src/FileMind.Domain/Contracts/Enums.cs` (OrganizationMode), `src/FileMind.Windows.Host/Program.cs`, `tests/**`, `docs/**`, `mockup/**`.

## New Files Required

`docs/plans/PLAN-017-MEGAPLAN.md` (this file), `src/FileMind.Application/Indexing/FileIndexingService.cs`, `src/FileMind.Application/Recovery/StartupRecoveryService.cs`, `src/FileMind.Application/Decisions/{SmartInboxService,ModePolicy}.cs` (or `Services/`), test files per milestone.

## Security / Privacy Impact

Index stores paths/metadata locally only; hashing is local; recovery never deletes user data; no secrets involved.

## Error Handling / Edge Cases

Hash failure on one file → skip + warning, scan continues; DB locked during index → typed persistence error; moved file reuses stable_id via fingerprint match; recovery idempotent (re-run = no-op on terminal states); mode unknown in settings → default Manual; malformed rule JSON → typed validation error.

## QA Plan

Unit tests per service (index dedupe/move/oversize; recovery states; inbox dedupe/resolve; mode outcomes; rule import validation); E2E: scan→index→plan→execute→kill-simulated recovery→smart-inbox on temp fixture; boundary grep unchanged (no `System.IO` mutation in Domain/Application); `dotnet build/test --artifacts-path` green.

## Acceptance Criteria

- [x] Scanned files appear in `file_index` with fingerprints; moved files keep stable_id — E2E `scan` prints `indexed 4, hashed 4`; tests `Index_UpsertsFingerprintAndHash`, `Index_MovedFile_ReusesStableId`, `Index_OversizedFile_SkipsHash`
- [x] `StartupRecoveryService` reconciles non-terminal ops to Failed/NeedsAttention — tests `Recovery_PristinePlanned_FailsCleanly`, `Recovery_PartialOrExecuting_NeedsAttention`, `Recovery_Idempotent_TerminalUntouched`; host prints recovery banner when reconciled > 0
- [x] Review decisions persist to `smart_inbox`, deduped; `smart-inbox` verbs work — E2E `analyze` persisted conf-73 docx; `smart-inbox` list + `resolve` verified; tests `SmartInbox_*`
- [x] Mode policy gates execute per Manual/Smart/Automatic and persists via settings — E2E `mode`/`mode smart`; manual `execute` preview-only until `--apply`; tests `Mode_*`
- [x] `rules` verbs: list/show/test/import/export verified incl. fail-safe import — E2E import→list→test→export round-trip on `rule-docs.json`; tests `RuleJson_*`
- [x] F-P1/F-P2/F-P3 statuses updated only after verification — flipped to EXISTING after suite + E2E
- [x] Mockup synced in same change set — console verbs (mode/smart-inbox/rules), mode-aware execute, indexed scan line, architecture module truth

## Risks / Regression Areas

Hashing large trees → size cap + partial pass; recovery mis-marking partial ops → conservative NeedsAttention bias; ctor param growth on `OrganizerPipeline` → optional params preserve existing call sites; concurrent PLAN-016 mockup edits → re-read mockup files before sync edits.

## Documentation Updates

`FEATURES.md` F-P1/P2/P3, `MODULES.md`, `DATA_FLOW.md` (index + inbox + recovery stages), `DEVELOPMENT.md` (new verbs), `CHANGELOG.md`, `docs/plans/PLAN-003` status line.

## Commit Plan

`docs(plans): PLAN-017` → `feat(infrastructure-sqlite): file_index hash columns (v2)` → `feat(application): file indexing service + wiring` → `feat(application): startup recovery` → `feat(application): smart inbox persistence + verbs` → `feat(application): organization mode policy + verbs` → `feat(host|application): rules tooling verbs` → `test(*):` → `feat(mockup): verb/status sync` → `docs(*):`.

## Approval

User approval recorded 2026-10-24 via the remaining-megaplans queue approval.

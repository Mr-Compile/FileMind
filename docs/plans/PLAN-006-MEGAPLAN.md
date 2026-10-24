# PLAN-006 — FileMind Backend QA / Hardening Megaplan (Backend Roadmap Phase 4 / PLAN-001 M5)

Status: APPROVED
Related Feature: Cross-cutting QA (all backend F-IDs)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Execute `docs/packs/backend-roadmap/04-backend-qa/QA_MATRIX.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M5): required test suites, adapter contract suite, fixture E2E breadth, stress (10k/50k+), failure injection, crash/recovery — release gate: no known silent data-loss path; unsafe ambiguity → review; undo/rollback tested for every mutating operation.

## Repository Audit

### Verified Existing State

- `EXISTING` suites: `tests/FileMind.Domain.Tests/` — Normalization, Matcher, Classifier, Confidence, Rule, Schema, Safety, Planner, Transaction tests; `tests/FileMind.Application.Tests/` — WiringSmoke + WindowsEndToEnd; `tests/FileMind.Branding.Tests/` — parity; `tests/FileMind.Testing/` — `FakeFileSystemAdapter` (injectable failures), support fakes.
- `tests/FileMind.Windows.IntegrationTests` exists but is excluded from `FileMind.slnx` (Smart App Control/WDAC blocks unsigned test binaries — see `docs/development/BUILD.md`, `tests/FileMind.Windows.IntegrationTests/README.md`).
- Fixture corpus covers: school-it421, work, development (bin/node_modules), downloads (.crdownload/.part), media, duplicates, messy-names + `manifest.json`.
- `MISSING`: shared adapter contract suite, stress suite (10k/50k), systematic failure-injection matrix, crash/recovery reconciliation suite, DB-failure paths, USB-removal simulation.

### Evidence / Relevant Files

- `04-backend-qa/QA_MATRIX.md` (matrix source), `01-core-engine/tests/*` (case suites), `tests/fixtures/manifest.json`.
- `src/FileMind.Platform.Windows/WindowsFileSystemAdapter.cs` + `tests/FileMind.Testing/FakeFileSystemAdapter.cs` (contract-suite targets).

### Documentation vs Code Differences

README reports "90 tests pass" (Status) vs "82 tests" (Build) — `INCONSISTENT`, resolved by the doc-fix task in the megaplan-coverage change set.

## Findings

- Matrix rows already satisfied: unit suites, fixture E2E (subset), boundary grep — `EXISTING` (verify per-row during implementation audit, mark honestly).
- Contract suite, stress, failure injection, recovery, release-gate audit: `MISSING`.

## Global Constraints

Fixture E2E restricted to temp copies — harness must assert no real paths touched. No test asserts mutable global state. SAC constraint: run via `dotnet test --artifacts-path <outside C:\wamp64\www>`; do not re-add `FileMind.Windows.IntegrationTests` to the solution unless signed/SAC-off (document outcome).

## Proposed Changes / Implementation Phases

### Milestone 0 — Matrix Gap Analysis

Exit gate: every QA_MATRIX row labeled EXISTING/INCOMPLETE/MISSING with evidence (test names).
- 0.1 Map each matrix row → existing tests; publish gap table inside this plan's findings section.

### Milestone 1 — Adapter Contract Suite

Exit gate: FakeFileSystemAdapter and WindowsFileSystemAdapter pass one shared xunit theory suite.
- 1.1 `FileSystemAdapterContractTests` (shared test base parameterized over adapters): move/copy/rename semantics, Keep Both suffixing, conflict policies, protected-path denial, locked-file → `ErrorCode.Locked`, free-space checks, post-op verify, not-found/stale-handle.
- 1.2 Document divergences as typed results, never silent differences.

### Milestone 2 — Stress

Exit gate: 10k and 50k indexed-file scenarios pass within documented budgets; burst watcher events handled (or deferred to PLAN-005 scope if watcher not yet built — record dependency).
- 2.1 Synthetic corpus generator (no real files committed for 50k; generated under temp).
- 2.2 Scan/classify/plan throughput + memory assertions; cancellation mid-stress; restart mid-stress.

### Milestone 3 — Failure Injection

Exit gate: each injected failure produces typed error or NeedsReview — never silent.
- 3.1 Permission denied, locked file, source disappears mid-op, disk full, DB failure, crash boundaries, USB removal (simulated via fake).
- 3.2 Crash-recovery: kill between transaction states → `StartupRecoveryService` reconciles to Committed/RolledBack/NeedsAttention.

### Milestone 4 — Release Gate + Evidence

Exit gate: gate statement reproducible; undo/rollback covered for every supported mutating op.
- 4.1 Undo/rollback coverage audit per OperationType; missing cases added.
- 4.2 QA evidence recorded in `docs/qa/` + CHANGELOG; FEATURES.md rows touched by findings updated.

## Non-Goals

New product features, UI testing, Android, performance tuning beyond documented budgets, mutating the release-gate definition.

## Files Expected to Change

`tests/**` (new suites), possibly small `src/` fixes where tests expose bugs (each fix = own commit with reproduction test).

## New Files Required

`tests/FileMind.Adapter.ContractTests/` (or shared base inside `FileMind.Testing`), stress + failure-injection test files, generated-corpus tooling under `tests/fixtures/` (generation script + gitignored output).

## Security / Privacy Impact

Tests must never exfiltrate paths/content; generated corpora are synthetic.

## Error Handling / Edge Cases

This plan IS the edge-case coverage. Each failure mode maps to a typed `ErrorCode` or NeedsReview path — gaps become bug reports, not workarounds.

## QA Plan

Self-referential: the milestone exit gates are the QA. All suites green via `--artifacts-path` outside docroot; unrun/unrunnable rows disclosed explicitly in this plan and `docs/qa/QA_CHECKLIST.md`.

## Acceptance Criteria

- [ ] Matrix gap table published; every row has an owner test or a recorded gap
- [ ] Adapter contract suite runs against both adapters
- [ ] 10k/50k stress pass with budgets documented
- [ ] Failure-injection rows produce typed errors / NeedsReview
- [ ] Crash-recovery reconciliation verified per transaction state
- [ ] Undo/rollback tested for every mutating OperationType
- [ ] Release-gate statement: no known silent data-loss path

## Risks / Regression Areas

Long stress runs in CI/uncapped environments → keep budgets configurable; SAC blocks new test DLLs → artifacts-path workaround documented; finding real bugs mid-phase → fixed via smallest responsible repair per AGENTS.md, each with repro test.

## Documentation Updates

`docs/qa/*` evidence, `docs/development/TROUBLESHOOTING.md` (observed failure modes), `CHANGELOG.md`.

## Commit Plan

`test(contracts): shared adapter suite` → `test(stress): 10k/50k corpus + budgets` → `test(failure): injection matrix` → `test(recovery): crash reconciliation` → `fix(*):` any bugs found (one per commit) → `docs(qa):`.

## Approval

User approval required before implementation.

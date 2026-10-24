# PLAN-010 — FileMind Android Backend Megaplan (Backend Roadmap Phase 7 / PLAN-001 M8)

Status: IMPLEMENTED — verified 2026-10-24
Related Feature: F-P8 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24 (implemented + verified)

## Request / Goal

Implement `docs/packs/backend-roadmap/07-android-backend/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M8): Kotlin Android backend — SAF/MediaStore adapters, persisted folder grants, WorkManager jobs, Room/SQLite persistence, share-input routing, notifications contracts, platform safety checks — reusing shared specifications. UI explicitly out of scope.

## Repository Audit

### Verified Existing State

- No Android module/toolchain exists in repo (verify: no `*.kt`, no `gradle` files — `INCOMPLETE` check to re-run at implementation).
- Shared spec surface `EXISTING`: contracts/rules/schemas are documented + JSON-schema-versioned; fixture corpus + manifest define cross-platform expected behavior.
- Shared kernel decision pending: C# domain cannot run on Android — plan must choose Kotlin re-implementation of the same contracts (spec-driven parity) vs shared-schema approach; roadmap says "reusing shared specifications" (specs, not binaries).
- `MISSING`: everything Android — adapter, persistence, jobs, notifications, share input, safety checks.

### Evidence / Relevant Files

- Spec: `07-android-backend/PLAN.md`; exit gate: same core rule/profile fixtures → equivalent classifications/plans on Windows and Android, execution respecting platform permissions.
- `tests/fixtures/manifest.json`, `schemas/rules/rule.schema.json`, `schemas/profiles/organizer-profile.schema.json`.

### Documentation vs Code Differences

None — untouched intent.

## Findings

- Cross-platform spec artifacts: `EXISTING`.
- Android toolchain + all implementation: `MISSING`.
- Parity-test strategy: `PROPOSED` (fixture-equivalence harness shared via JSON).

## Global Constraints (verbatim from pack)

Android access limited to authorized/OS-supported storage mechanisms (SAF persisted grants, MediaStore). Model capability differences explicitly — never pretend unrestricted path access. Undo only where the provider supports safe reversal. Same safety rules: ActionPlan → Safety → verify → history; review-by-default; no deletes.

## Proposed Changes / Implementation Phases

### Milestone 0 — Toolchain + Shared Spec Harness

Exit gate: Gradle/Android SDK build green on machine; spec-parity harness runs JSON fixtures against both backends.
- 0.1 Android Studio/SDK + Gradle setup (documented in `docs/development/BUILD.md`); module skeleton `android/` (Kotlin, Room, WorkManager deps — vetted versions).
- 0.2 Parity fixture format: rule/profile/corpus expectations as JSON consumable by xunit (Windows) and JUnit (Android).

### Milestone 1 — Contracts + Engine Parity (Kotlin)

Exit gate: Kotlin ports of FileCandidate/ClassificationResult/ActionPlan/Rule/errors compile; normalizer/matchers/confidence/rules parity suite green on fixtures.
- 1.1 Data classes + enums mirroring domain contracts (versioned schemaVersion).
- 1.2 Deterministic engine parity: normalization, matchers, confidence, rule eval, safety checks, plan generation.
- 1.3 Fixture-equivalence test suite (the exit gate).

### Milestone 2 — Platform Adapters

Exit gate: authorized-folder scan via SAF/MediaStore produces FileCandidates honoring granted scopes only.
- 2.1 `AndroidFileSystemAdapter` over document/media URIs; persisted folder grants (takePersistableUriPermission); capability model surfaced to caller.
- 2.2 Room/SQLite persistence mirroring repository ports (operations/history/index/rules…); migrations.
- 2.3 Android-specific safety: protected scopes, MediaStore-only volumes, partial-permission states.

### Milestone 3 — Jobs, Notifications, Share Input

- 3.1 WorkManager scheduled jobs (scan/organize) with cancellation; notification contracts via Android channels.
- 3.2 Share-target intake → Smart Inbox/scan flow.
- 3.3 History/undo where reversible; irreversible ops flagged NeedsReview.

### Milestone 4 — Docs + Gate

- 4.1 F-P8 status, MODULES/PLATFORM docs, CHANGELOG; parity evidence recorded.

## Non-Goals

Android UI (post-plan; Material 3 spec exists in ui-ux-foundation), Groq-on-Android until PLAN-009 parity allows, file sync, deletes.

## Files Expected to Change

New `android/` tree, `docs/development/BUILD.md`, `docs/features/FEATURES.md`, `docs/architecture/PLATFORM.md`, `CHANGELOG.md`, shared `schemas/` only if parity requires (versioned changes).

## New Files Required

Entire `android/` Gradle project + parity fixtures/harness.

## Security / Privacy Impact

Keystore for secrets; grant persistence minimal; no telemetry; same privacy rules as Windows backend.

## Error Handling / Edge Cases

Revoked grants mid-scan, MediaStore partial visibility, USB-style removable scopes, provider-side failures → typed errors/NeedsReview; WorkManager constraints.

## QA Plan

JUnit unit + instrumented tests where emulator allows; parity fixtures vs Windows outputs; migration tests; granted/denied capability matrices. Unrunnable-on-CI instrumented checks disclosed.

## Acceptance Criteria

- [x] Kotlin engine parity suite green against shared fixtures — `:core:test` 9/9 (`ManifestParityTest`, `RuleParityTest`, `ProfileParityTest`) against `tests/fixtures/manifest.json` + `rules-parity.json` + `profile-parity.json`; same manifest asserted on Windows by `ManifestParityTests` (96/96 via `sac-test-runner.fsx`), incl. golden fingerprint `400:8de541cd86c9000` matching .NET `UtcTicks`.
- [x] SAF/MediaStore scan respects granted scopes only — `AndroidFileSystemAdapter` gates all SAF access on `UriGrantStore` persisted-permission coverage (`GrantCoverage`), `checkCapabilities`/`AndroidCapabilityModel` models per-URI permissions, grant-less SAF URIs return `PermissionDenied`; covered by `UriModelTest`/`SafPathNormalizerTest`/`ProtectedPathsTest` (18/18 JVM) + instrumented `GrantAndAdapterTest` (compiled; device run UNVERIFIED).
- [x] Operations persist + undo where provider supports reversal — Room `FileMindDatabase` v2 mirrors all repository ports + `OperationStore`; `TransactionEngine`/`UndoService` reused unchanged; SAF `moveDocument` and MediaStore RELATIVE_PATH moves are reversible; copy/no-recipe/foreign-provider ops surface `NeedsAttention`/review; no deletes.
- [x] WorkManager jobs + notifications contracts working — `ScanWorker`/`OrganizeWorker` + `FileMindWorkScheduler` (unique-work, battery/storage constraints, cooperative cancel), `NotificationContracts` 3-channel contract + `AndroidNotificationService`; contract pinned by `WorkContractTest`. Runtime behavior on-device UNVERIFIED.
- [x] Share input routes into pipeline — headless `ShareIntakeActivity` (manifest SEND/SEND_MULTIPLE) → `ShareIntake` persists read grants best-effort → `SmartInboxItem` rows → review notification.
- [x] F-P8 status updated after verification — `FEATURES.md` = IMPLEMENTED; `PLATFORM.md`/`MODULES.md`/`BUILD.md`/`CHANGELOG.md` updated; mockup F-P8 chip updated (IMPLEMENTED, offline-styled pending device run).

## Risks / Regression Areas

Toolchain absence (biggest risk — M0 first); permission fragmentation across API levels → explicit capability model; spec drift between C#/Kotlin engines → single parity fixture source.

## Documentation Updates

`BUILD.md` Android toolchain, `FEATURES.md` F-P8, `PLATFORM.md`, `MODULES.md`, `CHANGELOG.md`.

## Commit Plan

`chore(android): toolchain + module skeleton` → `feat(android): contract/engine parity` → `feat(android): SAF/MediaStore adapters + Room` → `feat(android): jobs + notifications + share` → `test(android): parity suite` → `docs(*):`.

## Approval

User approval required before implementation.

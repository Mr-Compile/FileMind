# PLAN-012 — FileMind Cross-Platform Profiles Megaplan (Backend Roadmap Phase 9 / PLAN-001 M10a)

Status: APPROVED
Related Feature: F-P10 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement `docs/packs/backend-roadmap/09-cross-platform/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M10): versioned `organizer-profile.json` containing rules, collections, categories, subjects, projects, aliases, naming templates, exclusions, and portable preferences — export, validation, preview, import, migration, backup, restore. Platform-only settings (Windows paths, Android URI grants) separated. No user-file sync.

## Repository Audit

### Verified Existing State

- `schemas/profiles/organizer-profile.schema.json` `EXISTING` (delivered PLAN-001 M1.3); JSON-schema validation tests `EXISTING` (`SchemaTests`).
- Persistence `EXISTING`: repositories for rules/collections/subjects/projects/exclusions/settings (`SqliteStore` + `FileMindDb` tables verified).
- `MISSING`: profile document assembly/serialization, import/export/preview use cases, conflict resolution policy, backup/restore flow, platform-only separation impl, host verbs.

### Evidence / Relevant Files

- Spec: `09-cross-platform/PLAN.md`; `schemas/profiles/organizer-profile.schema.json`; `src/FileMind.Application/Ports/Repositories.cs` (`IProfileRepository`, `ISettingsRepository`).

### Documentation vs Code Differences

None — untouched intent.

## Findings

- Schema + persistence: `EXISTING`.
- Profile orchestration (export/validate/preview/import/migrate/backup/restore): `MISSING`.

## Global Constraints (verbatim from pack)

`organizer-profile.json` is versioned (`schemaVersion`); newer unknown versions fail safely. Platform-only settings kept out of the portable document. Initial cross-device transfer = profile data only — never user files. Import is preview-first: conflicts resolved explicitly, never silently overwrite local config; preserves local safety settings.

## Proposed Changes / Implementation Phases

### Milestone 0 — Profile Document Model

Exit gate: `OrganizerProfile` record + serialization round-trip tests against the schema.
- 0.1 Contract types mirroring `organizer-profile.schema.json` fields (rules/collections/subjects/projects/aliases/templates/exclusions/preferences) + `schemaVersion`.
- 0.2 Platform-only partition: explicit exclusion list (watched paths, grants, device settings) documented + enforced.

### Milestone 1 — Export + Validation

Exit gate: export produces schema-valid JSON from live repositories.
- 1.1 `ExportProfileUseCase`: repository → profile doc → validate → write.
- 1.2 Validation failures → typed errors; redacted safe fields only.

### Milestone 2 — Preview + Import + Migration

Exit gate: import is dry-run/preview-first with explicit conflict resolution; versioned migrations forward-only.
- 2.1 `PreviewImportUseCase`: diff incoming vs existing (add/changed/conflict lists).
- 2.2 `ImportProfileUseCase`: apply with per-section policy (skip/merge/replace chosen explicitly); unknown newer schemaVersion → fail safe.
- 2.3 Migration chain for older profile versions (numbered forward-only, same discipline as DB migrations).

### Milestone 3 — Backup / Restore + Host Verbs

Exit gate: `profile export|import --preview|--apply|backup|restore` verbs work end-to-end.
- 3.1 Timestamped backups before any import; restore flow verified.
- 3.2 Host verbs + docs; F-P10 status update; CHANGELOG.

## Non-Goals

Cloud/account sync (later opt-in), user-file transfer, real-time multi-device merge, UI surfaces.

## Files Expected to Change

`src/FileMind.Domain/**` (profile contracts), `src/FileMind.Application/**` (use cases), `src/FileMind.Infrastructure.Sqlite/**` (reads), `src/FileMind.Windows.Host/Program.cs`, docs.

## New Files Required

Profile contract types + use cases + migration registry + test fixtures (sample profiles at multiple schemaVersions).

## Security / Privacy Impact

Profiles may contain sensitive folder names — export warns + never includes file contents/hashes beyond what's spec'd; backups stored locally; no network.

## Error Handling / Edge Cases

Malformed JSON → typed error; newer schemaVersion → fail safe; partial import failure → rollback to pre-import backup snapshot; conflicting rule IDs → explicit resolution list.

## QA Plan

Schema round-trip tests; import preview correctness; migration chain tests; backup/restore E2E; conflict-policy tests; boundary: import never touches user files.

## Acceptance Criteria

- [ ] Export emits schema-valid `organizer-profile.json`
- [ ] Import requires preview + explicit conflict policy; never silently overwrites
- [ ] Unknown newer schemaVersion fails safely; migrations forward-only
- [ ] Backup created before import; restore verified
- [ ] Platform-only settings excluded from portable doc
- [ ] F-P10 status updated after verification

## Risks / Regression Areas

Schema drift between profile doc and repositories → single contract + parity tests; destructive import → mandatory backup + preview gate; version skew Windows↔Android → fail-safe rule.

## Documentation Updates

`FEATURES.md` F-P10, `docs/architecture/STORAGE.md` (profile store), `CHANGELOG.md`.

## Commit Plan

`feat(domain|application): organizer profile contracts` → `feat(application): export + validation` → `feat(application): preview/import/migration` → `feat(host): profile verbs` → `test(*):` → `docs(*):`.

## Approval

User approval required before implementation.

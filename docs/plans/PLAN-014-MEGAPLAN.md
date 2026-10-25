# PLAN-014 — FileMind Windows UI (WinUI 3) Megaplan (UI/UX Foundation Pack — zip 03)

Status: IN PROGRESS — M0–M5 verified on dev-signed unpackaged build; MSIX install still needs a machine-trusted cert (see "SAC / signing" note below)
Related Feature: F-UI1 (mockup, PLAN-003) + new F-UI2 row for the real Windows UI
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement `docs/_zips/03_file-organizer-ui-ux-foundation-pack.zip` (extracted to `docs/packs/ui-ux-foundation/`) as the real Windows UI: a WinUI 3 desktop app implementing the approved Fluent-inspired Minimal Utility spec — 6 spec screens (Dashboard, Files, Tools, Devices, Activity & Operation Center, Settings) + Organizer + Search destinations — driven by the real backend pipeline. Android UI is deferred (post-PLAN-010; Material 3 spec already in pack).

## Repository Audit

### Verified Existing State

- Backend `EXISTING`: `OrganizerPipeline`, use cases, host — UI calls these; no UI project exists (`FileMind.slnx` verified — 8 src + 4 test projects, none UI).
- Approved UI spec `EXISTING` in `docs/packs/ui-ux-foundation/`: foundations (design language, tokens, typography Segoe UI, themes System/Light/Dark, icons, motion, a11y), components (buttons, bento cards, chips/status 12-state model, progress, file tables, dialogs), ux (operation flow `Select source → Choose action → Pre-check → Resolve restrictions → Confirm if risky → Execute → Progress → Result → Next action`, validation, restrictions, friendly errors, empty/offline states, search/filter/sort, notifications), `platforms/windows.md` (sidebar + content, breadcrumbs, command/search, context menus, compact density, restrained acrylic), `qa/UI_UX_QA_CHECKLIST.md`.
- Canonical tokens `EXISTING`: `branding/colors.json` (18 tokens + gradient, `schemaVersion: 1`) + `src/FileMind.Branding` (`BrandTokens`, `ThemeMode`, `ThemePalette`) with parity tests.
- Token reconciliation gap `EXISTING`: ui-ux spec defines semantic tokens (`surface/elevated`, `text/disabled`, `status/offline`, `status/neutral`, spacing/radius/elevation/density) not yet mapped to `colors.json` — this plan performs the real reconciliation (PLAN-002 deferred it; PLAN-003 does a mockup-local one).
- PLAN-003 mockup (status READY) = canonical visual reference once built; this UI must match it.

### Evidence / Relevant Files

- `docs/packs/ui-ux-foundation/{README,IMPLEMENTATION_GUIDE,ASSET_MANIFEST}.md`, `foundations/01-07`, `components/01-06`, `ux/01-09`, `platforms/windows.md`, `screens/01-06`, `qa/UI_UX_QA_CHECKLIST.md`.
- `branding/colors.json`, `src/FileMind.Branding/BrandTokens.cs`, `src/FileMind.Application/UseCases/OrganizerPipeline.cs`, `src/FileMind.Windows.Host/Program.cs`.

### Existing Rules Found

UI never mutates — all mutations flow `Decision + Safety + ActionPlan -> Platform Adapter -> Verify -> History`. Status never color-only. Truthful progress; no fake progress. Friendly errors (ux/06). IMPLEMENTATION_GUIDE state contracts: components `idle|loading|success|empty|warning|error`; operations `ready|queued|checking|running|paused|completed|completed_with_warnings|failed|cancelled|offline|unsupported|permission_required`.

### Documentation vs Code Differences

ui-ux token model vs `colors.json` partial mapping — `INCONSISTENT`, resolved in M1 of this plan.

## Findings

- Spec + tokens + backend: `EXISTING`. WinUI project + all screens: `MISSING`.
- Mockup reference: depends on PLAN-003 completion.

## Global Constraints

UI is a presentation layer over Application use cases — zero direct FS mutation, zero domain-rule bypass. Mode policy gates execution. Every destructive tier gets spec'd confirmations. Compact density + keyboard + context menus + breadcrumbs + multi-select + drag/drop per windows.md. Acrylic only on transient layers. System/Light/Dark via `ThemePalette`.

## Proposed Changes / Implementation Phases

Order follows pack `IMPLEMENTATION_GUIDE.md` build order.

### Milestone 0 — Project + Token Reconciliation

Exit gate: `FileMind.WinUI` project builds; theme resource dictionaries generated from canonical tokens; spec-token↔colors.json mapping table committed.
- 0.1 WinUI 3 project scaffolding (Windows App SDK vetted version) + slnx entry; MSIX/unpackaged decision recorded.
- 0.2 Semantic-token map: spec tokens → `colors.json` values (fill `surface/elevated`, `text/disabled`, `status/offline`, `status/neutral`, spacing/radius/elevation scales); emit XAML `ResourceDictionary` per theme; provenance comments.

### Milestone 1 — Foundations: Typography, Icons, Theme Service

- 1.1 Segoe UI Variable type ramp per `03-typography-fonts.md`; icon abstraction per `05-icons-svg-assets.md` (SVG→IconElement/PathIcon strategy).
- 1.2 Theme service wrapping `BrandTokens`/`ThemePalette`; System/Light/Dark + density setting; reduced-motion honoring.

### Milestone 2 — Core Components + Status System

Exit gate: component gallery page demonstrates all spec components + both state contracts.
- 2.1 Buttons/actions, bento cards, chips/badges/status (12-state op model), progress indicators, file lists/tables, dialogs/sheets/toasts — each modeling `idle|loading|success|empty|warning|error`.
- 2.2 Error boundary: `FileMindError` → friendly title/explanation/recovery/severity per ux/06 + GUIDE error contract.

### Milestone 3 — Shell + Navigation

- 3.1 Title bar, left sidebar (Dashboard/Files/Organizer/Tools/Search/Devices/Activity/Settings), breadcrumbs, command/search box, global operation-center entry, inspector pane, back/forward/up, resizable + min-size + compact density.

### Milestone 4 — Screens over Real Pipeline

Exit gate: each screen renders live state from use cases; missing backend surfaces shown disabled/truthful.
- 4.1 Dashboard bento (storage/devices/quick actions/activity), Files (scope nav, table, sort/filter/search, multi-select, context menu, inspector), Organizer (full operation flow → `OrganizerPipeline`), Tools (truthful availability), Search (FTS5 when PLAN-011 lands; honest banner until then), Devices (This PC real; Android truthful `Planned`), Activity & Operation Center (live `IEventSink`/operation records; pause/cancel), Settings (theme/density real; modes gated to implemented policy).

Implemented 2026-10-24: `FmBackend` composition root mirrors the host wiring
(same SqliteStore, OrganizerPipeline, SearchUseCase, Storage/Duplicate
services, SmartInbox, ModePolicy, optional Groq advisor). All eight pages
render live state: Dashboard (storage/inbox/recent ops bento), Files
(index listing + name/extension filter + index-folder action), Organizer
(select → analyze → plan preview → safety → execute → result/undo),
Search (real FTS5 + kind filter), Devices (real DriveInfo; Android
"Planned"), Activity (operation records + undo), Tools (truthful
availability), Settings (live theme/density; mode displayed, host-owned).

### SAC / signing note (M4 blocker)

Smart App Control is ON (enforcement) on this dev box. Unsigned
`FileMind.*.dll` image loads are blocked per-file-hash (CodeIntegrity
3077). A CurrentUser self-signed code-signing cert DID satisfy SAC at
verification time — `tools/fm-sign.ps1` signs every `FileMind.*` +
apphost and the signed unpackaged build ran the full M4/M5 UIA evidence
pass (see `docs/qa/QA_EVIDENCE_PLAN-014.md`). HOWEVER: since 2026-10-25
SAC denies even dev-signed managed image loads (per-hash verdicts
re-evaluated; previously-allowed hashes revoked). WASDK single-file is no
escape: it requires `IncludeAllContentForSelfExtract=true` (Dll SxS
redirection), so managed DLLs extract to `%TEMP%\.net\…` as blockable
images anyway. Signed MSIX install still fails 0x800B0109 (root cert not
machine-trusted).

Remedies for packaging (need user/admin action, pick one):
1. Import the dev cert into `Cert:\LocalMachine\Root` (admin once) →
   signed MSIX dev loop works (`tools/fm-sign.ps1` + makeappx flow used).
2. Disable Smart App Control on the dev box.
3. Build/test on a box without SAC enforcement.

M4/M5 runtime verification DONE via the signed unpackaged apphost;
MSIX-as-distribution remains UNVERIFIED. The mockup-parity UI rebuild
(2026-10-25) compiles green and `BackendSmokeTests` passes 3/3, but a
full UIA pass on the new UI is BLOCKED by the SAC enforcement flip until
remedy 1 or 2 is applied.

### Milestone 5 — QA + Accessibility + Docs

- 5.1 `UI_UX_QA_CHECKLIST.md` applied end-to-end (both themes, states, operations, errors, Windows rows, a11y) with recorded evidence. DONE — `docs/qa/QA_EVIDENCE_PLAN-014.md` (UIA-driven: all 9 destinations, real organize+undo on disk, a11y names verified, errors specific; 3 findings fixed: Files init NRE, icon-button name, combo/row announcements).
- 5.2 Mockup parity check vs PLAN-003 build (when present); `FEATURES.md` F-UI2 row; MODULES.md; DEVELOPMENT.md run docs; CHANGELOG.

## Non-Goals

Android UI, tray implementation beyond contracts, re-skinning the console host, deviating from approved screens, implementing backend features inside UI (UI surfaces what exists, truthfully labeled otherwise).

## Files Expected to Change

`FileMind.slnx`, new `src/FileMind.WinUI/**`, `docs/features/FEATURES.md`, `docs/architecture/*`, `README.md`, `CHANGELOG.md`, possibly `branding/` token additions (versioned).

## New Files Required

Entire WinUI project: app host, theme dictionaries, components, screens, view-models (MVVM), DI wiring to Application services.

## Security / Privacy Impact

No telemetry; secrets via `ISecretStore` only; diagnostics never contain tokens/contents; disclosures for technical details.

## Error Handling / Edge Cases

All spec states per checklist; operation interruption on nav/app-close; backend absence/error → friendly surfaces; per-field validation; drag/drop safety (preview + cancel).

## QA Plan

Pack checklist (all sections) + keyboard/focus/contrast/reduced-motion verification + visual parity with mockup + smoke run on real backend via fixture dir. Unrun items disclosed.

## Acceptance Criteria

- [x] WinUI app launches, all nav destinations render per spec (UIA-verified — 9 destinations, 27-switch stability pass)
- [x] Themes System/Light/Dark + density work from canonical tokens (Dark applied live at runtime; pixel parity pending)
- [x] Operation flow runs real pipeline end-to-end with truthful progress (select→analyze→plan→safety→execute→undo verified on disk; mid-run cancel not surfaced — documented)
- [x] All components model required state contracts (`FmComponentState`/`FmOpState`)
- [x] UI/UX QA checklist applied with evidence; zero color-only statuses (`docs/qa/QA_EVIDENCE_PLAN-014.md`)
- [x] UI performs zero direct FS mutation (boundary verified — only a temp diagnostic log write)
- [ ] F-UI2 row added; statuses truthful

## Risks / Regression Areas

Windows App SDK/toolchain weight → M0 spikes first; spec-token mismatch → reconciliation table + reviews; UI/backend drift → mockup reference + FEATURES statuses; SAC may block unpackaged run → documented remedies.

## Documentation Updates

`FEATURES.md` (F-UI2), `MODULES.md`, `PLATFORM.md`, `README.md`, `DEVELOPMENT.md`, `CHANGELOG.md`.

## Commit Plan

`chore(winui): project scaffold` → `feat(winui): tokens/theme/foundations` → `feat(winui): components + status system` → `feat(winui): shell + navigation` → `feat(winui): screens wired to pipeline` → `test(winui):`/`docs(*):`.

## Approval

User approval required before implementation.

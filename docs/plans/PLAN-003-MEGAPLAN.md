# PLAN-003 — FileMind Interactive UI Mockup Megaplan (Static HTML Preview + Mockup-Sync Rule)

Status: COMPLETED (mockup shipped via commits `5a85275`, `04422f8`, `b9a865c`, `0061a94`; F-UI1 verified EXISTING 2026-10-24)
Related Feature: F-UI1 (new row to add in `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

User request: "make me a mockup html for the app and windows app we have showing how this codebase work, make it interactive with mockup data, and add a rule that if the UI is updated or any code functions the mockup html should also update so I can see this without running; make sure the UI is the same in the code; the mockup should clone along with all the features."

Deliverables:

1. A dependency-free static HTML mockup under `mockup/` that renders the approved FileMind UI (ui-ux-foundation pack) for Windows, demonstrates the real backend pipeline end-to-end with mock data that mirrors the actual domain contracts, simulates the console host verbs, and visualizes the codebase architecture. Openable by double-clicking `index.html` — no server, no build, no network.
2. A repository rule (`docs/rules/MOCKUP_RULES.md`, wired into `docs/rules/RULES.md` and `AGENTS.md`) requiring the mockup to be updated in the same change whenever UI-relevant code, contracts, commands, tokens, or feature statuses change — keeping it a truthful preview of the codebase.
3. The mockup clones the *approved* UI spec exactly (tokens, screens, states, copy rules). Where no implementation exists (all UI), it becomes the canonical visual reference that the future WinUI/Android UI must match.

## Repository Audit

### Verified Existing State

- **No UI project exists.** `FileMind.slnx` = `FileMind.{Domain, Application, Infrastructure.Sqlite, Branding, Intelligence.AI, Intelligence.Local, Platform.Windows, Windows.Host}` + test projects. WinUI deferred per PLAN-001 non-goals; Android is M8 (`MISSING`).
- **The "Windows app we have" is the console host** `src/FileMind.Windows.Host/Program.cs`: verbs `scan`, `analyze`, `plan`, `execute [--dry-run]`, `undo <id>`, `history [n]`, `seed-demo`; env `FILEMIND_DB`, `FILEMIND_DEST`; exact output formats verified (e.g. `{DisplayName,-40} {SizeBytes,10:N0} {SourceScope}`, `plan {PlanId} items=N review=B dryRun=B`, `operation {id}: Committed`).
- **Approved UI intent EXISTS** in `docs/packs/ui-ux-foundation/` (extracted under PLAN-002): 6 screens (Dashboard, Files, Tools, Devices, Activity & Operation Center, Settings), IA adds Organizer + Search destinations; foundations (design language Fluent-inspired Minimal Utility, tokens, typography Segoe UI, themes System/Light/Dark, motion, a11y); components (buttons, bento cards, chips/status with 12 canonical operation states, progress, file tables, dialogs); ux (operation flow `Select source → Choose action → Pre-check → Resolve restrictions → Confirm if risky → Execute → Progress → Result → Next action`, validation, restrictions, friendly errors, empty/offline states, search/filter/sort, notifications); `platforms/windows.md` (sidebar + content, breadcrumbs, command/search, context menus, compact density, acrylic only on transient layers); `qa/UI_UX_QA_CHECKLIST.md`.
- **Canonical brand tokens EXIST**: `branding/colors.json` (18 tokens + gradient, `schemaVersion: 1`), `branding/colors.css` (`--fm-*` custom properties, `:root` + `[data-theme="dark"]`), mirrored in `src/FileMind.Branding/BrandTokens.cs` with parity tests (`EXISTING`).
- **Domain contracts for mock data shapes EXIST**: `FileCandidate`, `ClassificationResult`/`Evidence`, `Decision`, `ActionPlan`/`ActionItem`/`UndoRecipe`, `SafetyResult`, `OperationRecord`/`OperationItemState`, `Rule`/`ConditionGroup`/`RuleCondition`/`RuleAction`, enums (`OperationType`, `ConflictPolicy`, `OperationStatus`, `ConfidenceBand`, `ClassifierSource`, `SourceScope`, `HashState`, `FileKind`, `DecisionKind`, `ConditionField`/`Operator`, `RuleActionKind`, `RuleProvenance`).
- **Pipeline EXISTING**: `FileScanner → ClassifierEngine(Normalizer/SubjectMatcher/ProjectMatcher) → RuleEngine → DecisionMaker → ActionPlanner → SafetyEngine → TransactionEngine → Verify → History → UndoService`, facade `OrganizerPipeline`; SQLite `SqliteStore` repositories incl. Smart Inbox + exclusions + protected paths.
- **Feature truth statuses** in `docs/features/FEATURES.md`: F-P1–F-P3 `INCOMPLETE`, F-P4–F-P10 `MISSING`, F-P11 `PROPOSED`.
- **No HTML/mockup/preview assets exist** (verified: `find -name "*.html"` empty).
- Working tree has uncommitted PLAN-002 work: modified `FileMind.slnx`, untracked `src/FileMind.Branding/`, `tests/FileMind.Branding.Tests/` — mockup commits must not sweep these in (same hazard PLAN-002 recorded).

### Evidence / Relevant Files

- UI spec: `docs/packs/ui-ux-foundation/{README,IMPLEMENTATION_GUIDE,ASSET_MANIFEST}.md`, `foundations/01-07`, `components/01-06`, `ux/01-09`, `platforms/windows.md`, `screens/01-06`, `qa/UI_UX_QA_CHECKLIST.md`.
- Tokens: `branding/colors.json`, `branding/colors.css`, `branding/BRAND.md`, `src/FileMind.Branding/BrandTokens.cs`.
- Behavior to mirror: `src/FileMind.Windows.Host/Program.cs`, `src/FileMind.Application/UseCases/OrganizerPipeline.cs`, `src/FileMind.Application/Decisions/DecisionMaker.cs`, `src/FileMind.Domain/Contracts/*.cs`, `src/FileMind.Domain/Operations/*`, `src/FileMind.Domain/Safety/SafetyEngine.cs`.
- Rules/docs: `AGENTS.md`, `docs/rules/RULES.md`, `docs/features/FEATURES.md`, `docs/architecture/ARCHITECTURE.md`, `docs/qa/*`, `README.md`, `CHANGELOG.md`.

### Existing Rules Found

`AGENTS.md` (audit→plan→approve→implement→QA→docs→auto-commit; plans in `docs/plans/`; agent may set `DRAFT`/`READY` only), `docs/rules/RULES.md` (rule authority: user instruction or approved rules docs; fact labels; change discipline), pack UI rules (never color-only status, friendly errors, no fake progress, destructive tiers, offline-first). User's instruction to add a mockup-sync rule is explicit direction → rule creation is authorized.

### Documentation vs Code Differences

- `colors.css` declares only 11 of 18 `colors.json` tokens (missing: surfaceElevated, textSecondary, border) and the dark block overrides only 3 of 6 dark tokens — asymmetry already recorded in PLAN-002. The mockup needs the full token set.
- PLAN-002 Milestone 3 says `FEATURES.md` gets F-B1/F-B2/F-UI1 rows; file currently has only F-P1–F-P11 → `INCONSISTENT` (PLAN-002 doc update not yet applied).
- ui-ux-foundation `02-design-tokens.md` defines semantic tokens (`surface/elevated`, `text/disabled`, `status/offline`, `status/neutral`, spacing/radius/elevation/density) not yet mapped to `branding/colors.json` — reconciliation deferred by PLAN-002 to "a future UI plan". This plan performs the first reconciliation, in the mockup only.

## Findings

- Product UI implementation: `MISSING` (no WinUI/Android project). Mockup renders approved intent, labeled as such.
- Windows app implementation: `EXISTING` as console host → mockup includes a faithful console simulator.
- Canonical tokens: `EXISTING`; full CSS custom-property coverage `INCOMPLETE` → mockup defines extended `--fm-*` vars derived from `colors.json`, documented in `mockup/README.md` (and optionally repairs `branding/colors.css`, see M1.0).
- Mock data shapes: must mirror `src/FileMind.Domain/Contracts/*` verbatim → `MISSING`, created as `mockup/mock-data.js`.
- Mockup-sync rule: `MISSING` → created per explicit user request.
- F-UI1 feature row: `MISSING` → added. F-B1/F-B2 rows `MISSING` (PLAN-002 intent) → added as related doc fix.
- Pipeline visualization: `MISSING` → Architecture screen mirrors verified `ARCHITECTURE.md`.

## Root Cause / Problem

There is no way to see the product UI or demonstrate codebase behavior without building/running the .NET host, and no rule keeps a preview in sync with code. The approved UI intent exists only as prose specs. A living static mockup solves preview + communication, and a sync rule prevents drift — matching the repo's "documentation may be stale; compare with code" principle.

## Global Constraints

- Mockup is **static, offline, dependency-free**: HTML + CSS + vanilla JS. No CDN, frameworks, fonts, or network calls (offline-first rule; opens via `file://`).
- **Single source of truth for tokens**: `mockup/index.html` links `../branding/colors.css` directly — palette can never drift. Extended semantic vars needed by the spec but absent from `colors.css` are defined in `mockup/styles.css` with values copied verbatim from `branding/colors.json` and annotated `// sync: branding/colors.json`.
- **Contract mirroring**: mock data uses exact contract field names (`displayName`, `sizeBytes`, `sourceScope`, `fileKind`, `confidence`, `evidence`, `decision.kind`, `items[].operation/destination/conflictPolicy/reason`, `status`, …). Each block annotated `// mirrors src/FileMind.Domain/Contracts/<File>.cs`.
- **Truthfulness**: unimplemented features render disabled with a truthful status badge (`Missing`/`Planned`), never fake-functional. Status never by color alone (icon + label). No fake progress; simulated progress uses real timers and can be cancelled. Friendly error copy per `ux/06`.
- **UI never mutates**: mockup is read-only visualization; the simulated "execute" only mutates in-memory mock state, clearly labeled `Demo — no real files touched`.
- Console simulator output must match `Program.cs` formats verbatim; unsupported input → `error: Unknown command '…'. Run 'filemind --help'.`
- Theme System/Light/Dark persisted (localStorage), `[data-theme="dark"]` attribute drives `colors.css` — same mechanism a real UI would use.
- No changes to `src/` or `tests/` C# code. In-flight PLAN-002 uncommitted files stay untouched/unstaged.
- Versioned-schema rule: mock rule objects carry `schemaVersion: 1` like real `Rule.SchemaVersion`.

## Proposed Changes / Implementation Phases

### Milestone 0 — Plan + Rules + Feature Rows

Exit gate: rule docs committed; FEATURES.md rows added; this plan committed.

- 0.1 This plan (`docs/plans/PLAN-003-MEGAPLAN.md`). Commit `docs(plans):`.
- 0.2 `docs/rules/MOCKUP_RULES.md` — the user-requested sync rule:
  - `mockup/` is the canonical visual reference of the approved UI until a real UI project ships; then UI and mockup must stay visually equivalent.
  - Any change to domain contracts/enums, pipeline behavior, host verbs/output, feature statuses, `branding/` tokens, or `docs/packs/ui-ux-foundation/` intent **must** update `mockup/` in the same change/commit.
  - Mock data field names mirror contract names; every mock entity cites its source file.
  - Only canonical tokens; extended vars must cite `branding/colors.json`.
  - Unimplemented features must be visibly labeled, never simulated as working.
  - Manual verification (open `mockup/index.html`, walk screens + both themes + console verbs) is part of QA for any UI-relevant change.
- 0.3 `docs/rules/RULES.md` — add pointer under a new `## Approved Mockup Rule` section; `AGENTS.md` — one line under Documentation Rule referencing the mockup-sync rule.
- 0.4 `docs/features/FEATURES.md` — add `F-UI1` (Interactive HTML mockup of approved UI + codebase preview — `INCOMPLETE`→`EXISTING` on completion) and the missing `F-B1`/`F-B2` rows per PLAN-002 intent (doc-consistency fix recorded above). Commit `docs(rules):` + `docs(features):`.

### Milestone 1 — Mockup Shell, Tokens, Theme, Router

Exit gate: `mockup/index.html` opens offline; sidebar + titlebar + hash routing render; System/Light/Dark works; zero console errors.

- 1.0 (Repair, small) Extend `branding/colors.css` to declare all 18 `colors.json` tokens for both themes (`--fm-surface-elevated`, `--fm-text-secondary`, `--fm-border`, `--fm-gradient-*`). Parity tests compare declared props only — adding props keeps parity if values match; verified by re-running `FileMind.Branding.Tests`. If user prefers, defer and keep extended vars in `mockup/styles.css` instead.
- 1.1 `mockup/index.html` — semantic shell: app titlebar (FileMind mark + command/search box), left sidebar nav (Dashboard, Files, Organizer, Tools, Search, Devices, Activity, Settings, Console, Architecture), content region, dialog + toast layers; links `../branding/colors.css` + `styles.css`; scripts `mock-data.js`, `app.js`; `lang`, skip-link, ARIA landmarks.
- 1.2 `mockup/styles.css` — Fluent-inspired Minimal Utility styles: extended `--fm-*` vars (elevated surface, secondary text, border, status offline/neutral — values verbatim from `colors.json`, annotated), spacing (4–48 scale), radius (6/10/14/18/pill), elevation 0–3, bento grid, sidebar (restrained acrylic), cards, chips/status components, file table, dialogs/sheets, toasts, progress, skeletons, focus-visible rings, compact-density class, `prefers-reduced-motion`.
- 1.3 `mockup/app.js` core — hash router (`#/dashboard` … `#/architecture`), nav state, theme service (`system|light|dark`, `matchMedia`, localStorage `fm-theme`), density toggle, icon set (inline SVG, `currentColor`, semantic names per `foundations/05`), toast + dialog helpers, formatting utils (bytes, dates), `esc()` sanitizer (no innerHTML injection).
- 1.4 `mockup/README.md` — what/why/how-to-open, architecture of the mockup, sync contract table (mock entity → contract file), status legend, rule pointer.

### Milestone 2 — Mock Data + Static Screens

Exit gate: all 7 spec screens render with mock data in both themes; truthful labels on missing features.

- 2.1 `mockup/mock-data.js` — `FM_DATA`: ~14 `fileCandidates` (mirroring `FileCandidate`: IT421 lecture mp4 [context-over-type case], alpha project docx, downloads `.crdownload`/`.part` [exclusion cases], photo jpg, archive zip, code cs, duplicates pair, protected-path item), `rules` (3, mirroring `Rule` w/ `conditionRoot`/`actions`/`priority`/`stopProcessing`/`provenance`/`schemaVersion:1`), `collections` (school `School/{Subject}`, work `Work/{Project}` — same as `seed-demo`), `subjects`/`projects` (seed-demo values), `operations` history (mirroring `OperationRecord`/`OperationItemState`), `devices` (This PC connected; Pixel phone disconnected `MISSING` F-P8; USB drive), `storage`, `smartInbox` review items, `settings`. `FM_ENUMS` mirroring `Enums.cs` verbatim with source comment.
- 2.2 Dashboard — bento: storage meter, device status, quick actions (Organize Downloads → routes to Organizer with source preset; Find Duplicates → Tools disabled card), active operations, cleanup opportunities, recent activity. Cards link deeper per spec.
- 2.3 Files — scope selector (Downloads/Documents/Desktop), breadcrumbs, file table (name/type/size/modified/status), sort + filter chips + search box + `Clear all`, multi-select w/ selection bar, context menu (Organize/Copy/Move/Rename/Open location — last shows "demo" toast), details inspector pane, empty/filtered-empty states.
- 2.4 Tools — goal-grouped cards w/ icon, outcome description, truthful availability: Organize (available), Rules viewer (available), Analyze storage (demo), Find duplicates/Cleanup/Rename/Convert (disabled, `Planned` badge).
- 2.5 Search — query + filter chips over mock candidates; honest banner "Demo index — FTS5 search not implemented (F-P9)"; no-result distinction states.
- 2.6 Devices — This PC (connected, capabilities, actions), Android phone (disconnected, reconnect guidance, actions disabled), removable drive; capability/restriction display per spec.
- 2.7 Activity & Operation Center — active op card (state icon+label, stage, determinate progress, pause/cancel), completed ops (result summary, counts, warnings, undo), Smart Inbox section (Review items w/ reasons), status chips for all 12 canonical states (demo strip).
- 2.8 Settings — General/Appearance (theme + density, functional)/File behavior (conflict policy default, organization modes Manual/Smart/Automatic — disabled, `Planned`)/Permissions/Notifications/Advanced (`FILEMIND_DB` path display, `schemaVersion`, danger zone gated)/About.

### Milestone 3 — Interactive Pipeline: Organizer Flow + Console + Architecture

Exit gate: full operation flow works end-to-end on mock state; console verbs match `Program.cs` output; architecture view mirrors verified modules.

- 3.1 Organizer screen — the spec operation flow over the real pipeline semantics: pick source → `Scan` (streaming rows like `scan` verb) → `Analyze` (per-file: kind, confidence bar + band color+label, subject/project, evidence list, `Review` reasons) → `Plan` preview (`plan {id} items=N review=B dryRun=B` + item list src→dest, Keep Both suffixing demo) → `Safety` step (warnings/blockers, protected-path blocker demo) → `Confirm` (destructive-tier appropriate) → `Execute` (staged progress Validating→Executing→Verifying, per-item status, pause/cancel) → `Result` (counts, warnings, undo) → `History` + `Undo` (fingerprint note, `NeedsReview` demo if file "changed"). Dry-run toggle default ON.
- 3.2 Console screen — terminal-styled simulator: input accepts `scan|analyze|plan|execute|undo|history|seed-demo|--help` against the same mock dataset; output text replicates `Program.cs` formatting (columns, `kind=… conf=… subject=… project=… -> Decision`, `warn:`/`blocked:` lines, exit-code hints); prompt `> filemind`.
- 3.3 Architecture screen — "How this codebase works": pipeline diagram (FileCandidate → Normalize → Local Signals → RuleEngine → Classifier/Confidence → [AI optional, greyed `Planned`] → Decision → SafetyEngine → ActionPlan → Platform Adapter → Verify → History/Undo), module cards (Domain, Application, Infrastructure.Sqlite, Platform.Windows, Windows.Host, Branding, Intelligence.* `Missing`, tests) with real file names + `EXISTING`/`INCOMPLETE`/`MISSING` status chips from FEATURES.md, boundaries callouts (UI never mutates; AI advisory only; mutations only via ActionPlan+Safety).
- 3.4 Cross-screen wiring — operations created in Organizer appear in Activity + Dashboard; undo updates both; toasts/dialogs per feedback spec.

### Milestone 4 — QA + Documentation

- 4.1 Apply `docs/packs/ui-ux-foundation/qa/UI_UX_QA_CHECKLIST.md` to the mockup; record results (checked/unrun disclosed) in plan + CHANGELOG.
- 4.2 Parity spot-checks: enum names in `mock-data.js` vs `Enums.cs`; console formats vs `Program.cs`; tokens vs `colors.json` (documented grep in README).
- 4.3 Docs: `README.md` (Documentation section + "Preview the UI" line), `docs/architecture/MODULES.md` (mockup entry as docs-tooling, not a shipped module), `docs/development/DEVELOPMENT.md` (how to open/mockup maintenance), `CHANGELOG.md`, `FEATURES.md` F-UI1 → `EXISTING` post-QA.
- 4.4 Commit `feat(mockup):` + `docs(*):` per commit plan.

## Non-Goals

- No real filesystem access, no backend calls, no Electron/Tauri wrapper, no `src/` or `tests/` changes (except M1.0 optional CSS token completion).
- Not a design proposal: the mockup implements existing approved intent; it does not invent new screens/features.
- No WinUI/XAML/Android resources generated; when a real UI project lands it gets its own plan and must match this reference.
- No test-project coverage for static files beyond documented manual QA + optional parity script.

## Files Expected to Change

`docs/rules/RULES.md`, `AGENTS.md`, `docs/features/FEATURES.md`, `README.md`, `CHANGELOG.md`, `docs/architecture/MODULES.md`, `docs/development/DEVELOPMENT.md`, `branding/colors.css` (M1.0 only).

## New Files Required

`docs/plans/PLAN-003-MEGAPLAN.md`, `docs/rules/MOCKUP_RULES.md`, `mockup/{index.html, styles.css, app.js, mock-data.js, README.md}`.

## Architecture Impact

None on product architecture — `mockup/` is repo tooling/documentation-adjacent, not a shipped module. It must respect boundaries (visualization only). Added to `MODULES.md` as tooling, clearly labeled.

## Data / Storage Impact

None — mock data lives in `mock-data.js`; theme/density persist in browser `localStorage` only.

## Routing / Flow Impact

None on product routing. Mockup uses hash routes mirroring approved IA: `#/dashboard`, `#/files`, `#/organizer`, `#/tools`, `#/search`, `#/devices`, `#/activity`, `#/settings`, `#/console`, `#/architecture`.

## Platform Impact

Mockup renders the **Windows** Fluent-inspired layout (sidebar, breadcrumbs, density, context menus). Android adaptation is representational only (documented in README); Material 3 variant deferred until Android phase.

## Security / Privacy Impact

None — static files, no network, no secrets, no real paths exfiltrated. Mock paths are fictional (`C:\Users\Demo\…`). Console simulator never executes anything.

## Error Handling / Edge Cases

- Mockup must render the spec's required states: loading skeleton, empty, filtered-empty, permission-blocked, offline/disconnected, partial failure, fatal failure — exercised via a dev-state toggle on relevant screens.
- Friendly error copy only (per `ux/06`); simulated failure path (e.g., locked file → `The file is in use…` + retry) demonstrates mapping.
- Deep hash routes recover gracefully; unknown route → Dashboard.
- `localStorage` unavailable (rare `file://` contexts) → theme falls back to System, no crash.

## QA Plan

- Manual evidence-based QA per `docs/qa/QA.md` + pack checklist: every screen both themes, every flow end-to-end, console verbs vs `Program.cs` output, keyboard nav + focus visible, reduced-motion, no color-only statuses, no fake progress, disabled-feature truthfulness, zero browser console errors.
- Sync verification: grep enum/contract names mock↔`Contracts/`; grep console formats↔`Program.cs`; grep token hexes↔`colors.json`.
- If M1.0 runs: `dotnet test FileMind.Branding.Tests` green (needs `--artifacts-path` outside `C:\wamp64\www` per environment note).
- Unrun/unverifiable checks disclosed explicitly.

## Acceptance Criteria

- [ ] `mockup/index.html` opens via `file://` with zero console errors and zero network requests
- [ ] All 6 spec screens + Organizer, Search, Console, Architecture render per ui-ux-foundation intent
- [ ] Full operation flow interactive: scan → analyze → plan → safety → dry-run/execute → result → history → undo
- [ ] Console simulator replicates `Program.cs` verbs/output formats
- [ ] Mock data mirrors domain contract field names (annotated)
- [ ] System/Light/Dark themes work via canonical `branding/colors.css` tokens
- [ ] Every unimplemented feature visibly labeled `Missing`/`Planned`; nothing fake-functional
- [ ] `docs/rules/MOCKUP_RULES.md` exists + referenced from `RULES.md` and `AGENTS.md`
- [ ] `FEATURES.md` has F-UI1 (+ F-B1/F-B2) rows; statuses truthful
- [ ] UI/UX QA checklist applied with results recorded; no unrelated files committed

## Risks / Regression Areas

- Drift between mockup and code → mitigated by MOCKUP_RULES.md + annotated mirrors + QA greps.
- `branding/colors.css` is canonical — M1.0 touches it; parity tests + optional deferral recorded.
- Mockup could be mistaken for a real UI → every screen carries spec-source truthfulness; README + disabled states make status explicit.
- File size: single-page app risks a bloated `app.js` → keep screens as small render functions; documented structure in README.
- Uncommitted PLAN-002 files → staged commits scoped to mockup/rules/docs paths only.

## Documentation Updates

As listed in M4.3; F-UI1 status moves to `EXISTING` only after QA evidence.

## Commit Plan

`docs(plans): add PLAN-003 mockup megaplan` → `docs(rules): mockup-sync rule + features rows` → `feat(branding): complete colors.css token coverage` (M1.0, only if kept) → `feat(mockup): static shell, tokens, router` → `feat(mockup): mock data + spec screens` → `feat(mockup): organizer flow, console, architecture` → `docs(mockup|readme|changelog|features):`. No pushes.

## Approval

Approved by user 2026-10-24 — "Approve as written", mockup location `mockup/` at repo root.

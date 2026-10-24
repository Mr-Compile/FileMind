# PLAN-022 — Cross-Platform UX Parity: Phone-Native Android Surfaces + Feature Parity Megaplan

Status: APPROVED
Related Feature: F-UI1 (mockup), F-UI4 (Android app), F-UI3 (cross-platform UX contracts)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

User: "update the android html to use a phone or portrait mode — whats the use
of the horizontal scroll if we are using a phone? handle it with a dropdown
filters or so on whats the best UI and UX. make a mega plan that the features
of the windows and app android is the same but the UI and UX is different
since its a cross platform — make sure to also make it suitable each platform,
do not just clone them." Approved immediately ("/megaplan ... implemented
immediately").

Two defects and one parity gap:

1. `mockup/android.html` renders a phone shell but uses **horizontally
   scrolling** chip strips (`.a-scope`, `.a-chips`, `.a-steps` all
   `overflow-x: auto`) — a desktop pattern that defeats the point of a phone
   surface.
2. The native `android/app` has the same defect: `FilesScreen.kt` renders two
   `Row`s of `FilterChip`s (~800dp of content on a ~360dp screen) — clipped,
   not scrollable.
3. The Android mockup demos fewer features than the Windows mockup (no rules
   list, no storage analysis, no undo simulation, thin inspector, no active-op
   card, fewer settings groups) — the user wants **feature parity with
   platform-appropriate UX, not a clone**.

User-approved decisions (2026-10-24):

- Scope: **both** mockup and native `android/app`.
- Filter pattern: **filter bottom sheet** (one Filters button with active
  count → M3 modal sheet with folder/kind/sort/size pickers).
- Parity: **full** — rules toggles, storage analysis, undo simulation, live
  active-op card, evidence inspector, retry, dry-run, expanded settings.

## Repository Audit

### Verified horizontal-scroll sites (mockup)

- `mockup/android.css` line 250 `.a-scope` — scope chips, `overflow-x: auto`.
- `mockup/android.css` line 268 `.a-chips` — kind filter chips, `overflow-x: auto`.
- `mockup/android.css` line 395 `.a-steps` — organizer step strip, `overflow-x: auto`.
- Used by: Files screen (`android-screens.js` 179-181), Search (254),
  Organizer `steps()` (288-290).

### Verified overflow sites (native app)

- `android/app/.../ui/FilesScreen.kt` lines 78-93: two `Row`s of 5+3
  `FilterChip`s — no wrap, no scroll; clips on phones.
- `SearchScreen.kt` line 48: `Row` of two weighted `OutlinedTextField`s —
  fits, no change needed to layout (pattern harmonized anyway via filter
  sheet for consistency).

### Feature-parity gap (Windows mockup `screens.js`/`flows.js` vs `android-screens.js`)

| Windows mockup has | Android mockup today | Plan |
|---|---|---|
| Scope chips + sort `<select>` + clear-all + Rescan button | h-scroll scope chips, no sort, no rescan | folder sheet + filter sheet (kind/sort) + Rescan action |
| Table + inspector pane (decision, confidence, subject/project, evidence expander) | Thin detail sheet (location/size/kind/modified) | Sheet gains decision chip + conf bar + evidence list ("Why?") |
| Context menu w/ rename (validation), multi-select bar w/ 6 actions | Selection sheet w/ 3 actions | Add rename (Android filename rules), move/copy truthful toasts |
| Search: q + scope + kind + size filter groups | q + kind chips only | Filter sheet: kind + size + scope |
| Dashboard: storage byKind, devices, quick actions, **active-op card w/ live progress**, cleanup card, recent | hero, storage, organize/inbox cards, quick chips, recent | Add active-op card (bus-driven), cleanup card, devices card |
| Activity: active-op card, undo that mutates store, retry, canonical legend | inbox+ops sheets, toast-only undo | Real undo record (RolledBack), retry toast, legend section |
| Tools: 7 tool cards + **rules list w/ toggles** + storage-analysis demo | 7 tool rows, no rules | Add rules section w/ toggles + storage demo card |
| Settings: default folder, density, conflict, mode, watched folders, notifications, advanced (db/schema/export/reset) | theme seg, mode, conflict, SAF, AI, catalog | Add default folder, notifications, advanced/reset, grant rows |
| Organizer: dry-run checkbox, cancel during execute, undo record | explainer→scope→plan→run→result; no dry-run, no cancel | Add dry-run toggle, cancel (AbortController), preserve-tree toggle |

Platform-appropriate differences kept (NOT cloned): Android uses bottom nav,
bottom sheets, long-press contextual bar, SAF grant explainer, FAB, system
Back; Windows keeps table+inspector, context menus, breadcrumbs, Ctrl+K.

### Helpers verified to exist

`A.sheet` (modal bottom sheet + back-stack), `A.row`, `A.sel` (selection),
`FM.engine.plan(analyzed, dry, dest, srcRoot, preserveTree)`, `FM.engine.safety`,
`FM.engine.execute(plan, onStep, abortSignal)`, `FM.confirm`, `FM.dialog`,
`FM.statusChip`, `FM.confBar`, `FM.empty`, `FM.bus` ("ops" event),
`FM.store.{ops,inbox,settings,files,rules,devices,activity,activeOp}`,
`FM.menu` (anchored dropdown — available for scope picker alternative),
icons incl. `filter`, `sort`, `undo`, `retry`, `shield`.

## Findings

- Horizontal-scroll surfaces: `BROKEN` UX on phone (3 sites mockup + 1 site native).
- Android mockup feature surface: `INCOMPLETE` vs Windows mockup (10 gaps above).
- Native FilesScreen filter row: `BROKEN` on narrow widths.
- All required engine/data hooks: `EXISTING` — no mock-data or app.js engine
  changes needed; `preserveTree`/`dryRun`/abort already supported by `FM.engine`.

## Global Constraints

- Mockup stays dependency-free HTML/CSS/vanilla JS, runs over `file://`.
- Zero `overflow-x` anywhere inside the phone shell (CSS + screens).
- Every async surface keeps `idle|loading|success|empty|warning|error`;
  statuses icon+label, never color alone.
- `Planned`/`Host:` badges stay truthful — parity means same features, not
  faking unshipped backends.
- ≥48dp touch targets, M3 sheet/scrim patterns already established.
- Native app: no new dependencies; use existing `ModalBottomSheet`.
- MOCKUP_RULES: mockup must stay visually equivalent to shipped UI — mockup
  filter-sheet pattern mirrors the native filter-sheet fix in the same plan.
- No changes to Windows screens `screens.js`/`index.html` (feature-complete
  already) except none; `styles.css` untouched unless a shared class must be
  namespaced safely (prefer `.a-` prefixes).

## Proposed Changes / Implementation Phases

### Milestone 0 — Megaplan commit

- Write `docs/plans/PLAN-022-MEGAPLAN.md` (this file); commit + push.

### Milestone 1 — Mockup: kill horizontal scroll

Exit gate: grep finds no `overflow-x` in `android.css`; no h-scrolling in the
phone at any width ≥320px.

- 1.1 New shared picker: `A.picker({ title, items, selected })` → `A.sheet`
  with selectable list rows (radio-style checks) — reused by scope picker,
  kind/sort/size pickers, default-folder picker.
- 1.2 Files screen: replace `.a-scope` strip with a scope button
  `Downloads ▾` (`.a-pick`) opening the picker; replace `.a-chips` with a
  control row: search field + `Filters` icon-button showing active-count
  badge → filter sheet (kind radio list, sort radio list, Clear-all action).
- 1.3 Search screen: same control row; filter sheet holds kind + size +
  scope radios.
- 1.4 Organizer `steps()`: replace h-scroll strip with compact header —
  `Step N of 5 — <name>` + `.a-progress` linear bar (5 segments).
- 1.5 CSS: delete `overflow-x` from `.a-scope/.a-chips/.a-steps` (classes
  kept for compat or removed with usages); add `.a-pick`, `.a-filterbtn`,
  `.a-badge-count` reuse, `.a-progress`, `.a-opt` (sheet option rows),
  `.a-radio` styles.

### Milestone 2 — Mockup: feature parity (Android-idiomatic)

Exit gate: every Windows-mockup feature row in the gap table has an Android
equivalent reachable without horizontal scroll.

- 2.1 Files: filter sheet gains Sort (name/kind/size/modified) + Clear all;
  header Rescan action (appbar `retry` icon or list header button); file
  detail sheet gains decision chip + confidence bar + subject/project +
  evidence expander (`<details>` styled `.a-ev`); selection sheet gains
  Rename (single item, Android-invalid-char validation `[]:*?"<>|`) +
  Move/Copy truthful demo toasts.
- 2.2 Dashboard: add Active-operations card (renders `FM.store.activeOp`
  progress; hidden when none), Devices mini-card, Cleanup opportunities
  card (dupes/big/leftovers counts → Tools link).
- 2.3 Activity: active-op card at top; real undo — builds `RolledBack`
  record + activity entry + toast (mirror flows.js undo, not a fake toast);
  `Retry failed` on NeedsAttention; canonical operation-states legend.
- 2.4 Tools: "Organizer rules" section — rule rows w/ enable toggles
  (reuses `.a-check` style or a switch) + provenance/priority badges +
  human-readable condition→action (port `humanCond`/`humanAct`); storage
  analysis card (`FM_DATA.storage.byKind` meters, truthful "demo" note).
- 2.5 Settings: General — default folder picker (`.a-pick` → picker sheet);
  Notifications — 3 switch rows bound to `s.notifications`; Advanced —
  db path mono, schema version, AI-provider note, Export profile
  (Planned-badge, disabled), Reset demo data (confirm → `FM.initStore`);
  Folder access row → grant list sheet w/ per-tree rows + revoke mock +
  "Manage Access" action.
- 2.6 Organizer: dry-run `Switch` row on plan step ("Preview only — nothing
  moves" → plan rebuilt `dryRun=true`, execute step renders dry-run result
  + "Run for real"); Cancel button during run (`AbortController`, same
  pattern as `flows.js` vExecute); preserve-tree toggle row on plan step.
- 2.7 Devices: grant-coverage row on the Android device card (SAF trees
  granted vs total) — truthful mock.

### Milestone 3 — Native app: phone-appropriate filters

Exit gate: `assembleDebug` green; no chip-row overflow at 360dp.

- 3.1 `FilesScreen.kt`: drop the two `FilterChip` `Row`s → search field +
  `OutlinedButton` "Filters" (leading filter icon, trailing active-count
  badge) → `ModalBottomSheet` with kind radio list + sort radio list +
  Clear. Filtering logic unchanged (query/kind/sort state).
- 3.2 `SearchScreen.kt`: replace ext/kind `Row` fields with the same
  Filters button + sheet (kind radio list; keep extension text field inside
  the sheet) for pattern consistency.
- 3.3 Audit other screens for implicit h-overflow (long `Row`s of
  `FilterChip`/`AssistChip`): wrap-safe via `FlowRow` where a chip group is
  legitimately short (≤3) — no overflow risk found elsewhere in audit; record.

### Milestone 4 — QA + docs

- 4.1 Mockup verification: grep `overflow-x`/`overflow-x:auto` → zero in
  `android.css`; serve `mockup/` on localhost, open `android.html` in the
  browser preview at 360×740 and desktop widths; verify every screen
  renders, sheets open/close via Back, both themes, zero console errors.
- 4.2 `./gradlew assembleDebug test` green; Compose UI audit at 360dp.
- 4.3 `FEATURES.md` F-UI1/F-UI4 notes updated; `CHANGELOG.md` entry;
  `mockup/README.md` parity note; plan status → IMPLEMENTED after evidence.
- 4.4 Commits per feature area + immediate `git push origin HEAD:main`.

## Non-Goals

- No Windows-mockup feature work (already complete); no `screens.js` changes.
- No new backend capability (mock engine untouched; truthful Planned badges
  for unshipped Android features remain).
- No PLAN-019 contract work; no emulator-verification work for PLAN-021
  (runs in parallel — system image download already in progress).
- No density toggle on Android (comfortable-only per spec).

## Files Expected to Change

`mockup/android-screens.js` (major), `mockup/android.css` (major),
`mockup/android.html` (minor — overflow/menu anchors if needed),
`mockup/README.md` (parity note),
`android/app/src/main/kotlin/com/filemind/app/ui/FilesScreen.kt`,
`android/app/src/main/kotlin/com/filemind/app/ui/SearchScreen.kt`,
`docs/features/FEATURES.md`, `docs/development/BUILD.md` (if verify commands
change — unlikely), `CHANGELOG.md`, this plan's status.

## New Files Required

`docs/plans/PLAN-022-MEGAPLAN.md` (this file). No new source files —
`A.picker` lives in `android-screens.js`; the sheet styles in `android.css`.

## Error Handling / Edge Cases

- Filter sheet with zero kinds (empty scope) → empty-state row in sheet.
- Sheet open + route change → `A.clearBack()` already resets (existing).
- Selection active + filter change → existing `A.sel.exit()` calls kept.
- Rename validation on Android: reject `[]:*?"<>|` + empty (Android filenames
  have no extension separator requirement; trailing dot/space tolerated but
  flagged as hint text).
- Undo of an already-undone op → `canUndo` guard mirrors flows.js
  (`undoOfOperationId` check).
- 320px min width: sheets are width:100% of phone; pickers single-column.

## QA Plan

- `grep -n "overflow-x" mockup/android.css` → zero matches (table/term none
  exist in android.css).
- Browser preview: every bottom-nav route + organizer flow + settings +
  help; filter sheet open/apply/clear; both themes; Escape=Back parity.
- `./gradlew :app:compileDebugKotlin assembleDebug test` green.
- Docs truth pass: FEATURES/CHANGELOG/plan checkboxes match evidence.

## Acceptance Criteria

- [ ] Zero horizontal scrolling inside the phone shell (all routes, 320–480px)
- [ ] Filters via M3 bottom sheet w/ active-count badge on Files + Search
- [ ] Scope via picker sheet; organizer steps via progress indicator
- [ ] Full feature parity with Windows mockup (gap-table rows all present)
- [ ] Native Files/Search use the same filter-sheet pattern; build green
- [ ] Platform idioms preserved (sheets/FAB/selection bar/Back vs
      table/inspector/context-menu/breadcrumbs) — documented, not cloned
- [ ] `Planned`/host badges truthful; no fake functionality
- [ ] Docs + mockup README updated; commits pushed per AGENTS sync rule

## Risks / Regression Areas

- Sheet-in-sheet stacking (filter sheet over detail sheet) — `A.sheet`
  supports multiple; verify Back unwinds in order.
- `FM.route` re-render loses open sheets (acceptable — matches real app).
- Rework is JS-heavy; keep `A.*` helpers small and reuse existing classes.
- Native `ModalBottomSheet` is `@OptIn ExperimentalMaterial3Api` — already
  opted in on FilesScreen.

## Commit Plan

`docs(plans): PLAN-022` → `feat(mockup): android filter sheets, no h-scroll`
→ `feat(mockup): android feature parity surfaces` → `feat(android-app):
filter bottom sheets (parity)` → `docs(*): statuses` — each pushed
immediately (`git push origin HEAD:main`).

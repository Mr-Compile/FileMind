# PLAN-025 — Android: mockup UI parity + organizer functionality + SAF Download fix

Status: IMPLEMENTED (build + unit tests green; device/runtime QA pending)
Related Feature: F-UI1 (mockup), F-UI4 (Android app), F-P8 (Android backend)
Created: 2026-10-26
Updated: 2026-10-26

> Implementation status (2026-10-26): all milestones M1–M9 implemented and
> compiled — `:app:compileDebugKotlin` BUILD SUCCESSFUL; `gradlew test`
> green (9/9 parity tests). Device-level verification (real SAF grant →
> scan → plan → execute → undo, Download-subfolder picker flow) remains
> open — see M10.

## Request / Goal

User (2026-10-26), four reports:

1. "The UI of the app run vs the mockup HTML Android UI is not matching —
   fix this."
2. "The file organizer when I've run the app is not functional — fix this."
3. "Fix the issue of 'to protect your privacy' — I can't select the
   Download folder in the phone."
4. "The mockup I see has complete UI but the app when run is not the same…
   fix this to match the live to mockup html UI."

**Goal: the running app must match `mockup/android.html` screen-for-screen,
element-for-element.** The mockup is the canonical visual reference of the
approved UI (`docs/rules/MOCKUP_RULES.md`: "the shipped UI and the mockup
must remain visually equivalent; differences must be resolved, not silently
accepted"). It exists so the real UI can be checked against approved intent —
right now that check fails on every screen.

Why the drift happened: `android/app` shipped in PLAN-021 (2026-10-24);
PLAN-022 then rebuilt the mockup shell to full feature parity via Android
idioms (picker sheets, filter sheets, selection bar, FAB, stepper, rule
toggles) and PLAN-023/024 polished it further. The app only received the
filter-sheet/back pieces of those follow-ups — the rest was never ported.

Scope: `android/app` (Compose UI + ViewModels) plus minimal, justified
repairs in `android/platform` service wiring. Parity means **mockup → app**:
the mockup already shows the target UI, so all changes are on the app side.
Where the app is *more* truthful than the mockup (real SAF grants vs
fictional peer devices; full Help sub-pages vs sheets) the app keeps the
stronger version — recorded here, not silently divergent.

## Repository Audit

### Verified Existing State — Functional bugs

**B0. Confidence thresholds are unreachable — every file decides `NoOp`
(primary root cause of "all of it is dead").**
`DecisionMaker.decide` (`core/.../DecisionMaker.kt:57-67`) sends
`ConfidenceBand.Low` (< `mediumThreshold`) files to `DecisionKind.NoOp` —
"left untouched". `ConfidencePolicy` (`ConfidenceEngine.kt:6-22`) sets
`mediumThreshold = 70`, `highThreshold = 95`, with per-kind caps:
extension-signal 10, mime-signal 8, subject-keyword 15, project-keyword 12,
context-over-type 20. On Android, `FileMindAndroid.pipeline()` wires
**no `enricher` and no `advisor`**, so a typical file scores at most 18;
even a full subject+project+context match reaches only ~65.
Result: with no user rules, every file on the device is `Low` → `NoOp` →
the Review step shows "0 actionable" → "Preview plan" stays disabled →
the organizer dead-ends after scanning. Matches the user report exactly:
the scan runs, then everything downstream is inert.

**B0-fix (proposed):** pass an Android-tuned `ConfidencePolicy` in
`FileMindAndroid.pipeline()` — kind-identified files (ext+mime ≈ 18)
reach High → `Organize` via `Files/{FileKind}`; weak-signal files reach
Medium → `Review` → Smart Inbox (with B3 write-back); zero-signal files
stay `NoOp`. Single-constructor repair, no engine changes; values
finalized at implementation with `decisionChip` chips surfacing bands
honestly.

**B1. Organizer plans have no destination root (root cause of
"not functional").**
`OrganizerViewModel.buildPlan()` (`android/app/.../vm/ViewModels.kt:389`)
calls `pipeline.plan(analyzed, dryRun = false, sourceRoot = …)` —
`destinationRoot` is never passed.
- `OrganizerPipeline.plan` (`core/.../OrganizerPipeline.kt:146`):
  `destBase = null` when `destinationRoot == null`.
- `ActionPlanner.plan` (`core/.../ActionPlanner.kt:58-74`) then resolves
  `destination` to the bare template output (e.g. `Files/Document`) and
  `SafPathNormalizer.combine` classifies it via `AndroidUris.classify`
  (no `://` → `UriKind.AppFile`) producing **relative** `file://Files/…`
  destinations.
- `AndroidFileSystemAdapter.move` → `AndroidCapabilityModel
  .canMoveBetween(SafDocument, AppFile)` → `false` → every item fails
  `UnsupportedOperation` → record lands `NeedsAttention` with all items
  failed. For the "App storage" source the relative `file://` destination
  also cannot be created under the process CWD.
- The mockup plans with a real root: `FM.engine.plan(analyzed, dry,
  root + "\\organized", root, tree)` (`android-screens.js:569`).

**B2. File index is never populated.**
`FileMindAndroid.pipeline()` (`platform/.../FileMindAndroid.kt:83-113`)
passes no `indexing=` — `OrganizerPipeline.index` returns a stub
`IndexResult(0,0)` (`OrganizerPipeline.kt:203`).
`OrganizerViewModel.startScan` calls `pipeline.index(candidates)` at
line 368 — a verified no-op. Consequence: `file_index` stays empty →
Files/Search lists and Home "Indexed files" never show anything after a
successful organizer run. `RoomFileIndexRepository`
(`platform/.../db/RoomRepositories.kt:105-127`) already implements the
`FileIndexRepository` port (`upsert`/`getAll`); only the
`IndexingService` adapter (interface at `OrganizerPipeline.kt:41`) is
missing. `ScanWorker` indexes per-file correctly for background scans —
the interactive flow does not.

**B3. Review decisions never reach the Smart Inbox (interactive flow).**
`Ask`/`Review` plan items execute as `Result.ok(source)` no-ops
(`TransactionEngine.kt:117`) and are recorded Committed. `ScanWorker`
(`platform/.../work/ScanWorker.kt:58-67`) persists `SmartInboxItem`s for
`DecisionKind.Review`; `OrganizerViewModel.startScan` does not — the
inbox stays empty even when the classifier flags files.

**B4. SAF picker can't select Download ("protect your privacy").**
`OrganizerScreen` launches `OpenDocumentTree` with `treePicker.launch(null)`
(`OrganizerScreen.kt:72-80`). On Android 11+ the picker refuses the
internal-storage root and the `Download` folder root — the documented
platform restriction. Subfolders inside Download remain selectable, and
`ActivityResultContracts.OpenDocumentTree` accepts an initial URI — the
picker can be landed inside `Download` so the user creates/picks
`Download/<subfolder>`. The `PermissionExplainer` copy does not explain
this restriction.

**B5. Cancelled runs leave zombie "active" operations.**
`vm.execute` cancels the coroutine mid-`TransactionEngine.execute`
(`ensureActive` at `TransactionEngine.kt:60` throws); the
`OperationRecord` is persisted with `Executing`/`Validating` status and
never reaches a terminal state → `operationRepository.getIncomplete()`
returns it forever → Home "Active operations" and Activity "Active"
show ghost runs permanently. Fix: on cancel (and on the
CancellationException path), load the record and persist a
`NeedsAttention`/`Failed` terminal state with a "cancelled" error.

**B6. Background worker path is dormant.**
`FileMindWorkScheduler.enqueueScan`/`enqueueOrganize`, `ScanWorker`,
`OrganizeWorker` exist but have **no call sites** — nothing ever
schedules them. Interactive `OrganizerViewModel` is the only entry.
Consistent with "Watch folders: Planned" — stays dormant in this plan,
recorded here so the dead path is explicit, not assumed live.

**B7. `inferScope` never matches SAF URIs.**
`FileScanner.inferScope` (`FileScanner.kt:40-49`) tests for
`\Downloads`/`\Documents` on the raw URI string where the docId is
percent-encoded (`content://…/tree/primary%3ADownload%2F…`) → every SAF
scan is `SourceScope.Other`. Fix by decoding docId/relPath before
matching.

**B8. `grantStore.take` result is ignored.**
`onTreePicked` proceeds to `startScan` even when `take()` returns false
(non-persistable provider) → later writes fail `PermissionDenied`.
Surface the failure up front instead of scanning first.

**B9. Index-backed screens never refresh after organizing.**
`FilesViewModel`/`SearchViewModel`/`HomeViewModel` load once in `init`;
`restoreState` in the nav click handler preserves the back-stack entries
(and their VMs) → after an organize run, Files/Search/Home stay stale
until the process restarts. Add refresh-on-resume.

**B10. Execute shows indeterminate bar — no live progress/log.**
`vm.execute` awaits `pipeline.execute(plan)` (`ViewModels.kt:402-424`);
`TransactionEngine` persists the record per item with `Executing` status
(`TransactionEngine.kt:56-73`), so `operationRepository.getIncomplete()`
already exposes live item states — the UI just never reads them. Mockup
run page shows stage chip + progress + per-item log + cancel.

### Verified Existing State — UI parity gaps (app vs `android-screens.js`)

Element-level checklist — QA verifies each mockup element has a live
Compose counterpart (the "→" column is what the app must render):

| Screen | Mockup element → live equivalent required | App today | Class |
|---|---|---|---|
| Shell | appbar: title + Search + Op-center(badge) + Theme icons → TopAppBar actions | title only (`AppShell.kt:102`) | MISSING |
| Shell | bottom nav Home/Files/Search/Activity/Settings | exists | EXISTING |
| Home | hero ("Your files, sorted themselves." + sub) → headline + supporting text | none | MISSING |
| Home | This-device card: icon + status chip + meter + "used of total · Folder access n of m" | plain card, no chip/grants | INCOMPLETE |
| Home | active-op "Running now" card w/ pct bar | list rows only | INCOMPLETE |
| Home | Organize card + Smart Inbox card w/ badge + Devices card + Cleanup opportunities card (dupes/>3GB/leftovers) | 2-up quick-action cards only | INCOMPLETE |
| Home | quick chips (Search/Tools/Devices/Help) + recent activity rows + Organize FAB | recent cards; no chips/FAB | MISSING |
| Files | one control row: scope picker + searchbar + rescan + icon-only filter w/ corner badge | textfield + labeled Filters; no picker/rescan | INCOMPLETE |
| Files | "N item(s) · sorted by …" line | filter-summary text only | INCOMPLETE |
| Files | rows: file-kind icon + title + `kind · size · date` + chevron | card + Info icon | INCOMPLETE |
| Files | inspector sheet: location/size/kind/modified + Why block + actions (Organize this file / Rename / Select) | read-only details sheet | INCOMPLETE |
| Files | long-press → contextual bar (count + ⋯ → actions sheet: Organize/Rename/Move-copy/Send to Inbox/Delete/Clear) | count + close only | INCOMPLETE |
| Files | Organize FAB | none | MISSING |
| Search | lg searchbar + icon filter, one row | textfield + labeled Filters | INCOMPLETE |
| Search | "N result(s) / All indexed files" header; scope + kind + size filter groups; result tap → sheet w/ Why + "Open in Files" | kind+ext only; tap = no-op | INCOMPLETE |
| Organizer | `Step N of 5 — name` progress header (Access/Folder/Analyze/Plan/Run) | none | MISSING |
| Organizer | Access page: explainer card w/ 3 bullet rows + "Choose a folder to grant" + "Not now" | modal sheet | INCOMPLETE |
| Organizer | Folder page: scope rows `name · path · N file(s) · grant state` + chip | cards + plain grant list | INCOMPLETE |
| Organizer | Plan: summary card + Preview-only switch + Keep-tree switch + safety ok/warn/err card + moves list w/ conf bars + Ask rows | plan items list; no toggles/conf bars | INCOMPLETE |
| Organizer | Run: stage chip + determinate bar + count + live item log + Cancel; dry-run → "Run for real"/"Start over" | indeterminate bar + cancel | INCOMPLETE |
| Organizer | Result: status card + moved count + "Undo this run" + Done | minimal result | INCOMPLETE |
| Activity | active-op card; inbox row → sheet (reasons + Accept/Dismiss); op row → detail sheet (items + Undo/Retry); status legend | sections + "Mark resolved" + inline Undo | INCOMPLETE |
| Tools | tool rows w/ status chips; rule rows w/ working Switch; "Where the space went" storage card | read-only rules; no storage card | INCOMPLETE |
| Settings | General (default folder) · Appearance (seg) · Organization (mode + conflict policy + history) · Notifications (3 switches) · Privacy & access (SAF grants w/ revoke, AI chip, catalog) · Advanced · About | org-mode seg + theme seg + permissions + help link | INCOMPLETE |
| Devices | storage card + device rows + detail sheet | real grants + capabilities — app *more* truthful | EXISTING |
| Help | 9 topic rows → sheets | 9 real sub-screens — superset | EXISTING |

### Verified Existing State — Token / shell-level divergences

Spacing (`--sp-*` = `Spacing` 4-48) and radii (`--r-*` = `Radius` 6/10/14/18)
match exactly; semantic colors match in light. Divergences found:

**T1. Dark-scheme brand drift (INCONSISTENT).** Mockup uses
`--fm-primary:#2563EB` in **both** themes (colors.css lines 2,9-10 only
remap surface/bg/text/border). Native `DarkScheme` substitutes
`#60A5FA/#818CF8/#A78BFA` (`Theme.kt:69-71`) — every accent element
(FAB, chips, progress, nav indicator, links) renders a different hue in
dark vs. the approved mockup. Fix: `DarkScheme` uses
`BrandColors.Primary/Secondary/Accent` verbatim.

**T2. Card-per-row vs flat rows (INCONSISTENT).** Mockup lists use
`.a-row` — transparent flat rows w/ hover/selected tint, 64px min —
while Files/Search/Activity/Inbox screens wrap every row in a `Card`
(border + elevation). Material idiom + mockup agree: use flat rows
(`Surface`/`Row`) for list items; cards only for section blocks.

**T3. Section headers (INCONSISTENT).** Mockup `.a-sec` = 12sp uppercase
bold letterspaced secondary label; native `SectionHeader` = titleMedium
SemiBold sentence-case. Restyle `SectionHeader` to the label treatment.

**T4. Search field (INCONSISTENT).** Mockup `.a-searchbar` = 48dp
(56dp lg) pill — elevated fill, leading icon, no outline. Native uses
default `OutlinedTextField` (rect + outline + floating label). Add a
shared pill `SearchField` composable.

**T5. No snackbar host (MISSING).** Every mockup action reports via
`FM.toast` ("Access granted", "Rescanning…", "Rule disabled"); the app
has zero transient feedback channel. Add `SnackbarHost` in `AppShell` +
shared `showSnackbar` for action results.

**T6. Card borders (INCONSISTENT, minor).** Mockup `.a-card` =
flat surface + 1px border, no shadow; native `Card` = elevation, no
border. Switch feature cards to `CardDefaults.outlinedCardBorder` +
0dp elevation to match the bordered-flat look.

**T7. Confidence bars (MISSING).** Mockup `FM.confBar` renders
bar+percent on plan rows and in the Why block; native shows confidence
only inside `WhySheet`. Add shared `ConfidenceBar` composable.

**T8. Fake status/gesture bars — equivalent, keep.** Mockup `.statusbar`/
`.sysbar` simulate system bars; the app has real ones via
`enableEdgeToEdge` — no work needed.

### Existing Rules Found

- `MOCKUP_RULES.md` — shipped UI and mockup must stay visually
  equivalent; differences resolved, not accepted.
- `SHARED_UX_SYSTEM.md` — lifecycle Analyze → Preview → Confirm →
  Execute → Result → Undo/View Activity; empty states offer next action;
  Why? explainability.
- `ANDROID_UX.md` — explain storage permissions, direct Manage Access
  flow, system Back, platform-appropriate background tasks.
- `AGENTS.md` — plan approval before implementation; repair before
  replace; per-feature commits + immediate `git push origin HEAD:main`.

### Documentation vs Code Differences

- `FEATURES.md` F-UI4 claims "index-backed list" for Files — the index
  is never populated (B2) so the claim is aspirational today. PLAN-021
  listed emulator verification as open; this plan delivers it.

## Root Cause / Problem

Four layered functional defects make the shipped organizer inert:

1. **Unreachable confidence policy** — thresholds (70/95) vs. a max
   achievable score of ~18-65 without enricher/advisor → every file is
   `Low` → `NoOp` → zero actionable items → dead-end Review step.
2. **Missing destination root** — every move targets an unwritable
   relative `file://` URI → `UnsupportedOperation` for the whole plan.
3. **Unwired services** — `IndexingService` and Smart-Inbox write-back
   absent → Files/Search/Inbox permanently empty.
4. **SAF picker placement** — opens where Android forbids Download-root
   selection → "protect your privacy" dead-end.

On the UI side, the Compose screens predate PLAN-022/023/024 mockup
parity work and never reached visual parity beyond the nav shell.

## Proposed Changes

### M1 — Organizer functional repairs (core bug fixes)

- `FileMindAndroid.pipeline()`: construct `ConfidenceEngine` with an
  Android-tuned `ConfidencePolicy` so kind-identified files reach an
  actionable band (B0). Exact thresholds chosen so ext+mime-only files
  → High (`Organize`), mime/name-only → Medium (`Review`→inbox), empty
  evidence → `NoOp`; verified by a core unit test in M10.
- `ViewModels.kt` `buildPlan`: pass
  `destinationRoot = app.backend.paths.combine(sourceRoot, "organized")`
  and `preserveTree`/`dryRun` from new `OrgState` flags (mockup parity:
  `<source>/organized/<template>`). `combine` on a SAF tree emits a
  `document/` URI under the granted tree → `canMoveBetween` same-authority
  → `moveSaf` works; AppFile sources get `file://…/organized`.
- New `RoomIndexingService` (platform/db) implementing
  `OrganizerPipeline.IndexingService`: upsert each candidate with
  `FileIdentity(stableId = scanId, pathHint = sourceUri, fingerprint =
  compute(size, modifiedAt))`; return real `IndexResult`. Wire
  `indexing = RoomIndexingService(fileIndex)` in `FileMindAndroid
  .pipeline()`.
- `startScan`: after `analyze`, persist `SmartInboxItem` for each
  `DecisionKind.Review` with `id = candidate.scanId` (stable digest →
  re-scans upsert instead of duplicating).
- `OrganizerScreen`: `treePicker.launch(initialUri)` where
  `initialUri = DocumentsContract.buildDocumentUri(
  "com.android.externalstorage.documents", "primary:Download")` —
  the picker opens inside Download; user picks/creates a subfolder.
  Update `PermissionExplainer` copy: Android forbids granting the
  Download/storage root; pick or create a subfolder such as
  `Download/FileMind`.
- `vm.execute`: launch a poller collecting
  `operationRepository.getIncomplete()` every ~250 ms during
  `pipeline.execute` → `OrgStep.Executing` exposes live
  committed/total + last item names for the run-page log. On
  `runJob.cancel()`/CancellationException, load the in-flight record and
  persist it `NeedsAttention` with a cancelled error (B5).
- `onTreePicked`: honor `grantStore.take`'s Boolean — persist failure
  surfaces a truthful error instead of a scan that can only fail on
  write (B8).
- `FileScanner.inferScope`: match on decoded docId/`relPath` so SAF
  trees classify into Downloads/Documents/Media scopes (B7).
- `Files/Search/Home` ViewModels: refresh-on-resume (`ON_RESUME` or
  `LaunchedEffect` reload) so post-organize state is visible (B9).

### M2 — App bar + shared token repairs

- `AppShell.kt` TopAppBar actions: Search icon → `Routes.Search`;
  Activity icon w/ `BadgedBox` (open-inbox count via lightweight state)
  → `Routes.Activity`; Theme icon → cycle System→Light→Dark through
  `settingsViewModel`. Count loaded in `LaunchedEffect(route)` from
  `app.backend.smartInbox.getOpen()`.
- `Theme.kt`: `DarkScheme` uses `BrandColors.Primary/Secondary/Accent`
  verbatim (T1).
- `Components.kt`: restyle `SectionHeader` to the uppercase label
  treatment (T3); new `SearchField` pill composable (T4); new
  `ConfidenceBar` (T7); shared `OrganizeFab`; flat `ListRow` pattern
  replacing card-per-row (T2); outlined-card defaults where the mockup
  uses bordered cards (T6).
- `AppShell.kt`: `SnackbarHost` + shared `showSnackbar` for action
  feedback across screens (T5).

### M3 — Home parity

Rebuild `HomeScreen` to mockup structure: hero headline + subtitle;
"This device" card (icon + status chip + meter + `X used of Y · Folder
access n of m`); active-op card when running; Organize card; Smart
Inbox card w/ count badge; Devices card (this-device row, real chip);
Cleanup opportunities card (size-grouped dupes, >3 GB, excluded
leftovers — computed from `fileIndex.getAll()`); quick-action chips
(Search/Tools/Devices/Help); Recent activity rows; extended FAB
"Organize" → `Routes.Organizer`.

### M4 — Files parity

- Control row: `[scope pick][search][rescan][filter icon w/ badge]` —
  scope = granted trees (`grantStore.grants()` → label via shortPath) +
  "App storage" + "All indexed"; filtering via
  `GrantCoverage.isCoveredByTreeUris` for SAF + `startsWith` for
  `file://`. Rescan = pipeline.scan + index for the scope.
- Row-style list: kind icon, name, `kind · size · date` sub, chevron;
  `combinedClickable` long-press → contextual bar (count + close +
  overflow → actions sheet: Organize selected / Rename (n=1) / Send to
  Smart Inbox / Move-copy `Planned` / Delete — truthful "not by design"
  / Clear).
- Tap → inspector sheet: location/size/kind/modified + Why block
  (reuse `WhySheet` content) + actions (Organize this file → Organizer;
  Rename → validated input sheet → `fs.rename`; Select).
- Extended FAB "Organize".

### M5 — Search parity

- Large searchbar + icon filter button one row; results header
  "N result(s) / All indexed files"; filter groups Folder + Kind + Size
  (mockup parity; ext field kept inside sheet); tap result → sheet w/
  location/size/modified + Why + "Open in Files" action.

### M6 — Organizer flow parity

- `Step N of 5 — name` progress header (Access/Folder/Analyze/Plan/Run).
- Step 1 Access: explainer *page* (3 bullet rows + "Choose a folder to
  grant" + "Not now") — replaces modal sheet.
- Step 2 Folder: scope rows — granted trees + App storage — with
  `path · N file(s) · grant state` + chip.
- Analyze: scanning progress; then **straight to Plan** (drop the
  separate Review screen — its per-file Why moves onto plan-item tap).
- Step 4 Plan: summary card (N actions, M moving, K to inbox, skipped,
  dry-run state); "Preview only" + "Keep folder structure" switches
  rebuilding the plan; safety card (ok/warn/err variants); moves list
  rows w/ conf bar (Ask rows → warning chip); item tap → `WhySheet`;
  CTAs "Run preview" / "Run this plan" + "Pick another folder";
  `ConfirmSheet` retained as the approval gate (shared lifecycle:
  Preview → Confirm → Execute).
- Step 5 Run: stage chip + determinate bar + `done/total` + last-N item
  log + Cancel; dry run → "Dry run complete" + "Run for real" /
  "Start over"; result → status card + moved count + "Undo this run"
  + Done → Activity.

### M7 — Activity parity

- Active-op card (Running now + live pct); Smart Inbox rows → detail
  sheet (reason + classification Why + "Mark resolved" + "Organize this
  file"); operations rows → detail sheet (per-item status/result +
  Undo + "Retry failed" truthful-disabled); status legend strip.

### M8 — Tools parity

- Rule rows gain `Switch` → `rules.upsert(rule.copy(enabled = v))`.
- Tool list gains Smart Inbox + Undo history entries (nav routes) and
  truthful `Planned` chips where missing.
- "Where the space went" card: kind → bytes bars computed from
  `fileIndex.getAll()` (real data, not demo).

### M9 — Settings parity

Sections per mockup: General (default-folder picker → `ui.defaultScope`
setting consumed by Files), Appearance (existing segmented), Organization
(mode seg — moved here — + conflict-policy picker → `organize.conflictPolicy`
+ history row → Activity), Notifications (3 pref switches → settings
keys `notify.operationDone|warnings|smartInbox`), Privacy & access
(folder-access row → Devices; AI features `Planned` chip; local catalog
row), Advanced (db row; export profile `Planned` chip), About (Help row
+ app row).

### M10 — QA + docs + commits

- `gradlew :app:compileDebugKotlin` green; `:core:test` +
  `:platform:testDebugUnitTest` green; assemble on-device/emulator
  walk-through where possible.
- `FEATURES.md` F-UI4 status text; `CHANGELOG.md`; this plan →
  IMPLEMENTED with evidence.
- Commits per milestone pushed to `origin/main` per AGENTS.md.

## Non-Goals

- No mockup changes — `android.html`/screens already render the target
  UI; the app is being raised to it.
- No `MANAGE_EXTERNAL_STORAGE` / "All files access" request — FileMind's
  privacy posture stays SAF-scoped (the Download-*root* restriction is
  a platform rule; subfolders are the sanctioned path).
- No MediaStore-backed Downloads source (deferred — separate plan).
- No cross-plan core engine changes (planner/safety/transaction logic
  stays untouched — fixes are wiring + UI).
- Windows app/mockup untouched.

## Files Expected to Change

- `android/app/.../vm/ViewModels.kt` — OrgState flags, destinationRoot,
  inbox write-back, progress polling, scope/selection/search state,
  rule toggle, storage aggregation.
- `android/app/.../nav/AppShell.kt` — appbar actions + badge.
- `android/app/.../ui/{HomeScreen,FilesScreen,SearchScreen,
  OrganizerScreen,ActivityScreen,SettingsScreen,ToolsScreen}.kt` —
  parity rebuilds.
- `android/app/.../components/Components.kt` / `Sheets.kt` — picker
  sheet, icon-only filter button, conf bar, steps header, inspector
  sheet actions, selection-actions sheet (new shared composables only
  where reuse is real).
- `android/platform/.../FileMindAndroid.kt` — `indexing =` wiring.
- New: `android/platform/.../db/RoomIndexingService.kt`.
- `docs/plans/PLAN-025-*.md`, `docs/features/FEATURES.md`,
  `CHANGELOG.md`.

## Architecture Impact

None — no new modules; one small adapter class implementing an
EXISTING port; all mutations still flow through `OrganizerPipeline`.

## Data / Storage Impact

`file_index` begins populating (its schema already exists). Smart-inbox
rows keyed by stable `scanId` → idempotent re-scans. Two new
`settings` keys (default scope, conflict policy) + three notify prefs —
plain key/value rows, no migration.

## Routing / Flow Impact

Files: "Organize this file" and inspector "Select" reuse existing
routes/state only — no new nav graph entries.

## Security / Privacy Impact

Positive: the SAF explainer now truthfully states the Download-root
restriction instead of letting users fail against the system dialog.
No new permissions; no all-files access.

## Error Handling / Edge Cases

- SAF move inside granted tree: `ensureSafDirs` already creates missing
  `organized/…` segments; `KeepBoth` conflicts handled by planner policy.
- `preserveTree` on SAF: `relativeTo` works on docId paths — verified
  logic in `SafPathNormalizer`.
- Cancel mid-execute → `ensureActive` throws; record persists
  `NeedsAttention` with partial items — surfaced truthfully.
- Empty index everywhere → truthful empty states with next action
  (existing `EmptyState`).
- Download root still unselectable by platform — explainer routes users
  to subfolders; `initialUri` ignored gracefully on providers that
  don't honor it.

## QA Plan

- `gradlew :app:compileDebugKotlin --console=plain` → exit 0.
- `gradlew :core:test :platform:testDebugUnitTest` → existing suites green.
- Static parity sweep: every mockup screen section mapped to a Compose
  element (checklist against `android-screens.js`).
- Manual/emulator walk (user-verifiable): grant `Download/FileMind`,
  scan, plan preview (dry + tree toggles), execute → files actually
  move into `Download/FileMind/organized/…`; Files/Search populate;
  undo restores; inbox receives Review items.
- Record emulator-vs-static gaps honestly in Evidence.

## Acceptance Criteria

- [ ] Scanning a folder with recognizable files yields actionable
      decisions (`Organize`/`Review` count > 0) — no more "0 actionable"
      dead-end on ordinary content.
- [ ] Organize on a granted folder produces `Committed` moves into
      `<source>/organized/<template>/` — zero `UnsupportedOperation`
      failures.
- [ ] After a scan, Files/Search/Home show indexed files (index count
      > 0); Review-classified files appear in Smart Inbox.
- [ ] SAF picker opens inside Download; granting a subfolder succeeds;
      explainer copy names the restriction.
- [ ] **Mockup parity**: every row of the element-level checklist above
      has a live Compose counterpart — verified screen-by-screen against
      `mockup/android.html` in both themes; deviations only where the
      app is more truthful (Devices, Help) and recorded in Evidence.
- [ ] Organizer shows Step-N-of-5, dry-run + preserve-tree toggles,
      live run progress, undo; cancelling leaves a terminal
      `NeedsAttention` record, not a ghost "Active" row.
- [ ] Files/Search/Home show fresh data after an organize run
      (refresh-on-resume) — no restart needed.
- [ ] `:app:compileDebugKotlin` + unit tests green.

## Risks / Regression Areas

- SAF `moveDocument` quirks per provider (returns null on success on
  some) — existing verify path covers.
- Large indexes make in-memory Home/Search aggregations heavier —
  bounded queries; keep top-N slicing.
- Selection-actions breadth vs. real backend verbs — Planned-badged
  where the verb doesn't exist (truthfulness rule).

## Documentation Updates

`FEATURES.md` F-UI4 status → verified behavior; `CHANGELOG.md` entry;
plan → IMPLEMENTED with evidence. `mockup/` unchanged.

## Commit Plan

- `fix(android-app): organizer destination root + index/inbox wiring`
- `fix(android-app): SAF picker lands inside Download + honest explainer`
- `feat(android-app): appbar actions (search, activity badge, theme)`
- `feat(android-app): home dashboard parity w/ mockup`
- `feat(android-app): files scope picker, inspector + selection actions`
- `feat(android-app): search scope/kind/size filters + inspector`
- `feat(android-app): organizer stepper, dry/tree toggles, live run`
- `feat(android-app): activity sheets + legend`
- `feat(android-app): tools rule toggles + storage analysis`
- `feat(android-app): settings sections parity`
- `docs(plans): PLAN-025 implemented` (+ FEATURES/CHANGELOG)

## Approval

User approval required before implementation.

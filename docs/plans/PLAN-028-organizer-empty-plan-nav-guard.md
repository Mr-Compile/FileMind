# PLAN-028 — Organizer: "plan has no items" fix + leave-flow guard (Android + Windows parity)

Status: APPROVED (user, 2026-10-26) — UX choice: preserve state + guard only during Execute
Related Feature: F-UI4 (Android app), F-UI3/Windows app, F-P8 (Android backend), PLAN-025 (device QA follow-up)
Created: 2026-10-26
Updated: 2026-10-26

## Request / Goal

User reports (2026-10-26, on Infinix X6725B, Android 15, debug build of PLAN-025):

1. "The organizer is blocked — plan has no items." Preview step dead-ends:
   safety card shows blocked, "Run preview" disabled.
2. "The steps are not finished but I navigate to the other screens — fix this.
   Should you add a discard-changes restriction or block navigation, or so on —
   as long as it's good UI/UX."
3. "Make the updates the same in Windows when you update the app — it's the
   same product on different platforms; check each platform's flow."

Goal: fix the empty-plan root cause, and give the organizer a coherent
leave-mid-flow policy — implemented symmetrically on Android and Windows.

## Repository Audit

### Verified Existing State — Issue 1: empty plan (Android, `BROKEN`)

- `SafetyEngine.validate` blocks `plan.items.isEmpty()` with
  `"Plan has no items."` (`core/.../safety/SafetyEngine.kt:27`); the Preview
  button requires `safety.allowed && items.isNotEmpty()`
  (`OrganizerScreen.kt:356`). Correct guard — the items list is empty because
  **every file decided `NoOp`/`Ignore`** (`ActionPlanner.plan` returns an
  empty-item plan for those kinds, `ActionPlanner.kt:29-39`; `Review` yields an
  `Ask` item — a Review file would still show).
- The PLAN-025 confidence fix **is** in the installed build
  (`FileMindAndroid.kt:97` → `highThreshold=15, mediumThreshold=5`, committed
  09:54, rebuilt + installed 10:27). Under it, ext-signal alone (10) already
  reaches Medium → Review → an Ask item. So an empty plan means **score < 5
  for every file**: no extension signal AND no mime signal AND no keyword.
- Root cause — scan discards provider metadata:
  - `emitSafChildren` (`AndroidFileSystemAdapter.kt:61-82`) emits
    `FileEntry(path = child.uri.toString())` and **drops** `DocumentFile.name`
    / `DocumentFile.type`.
  - `emitMediaChildren` (`:84-121`) queries `DISPLAY_NAME`
    (and could query `MIME_TYPE`) — then discards it and emits the numeric
    row URI `content://media/…/<id>`.
  - `FileEntry` (`core/ports/…` lines 29-34) has no name/mime fields, so
    `FileScanner.scan` (`FileScanner.kt:19-31`) derives
    `displayName`/`extension`/`mimeType` solely from the URI string.
  - `SafPathNormalizer.fileName` resolves names only from docIds that embed a
    readable path (`<vol>:<rel/path>`). Providers with **opaque docIds**
    (downloads provider `msf:NNN` / numeric ids, some SD-card/OEM providers)
    yield `fileName` = "12345" → `extension=""`, `mimeType=null` →
    `FileKind.Other` + zero evidence → `NoOp` for every file.
  - Even on good URIs, `mimeType` comes only from `MimeMap.fromExtension`
    (~33 exts); e.g. `.heic` scores ext-signal only (10 → Medium) though the
    provider reports `image/heic`.
- `BROKEN` classification: MediaStore-folder scans are guaranteed empty plans;
  SAF scans break for opaque-docId providers. On a normal
  `externalstorage` tree the flow works — user-visible repro depends on which
  folder/provider was granted (device verification in QA step).

### Verified Existing State — Issue 1: Windows parity

- Windows `FileScanner` mirrors the same URI-derived name logic
  (`src/FileMind.Application/Scanning/FileScanner.cs:21-33`), but `entry.Path`
  is always a real NT path → `Path`-based `FileName` always works. `EXISTING`,
  no functional bug. Contract change still applies symmetrically (see below).
- Windows pipeline wires `LocalAnalyzerService` enricher + `AiAdvisor` +
  calibrator (`FmBackend.cs:104-115`) → higher achievable confidence; the
  Android policy gap does **not** exist on Windows. `EXISTING`.
- Same `Items.Count > 0` + `SafetyEngine` blocker exists on Windows
  (`OrganizerPage.xaml.cs:337`) — intentional for truly-empty plans; Windows
  already shows `PlanSkipped` count ("N file(s) untouched") and per-decision
  chips at Analyze — Android shows neither. `INCONSISTENT` (parity gap).

### Verified Existing State — Issue 2: leaving mid-organizer

**Android (`BROKEN`/`INCOMPLETE`):**

- `OrganizerViewModel` is created via `backendViewModel { … }` scoped to the
  NavBackStackEntry (`OrganizerScreen.kt:102`, `ViewModels.kt:31-39`).
- Bottom-nav switches use `popUpTo(start){saveState=true} + restoreState`
  (`AppShell.kt:109-113`) — the organizer entry's state is saved.
- **But** re-entering Organizer goes through plain `nav.navigate(Routes.Organizer)`
  from FABs/cards (`HomeScreen.kt:170,294`, `FilesScreen.kt:192,226`,
  `ActivityScreen.kt:163`, `ToolsScreen.kt:70`, `HelpScreens.kt:120`) with **no
  `restoreState`** → a new back-stack entry + fresh VM → flow resets to
  `OrgStep.Access`. Verified: this is why steps restart after navigating away.
- System Back pops the entry → VM cleared → `runJob` cancelled mid-execute;
  `finalizeCancelled` (`ViewModels.kt:580-587`) terminalizes the record
  (NeedsAttention) — data integrity OK, UX silently abandons the session.
- App-scope precedent exists: `settingsViewModel` is a `FileMindApp` lazy
  singleton (`FileMindApp.kt:11`).

**Windows (`BROKEN`/`INCOMPLETE`):**

- `OrganizerPage` holds all flow state in page instance fields (`_step`,
  `_candidates`, `_analyzed`, `_plan`, `_cts` — `OrganizerPage.xaml.cs:26-33`).
- `Shell.Nav_SelectionChanged`/`NavigateTo` → `ContentFrame.Navigate(...)`
  (`Shell.xaml.cs:61-68`); the page sets no `NavigationCacheMode` → default
  behavior discards the instance → **every nav-away loses the whole flow**.
- A mid-flight `RunExecuteAsync` (`:366-411`) keeps running headless — its
  record persists correctly, but the UI/result is orphaned and `_cts` becomes
  unreachable (can't cancel). Same defect class as Android.
- No leave-confirm exists on either platform. `MISSING`.

### Existing Rules Found

- `docs/packs/crossplatform-ux/06_ROUTING/NAVIGATION_ROUTING.md`: "Back/forward
  must return users to meaningful prior state, not merely reload a default
  page." — supports state preservation.
- `docs/packs/crossplatform-ux/03_ANDROID/ANDROID_UX.md`: "Respect Android
  system back behavior and maintain navigation state"; "Long-running tasks
  should remain visible through the in-app Task Center" — supports
  continue-in-background + Activity visibility over hard navigation blocking.
- `docs/rules/MOCKUP_RULES.md`: UI-relevant changes must update `mockup/` in
  the same change.
- `AGENTS.md`: approval before implementation; repair before replace; small
  per-feature commits pushed to `origin/main`.

### Documentation vs Code Differences

- PLAN-025 marked organizer fixes IMPLEMENTED with device QA open (M10). This
  plan's device repro exposes a defect class PLAN-025 did not cover
  (provider-supplied metadata) — recorded here rather than claimed fixed.

## Findings

| # | Finding | Class |
|---|---------|-------|
| F1 | SAF/MediaStore enumerators discard provider `name`/`mime` → opaque-docId scans classify every file `NoOp` → "Plan has no items" | BROKEN |
| F2 | `FileEntry` contract lacks `displayName`/`mimeType` (Kotlin + C#) | INCOMPLETE |
| F3 | Android organizer re-entry creates a fresh VM → flow resets | BROKEN |
| F4 | Windows `OrganizerPage` destroyed on every `Frame.Navigate` → flow resets; running execute orphaned | BROKEN |
| F5 | No leave-guard UX on either platform while a run is executing | MISSING |
| F6 | Android Preview lacks decision-breakdown counts (Windows has chips + skipped count) | INCONSISTENT |
| F7 | Windows enricher/advisor/calibrator wiring has no Android analogue — intentional (offline-first, SAF-bounded); not a defect | EXISTING |

## Root Cause / Problem

1. **Empty plan:** the scan pipeline asks "what does the URI string say?"
   instead of "what does the provider say?" For opaque docIds / media rows the
   URI carries no name → zero evidence → all `NoOp`. Fix at the contract
   layer: carry provider-supplied `displayName`/`mimeType` through `FileEntry`
   into `FileCandidate`.
2. **Lost flow:** organizer session state is scoped to a transient
   screen/entry on both platforms. Fix at the session layer: outlive
   navigation; add a guard only where leaving is destructive (mid-run cancel).

## Proposed Changes

### M1 — Provider-supplied file identity (contract + scanners + adapters)

- `core/ports` `FileEntry`: add `displayName: String? = null`,
  `mimeType: String? = null`.
- `AndroidFileSystemAdapter`:
  - `emitSafChildren`: `displayName = child.name`, `mimeType = child.type`.
  - `emitMediaChildren`: add `MIME_TYPE` to the existing projection;
    `displayName`/`mimeType` from the cursor (name already queried!).
  - `emitFileChildren`: `displayName = child.name`.
- `FileScanner.scan` (Kotlin): `name = entry.displayName ?: paths.fileName(entry.path)`;
  `mimeType = entry.mimeType ?: MimeMap.fromExtension(ext)`.
- **Symmetric contract change (C#):** `FileEntry` gains
  `string? DisplayName = null, string? MimeType = null` (positional params
  appended — check call sites compile). Windows FS adapter populates
  `DisplayName = Path.GetFileName(...)`; `FileScanner.cs` prefers
  `entry.DisplayName ?? paths.FileName(entry.Path)` and
  `entry.MimeType ?? MimeMap.FromExtension(ext)`. Behavioral no-op on Windows
  (paths always carry names) but keeps the two platforms structurally equal —
  the user-facing ask.
- Unit tests: scanner prefers provider name/mime; SAF-adapter unchanged-name
  fallback (existing `SafPathNormalizerTest` patterns); C# parity test for the
  same rule.

### M2 — Android organizer session survives navigation

- Hoist `OrganizerViewModel` to app scope: `FileMindApp.organizerViewModel`
  lazy singleton (same pattern as `settingsViewModel`); `OrganizerScreen`
  consumes it instead of `backendViewModel`. All organizer state then survives
  tab switches, FAB re-entry, deep links, and process-death-adjacent cases
  (VM scope = app).
- `vm.reset()`/`StartOver` semantics unchanged — explicit fresh start still
  available via "Pick another folder".
- No `restoreState` flags needed anywhere — entry-scoping problem removed,
  not patched per-call-site.

### M3 — Leave-mid-flow guard (Android)

- During `OrgStep.Executing` only: intercept exit attempts
  (system Back via `BackHandler` in `OrganizerScreen`; bottom-nav/top-bar
  navigation is free — see UX rationale) → `AlertDialog`:
  - Title "Run in progress", body "N of M items done. The run continues in
    the background — find it in Activity."
  - Buttons: [Keep running] (default — leave page, run completes; record +
    notification land in Activity per ANDROID_UX) · [Cancel run] (existing
    `vm.cancel()` semantics, record terminalized NeedsAttention).
  - Rationale: don't *block* navigation — ANDROID_UX wants long tasks visible
    via Task Center, and the app-scoped VM makes the run genuinely safe.
    The dialog protects against accidental abandons, not against leaving.
- Non-executing steps: free navigation, no dialog — nothing is lost (state
  preserved by M2). A confirm on Preview adds friction without protecting
  anything.
- Alternative considered (recorded, not chosen): block all navigation until
  flow ends — rejected as hostile and unnecessary once state persists.

### M4 — Windows parity for M2+M3

- `OrganizerPage`: `NavigationCacheMode = NavigationCacheMode.Required` (page
  instance cached by the Frame → `_step`/lists/`_cts` survive nav-away; a
  running execute keeps polling and renders correctly on return).
- Leave-guard: in `Shell.Nav_SelectionChanged` + `NavigateTo`, when current
  page is `OrganizerPage` and it reports a busy state (new
  `internal bool IsRunning` / `HasUnsavedProgress` property — true while
  `Step == Execute`, or while a built-but-unexecuted plan exists), show
  `ContentDialog`:
  - Executing: "Run in progress — N of M done." [Keep running]/[Cancel run]/
    [Stay]. Stay → revert `Nav.SelectedItem` to organizer (guard re-entrancy).
  - Unsaved plan (not executing): "Leave the organizer? Your plan is kept —
    come back any time." [Leave]/[Stay] — informational since cache mode
    preserves it; only shows if we keep Windows aligned to Android's
    free-navigation rule. **Simpler parity: show the dialog only while
    Executing, same as Android.** Recommended: dialog only while Executing.
- `DispatcherQueue` updates from a cached-but-unloaded page are safe
  (fields update; controls rebind on next attach).

### M5 — Preview truthfulness + mockup sync

- Android Preview step: add decision-count row (e.g. "5 to move · 2 to inbox ·
  8 untouched") under the summary card — matches Windows' AnalyzeChips +
  `PlanSkipped` line and mockup's plan summary. Empty-plan `EmptyState` copy:
  explain the cause honestly ("no recognizable file types — files left in
  place" vs "already organized").
- `mockup/android.html` + `mockup/windows` equivalent: add the decision-count
  line on the plan page and the run-in-progress leave dialog, per
  `MOCKUP_RULES.md` (same change, not a follow-up).

## Non-Goals

- No changes to `SafetyEngine`'s "Plan has no items" blocker — the guard is
  correct; we're removing the cause, not the check.
- No Android enricher/advisor — offline-first scope unchanged (PLAN-025 note).
- No new navigation framework; no DI container.
- No changes to MediaStore-folder *sources* list (not currently offered in the
  organizer Source step — capability fix only).

## Files Expected to Change

- `android/core/src/main/kotlin/com/filemind/core/ports/…` (`FileEntry`)
- `android/core/src/main/kotlin/com/filemind/core/app/FileScanner.kt`
- `android/platform/src/main/kotlin/com/filemind/platform/fs/AndroidFileSystemAdapter.kt`
- `android/app/src/main/kotlin/com/filemind/app/FileMindApp.kt` (VM singleton)
- `android/app/src/main/kotlin/com/filemind/app/ui/OrganizerScreen.kt` (VM source, BackHandler+dialog, decision counts, empty copy)
- `android/app/src/main/kotlin/com/filemind/app/vm/ViewModels.kt` (minor — expose busy/run state if needed for dialog counts)
- `src/FileMind.Domain/Ports/IFileSystemAdapter.cs` (`FileEntry` record)
- `src/FileMind.Application/Scanning/FileScanner.cs`
- `src/FileMind.Infrastructure.*/…` Windows FS adapter `EnumerateAsync` impl (populate DisplayName)
- `src/FileMind.WinUI/Views/OrganizerPage.xaml.cs` (+ .xaml if needed) — cache mode, `IsRunning`, guard surface
- `src/FileMind.WinUI/Shell.xaml.cs` — nav guard + dialog
- `mockup/android.html` (+ JS), windows mockup equivalent
- Tests: `android/core/…` scanner test; `android/platform` adapter test if feasible; `tests/FileMind.*` parity/unit tests

## New Files Required

None expected.

## Architecture Impact

`FileEntry` contract widened by two optional fields on both platforms —
additive, backward compatible. Organizer session lifetime moves from
screen-scope to app-scope (Android) / frame-cached (Windows) — matches the
documented "meaningful prior state" routing rule.

## Data / Storage Impact

None — no schema or settings changes.

## Routing / Flow Impact

Android: organizer state no longer resets on re-entry; Executing step
intercepts Back with a continue/cancel dialog. Windows: identical guard via
NavigationView + ContentDialog. No route changes.

## Platform Impact

- Android: fixes the reported empty plan + lost-steps bugs.
- Windows: contract symmetry + nav-guard parity; no behavioral regression
  (NT paths unaffected).
- Mockup: two UI additions mirrored per MOCKUP_RULES.

## Security / Privacy Impact

None — provider metadata (name/MIME) is already granted by the SAF/MediaStore
access model; nothing new leaves the device.

## Error Handling / Edge Cases

- Provider returns null name/type → fall back to URI-derived values (today's
  behavior) → NoOp path still available and now *explained* in UI.
- Mid-run cancel during dialog → `finalizeCancelled` path already terminalizes.
- App-scope VM holds no Activity/Context refs (constructor takes FileMindApp) —
  no leak introduced.
- Windows: cached page with a finished run shows Result on return — correct.

## Implementation Phases

1. M1 contract + adapters + scanners (both platforms) + unit tests.
2. M2 Android app-scope VM.
3. M3 Android executing-leave dialog.
4. M4 Windows cache-mode + dialog.
5. M5 Preview decision counts + mockup sync.
6. QA + device verification + docs updates + commits per feature.

## QA Plan

- `cd android && ./gradlew.bat :core:test :platform:testDebugUnitTest :app:testDebugUnitTest` (new scanner/provider-metadata tests green).
- `dotnet test FileMind.slnx --artifacts-path <out>` — C# scanner parity test.
- **Device (Infinix X6725B):** reproduce the reported flow — grant the same
  folder → scan → Preview shows items + decision counts → toggle dry-run →
  Run preview → Done. Also verify a second provider type if available.
- Device: mid-scan/mid-preview/mid-execute → switch tabs, use FAB re-entry,
  system Back → state preserved; executing → dialog appears, both paths
  verified (keep-running lands in Activity; cancel terminalizes).
- Windows (if buildable under SAC — `tools/fm-sign.ps1` dev-sign path per
  README): organizer mid-run nav-away → dialog; return → live progress.

## Acceptance Criteria

- A SAF scan of a folder with normal files yields non-empty plan items on the
  Infinix (verified on device, not just unit tests).
- Files with unrecognizable types still NoOp but the Preview explains counts.
- Leaving and re-entering the organizer preserves the exact step on both
  platforms; leaving during Execute prompts once, with working keep/cancel.
- All existing tests pass; mockup updated in the same change.

## Risks / Regression Areas

- `FileEntry` positional-record change → compile sweep of all adapters/tests
  on both platforms (low risk, additive with defaults).
- App-scope VM leaks a finished session's state across app restarts — state is
  in-memory only; `refreshGrants()` on entry keeps grants fresh. Document the
  "session persists for app lifetime" choice.
- WinUI `NavigationCacheMode.Required` keeps page alive for app lifetime —
  acceptable (single organizer session per app run).

## Documentation Updates

- PLAN-025 status note (device QA finding → follow-up here).
- `docs/architecture/` + `docs/features/FEATURES.md` (F-UI4) if behavior
  descriptions change.
- `docs/packs/crossplatform-ux/` — record the leave-mid-flow contract
  (preserve state; confirm only while executing) so platforms stay aligned.
- `docs/qa/` evidence file for device verification.

## Commit Plan

- `fix(android-scan): carry provider displayName/mime through FileEntry → no more all-NoOp empty plans` (M1 android side)
- `fix(scan): FileEntry displayName/mime parity on Windows contract + scanner` (M1 windows side)
- `fix(android-organizer): app-scope session VM so organizer survives navigation` (M2)
- `feat(android-organizer): run-in-progress leave guard` (M3)
- `fix(winui-organizer): cached page + run-in-progress leave guard` (M4)
- `feat(android-organizer): decision counts on plan preview + mockup sync` (M5)

## Approval

User approval required before implementation.

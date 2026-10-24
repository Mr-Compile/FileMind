# PLAN-021 — FileMind Android UI Megaplan (Jetpack Compose app — the deferred "Android-UI plan")

Status: APPROVED
Related Feature: new F-UI4 row (Android UI); feeds F-UI3 Android contract obligations directly from the packs
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement the Android UI that PLAN-010/PLAN-014/PLAN-019 all deferred to a
"future Android-UI plan" that was never written. Deliver a new `android/app`
module: a Jetpack Compose + Material 3 app implementing the approved
Material-3 Minimal Utility spec — the full shared IA (Home, Files, Search,
Tools, Activity & Operation Center, Settings, Organizer flow, Help & About
subtree) — driven by the real `android/core` + `android/platform` backend.

User-approved scope decisions (2026-10-24):

- Jetpack Compose + Material 3 (not XML Views).
- Standalone: cite `docs/packs/crossplatform-ux/` + `docs/packs/ui-ux-foundation/`
  directly. PLAN-019 stays its own plan; `UX_CONTRACTS.md` reconciles later.
- Full shared IA (all screens + 9-destination Help & About).
- Emulator verification: create an API-36 AVD (system-image download) and run.

## Repository Audit

### Verified Existing State

- `android/core` — pure-JVM Kotlin engine port: `OrganizerPipeline`
  (`scan(root: String): Flow<FileCandidate>`, `analyze`, `plan(dryRun,
  destinationRoot, sourceRoot, preserveTree)`, `dryRun`, `execute`, `undo`,
  `recentOperations`, `index`), `ModePolicy` (`organization.mode` settings
  key; Manual/Smart/Automatic/AiAssisted/LocalOnly), contracts
  (`FileCandidate`, `AnalyzedFile`, `ActionPlan`, `ActionItem`, `SafetyResult`,
  `OperationRecord`, `OperationItemState`, `SmartInboxItem`), enums
  (`DecisionKind` Organize/Review/Ignore/NoOp, `OperationStatus`
  Planned/Validating/Executing/Verifying/Committed/Failed/RollingBack/
  RolledBack/NeedsAttention, `FileKind`, `ConfidenceBand`, `SourceScope`).
- `android/platform` (`com.android.library`, namespace `com.filemind.platform`,
  minSdk 26, compileSdk 36, Java 17): `FileMindAndroid.from(context)` manual
  service graph; `pipeline()` (suspend, snapshots catalogs/rules);
  `UriGrantStore` (`take(treeUri, flags)`, `release`, `grants()`,
  `revalidate()`, `covers()`); `AndroidFileSystemAdapter` (SAF tree/doc,
  MediaStore, app-private only — anything else is a capability error, never
  faked); `AndroidCapabilityModel`; Room `FileMindDatabase` v2 +
  `Room*Repository` impls incl. `settings`, `fileIndex`, `operations`,
  `smartInbox`, `protectedPaths`, `watchedFolders`, `jobs`;
  `FileMindWorkScheduler` (`enqueueScan`, `enqueueOrganize`,
  `cancelScan/cancelOrganize`, battery/storage constraints, unique-work
  idempotency); `NotificationContracts` (3 channels) +
  `AndroidNotificationService`; headless `ShareIntakeActivity`
  (ACTION_SEND/_MULTIPLE → Smart Inbox).
- **No `app` module, no UI code.** `android/settings.gradle.kts` includes
  `:core`, `:platform` only. `ShareIntakeActivity` is the only Activity and
  is explicitly headless.
- Toolchain (`docs/development/BUILD.md`, verified): AGP 9.0.1, built-in
  Kotlin 2.2.10, KSP 2.3.6, Gradle 9.1.0 wrapper, JBR 21 at
  `C:/Program Files/Android/Android Studio/jbr`. SDK at
  `%LOCALAPPDATA%/Android/Sdk`: `platforms;android-36` (+36.1),
  build-tools 36.0.0, emulator + platform-tools binaries, cmdline-tools —
  **no system-images, no AVDs**.
- Kotlin-metadata constraint: `kotlin-stdlib`/`kotlinx-*` pinned to Kotlin
  ≤2.2 output; `androidx.core` pinned 1.17.x (1.19.x needs compileSdk 37 /
  AGP 9.1). Same discipline applies to the app module.
- Brand assets verbatim: `branding/logo/android/res/` (adaptive-icon,
  mipmap densities, `ic_launcher_foreground/monochrome` VectorDrawables,
  `ic_notification`, `splash_icon.xml`), `branding/logo/android/colors.xml`.
  Branding pack: Android typeface = Roboto / Roboto Flex.
- Specs `EXISTING`: `ui-ux-foundation/platforms/android.md` (adaptive nav,
  touch targets, long-press select + contextual app bar, bottom sheets,
  system Back, explain-why permission flows, single-column first, M3
  light/dark/system + FileMind semantic tokens); `screens/01–06`;
  `foundations/02-design-tokens.md` (spacing 4–48, radius 6/10/14/18/pill,
  semantic color slots incl. status/offline + status/neutral, elevation 0–3,
  Android = comfortable touch density only); `IMPLEMENTATION_GUIDE.md`
  (component states `idle|loading|success|empty|warning|error`, op states
  12-state model, error boundary → friendly categories); pack UX docs
  01–09 (operation flow `Select source → Choose action → Pre-check →
  Resolve restrictions → Confirm if risky → Execute → Progress → Result →
  Next action`, pre-check list, result summary contract).
- Cross-platform spec `EXISTING`: `crossplatform-ux/01_SHARED_UX` (IA
  Home/Files/Search/Tools/Activity/Settings; Settings groups General/
  Appearance/File Management/Privacy & Safety/Updates/Help & About; Help &
  About 9 destinations; lifecycle Analyze→Preview→Confirm→Execute→Result→
  Undo/Activity; 14 shared statuses; empty states = explanation + next
  action; Why? explainability); `03_ANDROID/ANDROID_UX.md` (system back,
  touch targets, action sheets, no Ctrl+K equivalent, Task Center
  visibility for long-running tasks, storage-permission explainer +
  Manage Access, compact About); `04_HELP_ABOUT`; `05_SAFETY` (8-step
  required flow: analyze→preview→counts→deletion statement→confirm→
  execute w/ progress+cancel→result→undo/activity; undo first-class;
  protected-folder authorization); `08_QA` checklist.

### Feature truth for truthful labeling (Android surface)

- `EXISTING` on Android: scan/analyze/plan/execute/undo, rules read,
  collections/subjects/projects catalogs, smart inbox, exclusions,
  protected paths (system + user), watched-folder records, job queue,
  settings, operations/history, SAF grant model, share→inbox, WorkManager
  scan/organize, notifications contract.
- `MISSING` on Android (Windows-only today): FTS5 search (F-P9 is C#
  SQLite), duplicates/storage analysis services (F-P6 C#-only),
  `IFileAnalyzer` enrichers (F-P5 C#-only), Groq provider (F-P7 C#-only),
  watch/orchestrator, profile export/import service (F-P10 C#-only),
  learning/calibration services (F-P11 C#-only).
- Consequence: Search = index filtering (name/ext/kind/date) with content
  search labeled `Planned`; Tools cards for duplicates/storage/AI =
  `Planned`-badged; AI-assisted mode is selectable-in-name only and must
  state the provider is not shipped on Android.

### Existing Rules Found

- UI never mutates — mutations flow `Decision + Safety + ActionPlan →
  Platform Adapter → Verify → History` (`RULES.md` / packs).
- Status never color-only (icon + label + color). Truthful progress; no
  fake progress. Friendly errors (`ux/06`).
- MOCKUP_RULES: feature-status changes update `mockup/` in the same
  change — `mock-data.js` devices + `flows.js` architecture rows say
  "Android UI not shipped" today.
- Plans: statuses per `docs/plans/README.md`; this file records user
  approval given in chat 2026-10-24.

### Documentation vs Code Differences

- `mockup/mock-data.js`/`flows.js` label Android "backend only — UI not
  shipped": `EXISTING` truth today, becomes stale when this plan ships —
  updated in Phase 6.
- `FEATURES.md` F-P8 says "Android UI remains out of scope": accurate
  today; F-UI4 row added in Phase 6.
- `ARCHITECTURE.md` "NOT IMPLEMENTED YET: Android, UI" — partially stale
  (Android backend exists); corrected in Phase 6.

## Findings

- App module: `MISSING` → `android/app`.
- Shared IA surface: `MISSING` entirely on Android.
- Semantic-token map spec→`colors.json` for Compose: `MISSING` (same gap
  PLAN-014 reconciles for XAML; this plan does the M3 equivalent).
- SAF permission UX (explainer + Manage Access): `MISSING` — backend
  `UriGrantStore` exists.
- Help & About, Diagnostics, protected-folder UX: `MISSING`.
- Emulator/system image for verification: `MISSING` → downloaded in M0.

## Global Constraints

- UI never mutates: app module calls `pipeline()`/`FileMindWorkScheduler`/
  repositories only. No `DocumentsContract`/`MediaStore`/`contentResolver`
  mutation calls in `app/` outside the grant flow (`OpenDocumentTree`
  result + `takePersistableUriPermission` — permission chrome, allowed).
- Offline-first; no telemetry; diagnostics privacy-safe by default (no
  filenames/paths unless explicit opt-in).
- Every async surface models `idle|loading|success|empty|warning|error`;
  operations model the 12-state op model. No color-only status. Empty
  states always offer a next action.
- Truthful `Planned` badges on Windows-only features; nothing fake-functional.
- Android adaptations per pack: bottom nav on phone / rail on wide,
  ≥48dp targets, long-press select + contextual app bar, bottom sheets,
  system-Back integration, explain-why permissions, single-column first,
  no compact desktop density.
- Deps: pinned versions, Kotlin ≤2.2 metadata compatible, compileSdk 36 —
  choices recorded with rationale in `BUILD.md`.
- No plan subfolders; statuses per `docs/plans/README.md`.

## Proposed Changes / Implementation Phases

### Milestone 0 — Module scaffold + emulator bring-up

Exit gate: `gradlew assembleDebug` green; `:app` installs + cold-starts a
blank themed shell on an API-36 emulator.

- 0.1 `android/settings.gradle.kts`: `include(":core", ":platform", ":app")`.
- 0.2 `android/app/build.gradle.kts`: `com.android.application` 9.0.1,
  `org.jetbrains.kotlin.plugin.compose` 2.2.10; applicationId
  `com.filemind.app`, minSdk 26 / targetSdk 36 / compileSdk 36; `project(
  ":platform")`; Compose BOM + `material3`/`ui`/`ui-tooling`(debug)/
  `foundation`; `activity-compose`, `navigation-compose`,
  `lifecycle-viewmodel-compose` + `lifecycle-runtime-compose`,
  `core-splashscreen`; `kotlin-stdlib:2.2.10` constraint like `:platform`.
  Build features: `compose = true`, `buildConfig = true`.
- 0.3 `AndroidManifest.xml`: `FileMindApp` Application; `MainActivity`
  LAUNCHER exported, `Theme.SplashScreen`; `POST_NOTIFICATIONS`; merges
  with platform's share-intake activity.
- 0.4 `app/src/main/res/` ← verbatim drop-in of
  `branding/logo/android/res/**` + `values/themes.xml` (splash → app
  theme), `values/strings.xml`, provenance README block.
- 0.5 Emulator: `sdkmanager "system-images;android-36;google_apis;x86_64"`
  → `avdmanager create avd -n filemind-pixel -k <image>` → `adb` smoke.
  Commands recorded in `BUILD.md`.

### Milestone 1 — Theme, tokens, navigation shell

Exit gate: every route renders a themed placeholder; theme switch works;
adaptive nav flips bar↔rail by width.

- 1.1 `theme/Color.kt`: every `branding/colors.json` token as consts
  (provenance comment); `theme/Theme.kt`: M3 `lightColorScheme`/
  `darkColorScheme` from brand tokens + `FileMindSemantic` extension
  (surface/elevated, text/disabled, status/{info,success,warning,error,
  destructive,offline,neutral}); `ThemeMode` System/Light/Dark bound to
  `settings` key `ui.theme.mode`.
- 1.2 `theme/Type.kt` (M3 scale, Roboto default), `Spacing`/`Radius`/
  `Elevation` token objects per spec values.
- 1.3 `nav/Routes.kt` shared route IDs: `home files search tools activity
  settings organizer help help/{about,getting-started,guide,whats-new,
  troubleshooting,diagnostics,feedback,credits,licenses}`.
- 1.4 `AppShell.kt` + `FileMindNavHost.kt`: `NavigationSuiteScaffold`
  (bottom bar phone / rail expanded), system-Back, per-route state.

### Milestone 2 — Components + status system

Exit gate: component gallery states render; zero color-only statuses.

- `StatusChip` (icon+label+color; shared vocabulary → domain map:
  DecisionKind/OperationStatus/FileKind/flags→chips incl. `Planned`);
  `EmptyState` (explanation + next action); `ProgressCard` (truthful
  progress + cancel); `WhySheet` (Evidence signals, score contributions,
  confidence band, sources); `ConfirmSheet` (counts, "0 deletions"
  statement, irreversibility, undo availability); `PermissionExplainer`;
  `FriendlyError` (`FileMindError` code → title/explanation/recovery);
  `SummaryStrip` (counts). All state-contracted.

### Milestone 3 — Core surfaces

Exit gate: Home/Files/Devices/Activity render real data on device.

- 3.1 Home: storage card (StatFs), grant status card, quick actions,
  active ops (WorkManager `WorkInfo` flow + non-terminal records), recent
  activity, cleanup card (`Planned` deep-link truthfully).
- 3.2 Files: `fileIndex.getAll()` list, scope chips, sort/filter/name
  search, long-press multi-select + contextual app bar, inspector sheet
  (metadata + `Why?` when analyzed state exists), Protected/Excluded
  chips from engine flags.
- 3.3 Devices: This device (Build identity, storage, capability matrix
  from `AndroidCapabilityModel` + live grant coverage, restrictions);
  truthful guidance copy.
- 3.4 Activity & Operation Center: active section (WorkInfo + in-flight
  records; cancel), completed (summary, warnings, item counts, Undo where
  `undoRecipe` exists, open-location where a browsable root exists),
  Smart Inbox (`getOpen`/`resolve`), 12-state chip legend. Doubles as the
  pack's Task Center.

### Milestone 4 — Organizer flow + Tools + Search

Exit gate: real pipeline e2e on a granted SAF tree with progress/cancel/
undo on emulator.

- 4.1 Organizer: explainer → `OpenDocumentTree` → `grantStore.take` →
  `revalidate` → `scan(tree)` collect → `analyze` → decision list (chips,
  confidence, Why?) → `plan()` preview → `dryRun()` safety (blockers
  hard-stop, warnings banner, `requiredConfirmation` typed confirm) →
  ConfirmSheet (moved/copied/renamed/unchanged counts, "0 deletions —
  FileMind does not delete files", undo statement) → execute (in-app
  `pipeline.execute` for interactive run; `enqueueOrganize` when
  backgrounded) → progress + `cancelOrganize` → result (counts, where
  files went) → Undo + View Activity.
- 4.2 Tools: Organize (works), Rules viewer (read-only `rules.getAll()`),
  Find duplicates / Analyze storage / AI tools `Planned`-badged.
- 4.3 Search: name/ext/kind/date filters over `fileIndex`; content +
  semantic `Planned`.

### Milestone 5 — Settings + Help & About

Exit gate: settings persist; Help subtree complete; diagnostics copy
privacy-safe.

- 5.1 Settings: General (organization mode Manual/Smart/Automatic via
  `ModePolicy.SettingsKey`; AiAssisted/LocalOnly rows truthful about
  absent provider), Appearance (System/Light/Dark live), File behavior,
  Permissions (grant list + revoke + Manage Access → `OpenDocumentTree`;
  POST_NOTIFICATIONS w/ explainer on 33+), Notifications (channels),
  Advanced, Help & About entry.
- 5.2 Help & About (compact): About (logo lockup, version from
  `BuildConfig`, purpose, links), Getting Started (real workflow), User
  Guide, What's New, Troubleshooting, Diagnostics (version/build/SDK/
  Room ping/grant coverage; Copy Diagnostic Info privacy-safe default +
  explicit paths opt-in), Feedback (labeled surface), Credits, Licenses.

### Milestone 6 — QA + docs

Exit gate: checklist evidence recorded; FEATURES F-UI4 truthful; mockup
synced; plan status IMPLEMENTED.

- 6.1 Emulator verification: every route, organizer e2e on granted tree
  (adb-pushed fixture), both themes, screenshots.
- 6.2 `UI_UX_QA_CHECKLIST` + `crossplatform-ux/08_QA` applicable rows;
  a11y: 48dp targets, content descriptions, TalkBack order, contrast,
  reduced-motion respected.
- 6.3 Boundary grep: no FS-mutation calls in `app/` outside grant flow.
- 6.4 JVM unit tests for UI-state mappers; instrumented Compose smoke
  test on the AVD where practical.
- 6.5 Docs: `FEATURES.md` F-UI4; `MODULES.md`; `PLATFORM.md`;
  `BUILD.md` (build + emulator + dep pins); `CHANGELOG.md`; mockup
  `mock-data.js`/`flows.js` status edits; plan status.

## Non-Goals

- No `core`/`platform` logic changes (UI surfaces what exists; new
  `ui.theme.mode` key uses generic `SettingsRepository` — no schema work).
- No FTS/content search, duplicates/storage, Groq, enrichers, watch
  service, profile sync, or deletes on Android — all `Planned`-labeled.
- No mockup redesign (PLAN-019); only status-truthfulness edits.
- No WinUI work (PLAN-014), no release signing/Play listing, no Rust.

## Files Expected to Change

`android/settings.gradle.kts`, `docs/features/FEATURES.md`,
`docs/architecture/{MODULES,PLATFORM}.md`, `docs/development/BUILD.md`,
`CHANGELOG.md`, `mockup/{mock-data.js,flows.js}`.

## New Files Required

`docs/plans/PLAN-021-MEGAPLAN.md` (this file);
`android/app/{build.gradle.kts,src/main/AndroidManifest.xml}`;
`android/app/src/main/kotlin/com/filemind/app/**` (Application,
MainActivity, theme/, nav/, components/, ui/{home,files,organizer,tools,
devices,activity,settings,help}/, vm/, data/);
`android/app/src/main/res/**` (brand drop-in + themes/strings);
`android/app/src/test/**`.

## Security / Privacy Impact

No telemetry; diagnostics privacy-safe default + opt-in paths; grant
model unchanged (UI consumes `UriGrantStore`); POST_NOTIFICATIONS only
with explainer; secrets untouched (no Groq on Android).

## Error Handling / Edge Cases

- Grant revoked mid-flow → `revalidate()` before runs; permission_required
  state, not crash.
- Unsupported root URI → adapter's capability error → friendly surface.
- Partial operation failure → `OperationItemState` failures listed;
  NeedsAttention surfaced.
- Backgrounded during interactive execute → WorkManager path keeps op
  visible in Task Center via notifications.
- Empty index/grants → next-action empty states (grant folder / open
  organizer).

## QA Plan

Emulator evidence per M6; JVM unit tests for mappers; checklists; a11y
sweep; boundary grep; `assembleDebug` + `testDebugUnitTest` green.

## Acceptance Criteria

- [ ] `assembleDebug` green; app installs + launches on API-36 emulator
- [ ] All shared-IA routes render; adaptive nav bar↔rail correct
- [ ] System/Light/Dark themes from canonical `colors.json`
- [ ] Organizer e2e on granted SAF tree: progress/cancel/result/undo truthful
- [ ] Explainer-before-request permission flow + Manage Access
- [ ] No color-only status; empty states have next actions; `Planned` truthful
- [ ] Zero direct FS mutation in `app/` (grep)
- [ ] QA evidence; F-UI4 truthful; mockup synced

## Risks / Regression Areas

System-image download size + emulator speed (fallback: UNVERIFIED device
note like PLAN-010); androidx/compileSdk-36 pin resolution (pin
conservatively + record); SAF revoke edge cases (`revalidate` gates);
scope size → phase commits.

## Documentation Updates

`FEATURES.md` F-UI4; `MODULES.md`; `PLATFORM.md`; `BUILD.md`;
`CHANGELOG.md`; `mockup` status edits; this plan's status.

## Commit Plan

`docs(plans): PLAN-021` → `chore(android-app): module scaffold` →
`feat(android-app): theme + tokens + shell` → `feat(android-app):
components` → `feat(android-app): surfaces` → `feat(android-app):
organizer + tools + search` → `feat(android-app): settings + help` →
`test/docs(*):`. No pushes.

## Approval

Approved by user 2026-10-24 ("implement it immediately approved" in chat);
recorded per `docs/plans/README.md` convention.

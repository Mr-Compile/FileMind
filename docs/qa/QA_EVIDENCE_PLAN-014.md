# PLAN-014 QA Evidence — WinUI 3 Application

Date: 2026-10-24. Two verification tracks:

- **Managed/backend** — `dotnet fsi tools/sac-test-runner.fsx` byte-loads
  `FileMind.WinUI.Tests` (SAC blocks unsigned image loads from disk).
- **Runtime/UIA** — development-signed unpackaged build staged at
  `C:\tmp\fm-winui\`; screens inspected and driven via UI Automation
  (pattern invokes, value sets, nav selection). UIA *is* the accessibility
  tree, so the same probes double as a11y evidence.

## Backend smoke tests — 3/3 pass

`tests/FileMind.WinUI.Tests/BackendSmokeTests.cs` proves `FmBackend` (the UI
composition root, mirroring `Program.cs`) end-to-end:

| Test | Result |
|---|---|
| `Backend_Opens_Real_Store_And_Pipeline` | PASS — real SQLite store + pipeline |
| `Pipeline_Scan_Analyze_Plan_Execute_Undo_Roundtrip` | PASS — seeded subject/collection, move + undo verified on disk |
| `Backend_Search_And_Recent_Operations_Surface` | PASS — FTS5 search + operation records |

## Runtime evidence (UIA captures)

- **Shell**: custom title bar (Min/Max/Close), global `Search files…`
  AutoSuggestBox, `Operation center` button (announces by name), nav rail with
  all 9 destinations.
- **Dashboard**: bento renders live zero-state ("Indexed files 0", "Inbox 0",
  "No operations yet — organize something…").
- **Files**: filter box, Extension + Sort combos, `Index folder…`, truthful
  empty state ("Nothing indexed — index a folder to browse it here").
- **Organizer**: typed source → Analyze → "2 planned action(s), 2 mutating.
  Safety check passed." with per-item rows
  (`Move … → C:\tmp\fm-org-test\inbox\School\IT421\… · High-confidence
  classification (95).`). Execute → "Result | Completed | 2 item(s)
  completed." — files verified moved on disk. "Undo last run" → "Cancelled |
  Operation reversed — files are back where they were." — files verified
  restored on disk.
- **Search**: FTS5 surface + Kind filter; empty-index handled truthfully.
- **Devices**: real `DriveInfo` ("C:\ — 279.2 GB free of 475.9 GB"), Android
  "Unsupported — Planned" truthful row.
- **Tools**: per-tool truthful states (AI "Offline — Off by default", Android
  "Unsupported — Planned", others "Ready — Available").
- **Activity**: live operation records (3 real ops with ids/timestamps),
  items announce "Completed: 2 item(s) — 2 done — c60d391e… · 10/24/2026
  10:18 PM" (readable, not record literals).
- **Settings**: Theme=Dark selected live without error; "Organization mode:
  Manual" truthful host-owned surface.

## Errors & validation

- Analyze with empty source → "A field needs attention. Choose a source
  folder first. Fix it, then run the operation again." (specific, recovery —
  was generic "Something went wrong" before fix).
- `FmErrorPresenter` maps all `ErrorCode` values the pipeline emits; raw
  technical detail rides in `TechnicalDetails`, never the primary copy.

## Stability

- 27 rapid nav switches (3 rounds × 9 pages) + full operation flows —
  zero crashes after fixes.
- Pre-fix, navigating Files crashed (`ApplyFilters` NRE —
  `IsSelected="True"` fired `SelectionChanged` during `InitializeComponent`
  before list/summary fields existed). Fixed: default selected in ctor +
  null-guard. Crash logger captured the managed stack.

## Zero direct filesystem mutation

`grep -n "File\.|Directory\.|FileStream"` over `src/FileMind.WinUI/*.cs` —
the only `System.IO` touch is the crash-logger's temp diagnostic write
(`%TEMP%\filemind-winui-crash.txt`), which never touches user files.
Every user-file mutation flows through `OrganizerPipeline` /
`FileIndexingService` / repositories.

## UI_UX_QA_CHECKLIST rows

| Row | Verdict | Evidence |
|---|---|---|
| Light/dark/system themes | VERIFIED-code + switch | Both theme dictionaries exist; Dark selected live at runtime; pixel check pending |
| No color-only status | PASS | `FmStatusChip` = icon + text; `FmStateView` = icon + title + body |
| States (loading/empty/warning/error) | PASS | `FmStateView`/`FmOpState` model all required states; empty states verified live |
| Pre-checks before destructive work | PASS | plan preview + safety verdict gates Execute (disabled on unsafe/zero-mutating) |
| Truthful progress | PASS | Busy state is honest ("Executing…"), result counts come from real records |
| Cancel/pause defined | DOCUMENTED | mid-run cancel not surfaced — `ExecuteAsync` CT exists; UI offers Undo post-run |
| Error anatomy + recovery | PASS | validation error verified live; all kinds carry title+explanation+recovery |
| Keyboard nav / focus | PASS-native | NavigationView/Buttons/ListView native focus visuals |
| Icon-only button names | FIXED | `OpCenterButton` now has `AutomationProperties.Name` (verified in UIA tree) |
| Combo labels | FIXED | Settings combos use `LabeledBy` (announce "Theme"/"Density"); `Header` combos verify correctly |
| Readable list announcements | FIXED | `ToString` overrides on all row records |
| Reduced motion | PASS | no custom animations; `FmThemeService.ReducedMotion` reads OS `UISettings.AnimationsEnabled` |
| Resizing/min size | PASS-code | `OverlappedPresenter` min-size set in `MainWindow` |
| Context menu / multi-select / sort / inspector (Files) | PASS | `Extended` selection, `MenuFlyout` (Show in folder / Copy path), sort combo, details pane |
| Android section | N/A | Android UI deferred per PLAN-014 (owned by PLAN-010 tab) |

## SAC / packaging status

- Smart App Control (enforcement) blocks unsigned `FileMind.*.dll` image
  loads per-hash (CodeIntegrity 3077).
- Dev-signed binaries pass: `tools/fm-sign.ps1` signs all `FileMind.*` +
  apphost with a CurrentUser self-signed cert → signed build ran all UIA
  evidence above.
- MSIX installs fail `0x800B0109` — the cert root needs
  `Cert:\LocalMachine\Root` (admin). `Package.appxmanifest` + `makeappx`
  packaging itself verified working.
- **Supported dev path on this box**: signed unpackaged apphost (as used
  here). Distribution still needs a trusted cert.

## UNVERIFIED

- Pixel-level visual parity with the mockup (no screenshot comparison run).
- 200% text scaling / narrow-width reflow at runtime.
- Live Groq path in-app (no key configured — truthful offline states verified).
- Multi-day soak / real-folder organize beyond the two-file smoke set.

## Update 2026-10-25 — mockup-parity rebuild + SAC enforcement flip

**UI rebuilt to `mockup/` structure** (F-UI1 canonical): shell (brand mark,
op-center badge, density/theme actions, nav order), page-head pattern,
bento Dashboard (Storage span2 / Devices / Quick actions / Active ops /
Cleanup / Recent span2), Files + Search headered tables w/ chips +
inspector + selection bar, Devices card grid (meters, capabilities,
restriction badges), Activity (ops w/ per-row undo + Smart Inbox resolve +
states legend), Tools (grid w/ truthful badges + rule toggles persisted via
`IRuleRepository`), Settings (Appearance/File behavior/Permissions/
Notifications/Advanced/About). Typography ramp corrected to mockup
(H1 22/H2 16/H3 13). All on real backend data — no fake content.

**Backend regression**: `BackendSmokeTests` 3/3 pass post-rebuild.

**SAC enforcement flip (machine-level blocker, new)**: at ~22:20 dev-signed
DLLs passed per-hash verdicts and all prior UIA evidence was gathered;
from ~00:11 SAC (`VerifiedAndReputablePolicyState=1`, enforcement) denies
*every* `FileMind.*` managed image load — including previously-allowed
signed hashes and the WASDK-mandated full-extraction single-file bundle
(`%TEMP%\.net\…` extraction exposes managed images; `IncludeAllContent
ForSelfExtract=true` is required for Dll SxS redirection, so single-file
cannot hide them). Remaining remedies, both machine-policy decisions:

- `Import-Certificate C:\tmp\fm-dev-sign.cer -CertStoreLocation Cert:\LocalMachine\Root`
  (admin, one-time) → signed binaries meet "Enterprise signing level".
- Smart App Control → Off (Windows Security; one-way — cannot re-enable
  without reinstall).

New-code runtime evidence before the flip: the app launched, backend
opened, Dashboard storage/devices data loaded; then a page-level
`Resources["FmTextBodySmall"]` lookup (page dict ≠ app dict) crashed —
fixed to `Application.Current.Resources`. Full-page UIA pass on the new
UI is BLOCKED until the cert is machine-trusted or SAC is off.

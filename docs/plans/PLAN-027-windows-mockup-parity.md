# PLAN-027 — Windows (WinUI): mockup UI parity + organizer polish

Status: IMPLEMENTED — M1–M5 committed & pushed; M6 QA done (see Verification)
Related Feature: F-UI1 (mockup), F-UI3 (Windows app), F-P* (Windows backend)
Created: 2026-10-27
Updated: 2026-10-27

## Request / Goal

User (2026-10-27): "make the same plan of this but for the windows UI
also" — i.e. apply PLAN-025's discipline to the Windows app:

**Goal: the running WinUI app must match `mockup/index.html`
screen-for-screen, element-for-element** — the same contract
`docs/rules/MOCKUP_RULES.md` defines ("the shipped UI and the mockup must
remain visually equivalent").

Baseline differs from Android: the Windows backend is fully wired
(`FmBackend` composes real indexing, FTS5 search, hashing, AI opt-in,
duplicates, storage analysis, Smart Inbox, startup recovery), the app
builds clean (`dotnet build` → 0 errors/0 warnings, verified 2026-10-27),
and most screens already approximate the mockup. This plan closes the
remaining element/flow gaps — no dead-end repair needed.

Scope: `src/FileMind.WinUI` (pages, controls, shell). Out of scope:
backend/service changes (the pipeline already works), the `gallery`
developer page, `FileMind.Windows.Host` (console host is a separate app),
and the mockup's `console`/`architecture` surfaces (WinUI intentionally
has a component gallery instead — divergence kept, see W7).

## Repository Audit — verified state

### What's already at/near parity (no work)

- Shell: titlebar (brand, command box → Search, op-center badge with real
  counts, density toggle collapsing the rail, theme flyout) — matches
  `mockup/index.html`.
- Dashboard: bento grid (Storage span2 · Devices · Quick actions ·
  Active ops · Cleanup · Recent span2) + real DriveInfo/storage data.
- Tools: tool grid + rule list with persisted toggles + working
  Analyze/Dupes/Cleanup/Export-profile actions (all real services).
- Devices: real drive cards + meters + capabilities.
- Settings: theme/density/conflict-policy/notifications persisted via
  `ISettingsRepository`; mode + watched folders truthful.
- Dark theme keeps canonical brand primary `#2563EB` (no Android T1 bug).
- `OrganizerPage` already passes `destinationRoot` and runs the full
  pipeline — the organizer *works*.

### Element-level gaps (mockup → required WinUI counterpart)

**W1 — Organizer flow (largest gap).** Mockup `flows.js` is a 7-step
stepper (`source → scan → analyze → plan → safety → execute → result`);
the WinUI page is a flat 3-card form. Missing/weak:

- No step indicator at all.
- No **Dry run** toggle — `Analyze_Click` hardcodes `dryRun: false`;
  mockup ships a header checkbox and a dry-run result state with a
  "Run for real" path.
- No streaming scan output (mockup prints each scanned file with size +
  scope, then exclusion skips).
- No analyze results table — mockup shows name / kind / **confidence
  bar** / subject / project / decision chip with expandable evidence rows;
  WinUI jumps straight to a plan list with no per-file classification.
- Plan + safety are merged — mockup has a dedicated safety step with
  blocker/warning banners and a confirm gate ("Execute with warnings?").
- No execute progress, per-item log, or cancel; the page shows a busy
  state only.
- Destination convention differs: `destRoot = dest || source` organizes
  *in place* (`<source>/<template>`); mockup routes into
  `<source>\organized\<template>` — safer default, mockup parity.
- Result lacks the mockup's moved/skipped breakdown and "needs attention"
  guidance; no Smart Inbox routing feedback anywhere in the flow.

**W2 — Files.**

- Selection bar only offers Organize/Clear — mockup also shows
  Move/Copy/Rename (Move/Copy are honest-disabled in the mockup) and a
  validated **Rename dialog** (the backend has a real Rename operation —
  wire it, keep the mockup's character validation copy).
- Inspector lacks the mockup's confidence bar and expandable evidence
  (`<details>Evidence`) section.
- No rescan of the current scope (only "Index folder…").
- Decision column re-analyzes per filter change with no caching — visible
  cost on large indexes; cache per scope like the mockup's `analyzed()`.

**W3 — Activity.**

- No active-operation card (mockup shows live progress + current item).
- **Undo executes immediately, no confirmation** — mockup gates undo
  behind a confirm explaining fingerprint-checked reversal; real files
  move, so this gate is required.
- Inbox rows lack the mockup's "Send to {suggestedDest}" primary action —
  only generic Resolve exists.
- No "Retry failed" affordance for `NeedsAttention` ops.

**W4 — Search.**

- No scope chips (all/downloads/documents/desktop) — mockup has them;
  WinUI offers kind+size only.
- No result inspector (mockup rows are scannable only, so parity is a
  row click → details; keep it light).
- Result empty state doesn't offer "Clear all filters" recovery.

**W5 — Settings.**

- Missing "Default folder shown on open" select (mockup `st-scope`).
- Missing the About block's version/reset affordances (Windows version:
  show real version + DB path exists already — add app version row).
- Notification checkboxes persist but nothing consumes them — mark
  truthfully ("saved for the host/notification service").

**W6 — Dashboard (minor).**

- Quick actions lack "Preview a dry run" and "Open console host"
  (mockup links; WinUI can deep-link `organizer?dry=1` equivalent and
  optionally launch the host exe).
- Storage card is not clickable (mockup routes to files).

**W7 — Shell/IA divergence (recorded, keep).** Mockup's "Backend" section
(console + architecture) vs WinUI's "Developer → Component gallery".
Keep the gallery; do not port console/architecture — the real console
host exists as `FileMind.Windows.Host`.

## Milestones

- **M1 — Organizer flow parity (W1):** step indicator; dry-run toggle +
  dry-run result w/ "Run for real"; scan progress list; analyze results
  table (confBar + decision chips + expandable evidence); dedicated
  safety card w/ banners + confirm gate; execute progress + per-item log
  polling `OperationRecord` (same technique as Android M1) + cancel if
  the pipeline accepts a token; `<source>\organized` default destination;
  moved/skipped/inbox breakdown in the result.
- **M2 — Files (W2):** selection-bar Move/Copy (disabled-truthful) +
  Rename (real Rename op via plan, validated dialog); inspector confBar +
  evidence expander; rescan-scope button; decision cache per scope.
- **M3 — Activity (W3):** active-op card; undo confirmation dialog;
  inbox "Send to suggested destination" action (routes via a plan);
  retry-failed affordance or truthful removal.
- **M4 — Search (W4):** scope chips; clear-filters recovery; result row
  inspector (reuse Files inspector shape).
- **M5 — Settings + Dashboard (W5/W6):** default-folder select, app
  version row, notification honesty note; "Preview a dry run" quick
  action → Organizer with dry-run preselected; clickable storage card.
- **M6 — QA/docs:** `dotnet build` clean; per-metric parity checklist
  pass vs `mockup/index.html`; plan acceptance updated; commits per
  milestone pushed to `origin/main`.

## Acceptance criteria

1. Every mockup screen/element above maps to a WinUI counterpart or a
   recorded, intentional divergence.
2. Organizer exposes the full stepper incl. dry-run end-to-end and a
   confirm gate before mutating files.
3. `dotnet build` stays at 0 errors/0 warnings; no mockup element is
   faked — anything not backed by a service is labeled truthfully.
4. Where app and mockup conflict, the resolution is documented in this
   plan (as in PLAN-025's stronger-truth rule).

## Verification (2026-10-27)

- `dotnet build FileMind.WinUI` — **0 errors, 0 warnings** after every
  milestone (M1 `a298279`, M2 `26b6644`, M3 `7bf6252`, M4 `1dba5c5`,
  M5 `3161ade`; all pushed to `origin/main`).
- `dotnet test` — **environment-blocked**: WDAC ("An Application Control
  policy has blocked this file", `0x800711C7`) prevents xUnit from
  loading any test assembly built under `C:\wamp64`. OS-level policy,
  unrelated to code; tests remain UNVERIFIED on this machine.
- Deferred (recorded, not faked): Activity "retry failed" — the backend
  has no retry primitive and re-executing a plan would reprocess
  committed items; `NeedsAttention` ops already surface in Activity with
  guidance. Candidate for a follow-up plan.
- Mockup kept as source of truth; no mockup edits needed — all app
  changes converged toward the existing mockup elements.

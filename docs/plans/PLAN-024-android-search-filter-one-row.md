# PLAN-024 — Android: search + filters on one row

Status: IMPLEMENTED
Related Feature: F-UI1 (mockup), F-UI4 (Android app)
Created: 2026-10-26
Updated: 2026-10-26

## Request / Goal

User (2026-10-26): "make the filters and serch to be in one line row so to
save space."

Collapse the separate search-bar row and filter-control row into a single
row on the Android surfaces — mockup (`android.html`) and native app —
matching the mockup+native parity convention from PLAN-022/023.

## Repository Audit

### Verified Existing State

**Mockup — Files screen** (`android-screens.js` ~333-344): three stacked
rows — `.a-searchbar` (48px), `.a-ctl` (scope picker + rescan + Filters
button, 48px), `.a-ctl-sub` summary line.

**Mockup — Search screen** (~493-497): `.a-searchbar lg` row + `.a-ctl`
row holding only the Filters button.

**Native FilesScreen.kt** (85-108): `OutlinedTextField` row + `Row` with
`FilterButton` + summary `Text`.

**Native SearchScreen.kt** (50-75): same structure — field row, then
`FilterButton` + summary row.

`FilterButton` (`components/Sheets.kt:163`) is an `OutlinedButton`
(icon + "Filters" + active-count badge) — drop-in trailing row item.

### Existing Rules Found

- PLAN-022 constraint: zero `overflow-x`; controls must not clip at
  phone widths — a merged row must truncate, not overflow.
- ≥48dp targets stay (`min-height: 48px` already on all row controls).
- MOCKUP_RULES: mockup+native stay visually equivalent.

## Proposed Changes

### Milestone 1 — Mockup

- **Files**: one `.a-ctl` row —
  `[scope .a-pick][.a-searchbar grow][rescan][Filters]`. The separate
  `.a-searchbar` row is removed; `.a-pick` loses `grow` and gains
  `min-width:0` so its label truncates instead of overflowing.
- **Search**: `.a-ctl` = `[.a-searchbar.lg grow][Filters]`.
- `.a-ctl-sub` summary lines unchanged (already a thin text line).
- CSS: `.a-ctl .a-searchbar{flex:1;min-width:0}`;
  `.a-ctl .a-pick{flex:0 1 auto;min-width:0}`.

### Milestone 2 — Native

- `FilesScreen.kt`: `Row { OutlinedTextField(Modifier.weight(1f));
  FilterButton(...) }`; the active-filter summary `Text` renders below
  **only when `activeFilters > 0`** (the "Sort: Name" default line is
  noise — the button badge already shows the count).
- `SearchScreen.kt`: same merge; summary shown only when filters active
  (replaces the always-on "All kinds" line).

### Milestone 3 — QA + docs

- `node --check`; browser preview at phone width; `overflow-x` grep → 0.
- `gradlew :app:compileDebugKotlin` green.
- CHANGELOG entry; plan → IMPLEMENTED.

## Non-Goals

- No change to filter-sheet contents, scope picker behavior, rescan
  semantics, or the `.a-ctl-sub`/native summary copy.
- Windows mockup and Windows app untouched.
- No icon-button redesign of `FilterButton` (keeps text + badge).

## Files Expected to Change

`mockup/android-screens.js`, `mockup/android.css`,
`android/app/.../ui/FilesScreen.kt`,
`android/app/.../ui/SearchScreen.kt`, `CHANGELOG.md`, this plan.

## Edge Cases

- Very narrow widths: `.a-pick` label truncates to ellipsis; search
  field keeps `min-width:0` — worst case ~60px of field, still tappable.
- Long active-filter summary (native): remains a wrapping single line
  below the row, only when filters are active.
- Mockup Files rescan keeps its 48px icon button in the row (no label).

## QA Plan

- `node --check mockup/android-screens.js` clean.
- Preview: Files + Search render one control row at 400px and 320px
  widths; no clipping or h-scroll; both themes.
- `gradlew :app:compileDebugKotlin` → exit 0.

## Acceptance Criteria

- [x] Files + Search show search and filters on one row (mockup + native).
- [x] No horizontal overflow at 320px — `.a-pick` truncates
      (`min-width:0`, `max-width:45%`), searchbar `flex:1` shrinks; no
      `overflow-x` additions (grep → 0).
- [x] Filter sheets, scope picker, rescan all still work — ids/markup
      preserved; `node --check` clean; `:app:compileDebugKotlin` exit 0.

## Evidence (2026-10-26)

- `node --check mockup/android-screens.js` → clean
- `grep overflow-x mockup/android.css` → 0 (PLAN-022 invariant held)
- `gradlew :app:compileDebugKotlin --console=plain -q` → exit 0
- Local preview at `http://127.0.0.1:8741/mockup/android.html` refreshed
  for manual shell checks (visual confirmation UNVERIFIED by agent)

## Commit Plan

`fix(mockup): merge files/search controls into one row` →
`fix(android-app): search + filters single row` →
`docs(plans): PLAN-024 implemented` — each pushed to `origin/main`.

## Approval

Approved by user 2026-10-26.

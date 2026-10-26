# PLAN-026 — Android: launcher icon + splash background contrast fix

Status: IMPLEMENTED
Related Feature: F-B2 (brand assets), F-UI4 (Android app)
Created: 2026-10-26
Updated: 2026-10-26

## Request / Goal

User (2026-10-26): "fix the phone icons in the file mind app why is the bg
is blue if the file folder is blue fix this issues and use a good color
coding for it base on the codebase."

The adaptive launcher icon renders the brand-gradient folder mark on a
flat `#2563EB` background — the same blue the mark's gradient starts with.
The folder's left edge and tab disappear into the background
(blue-on-blue). The Android 12+ splash inherits the same pairing.

## Repository Audit

### Verified Existing State

- `android/app/src/main/res/values/colors.xml`:
  `ic_launcher_background=#2563EB` (brand `primary`).
- `android/app/src/main/res/drawable/ic_launcher_foreground.xml`: folder
  path filled with the brand gradient `#2563EB → #4F46E5 → #7C3AED` —
  **BROKEN**: start stop equals the background color.
- `android/app/src/main/res/values-v31/themes.xml`:
  `windowSplashScreenBackground=@color/ic_launcher_background` +
  `@drawable/splash_icon` (same gradient mark) — same blue-on-blue on the
  splash.
- `minSdk 26` → `mipmap-anydpi-v26` adaptive icon is what every device
  uses; the baked `mipmap-*dpi` PNGs are fallbacks only.
- **INCONSISTENT**: `colors.xml` (`#2563EB`) contradicts the pack's own
  sources — `branding/logo/android/ic_launcher_round.svg` and
  `ic_launcher_legacy.svg` fill the icon bg with `#F8FAFC`, and the baked
  `mipmap-*dpi/ic_launcher{,_round}.png` are light-background (verified
  visually). `splash-light.svg`/`splash-dark.svg` pair the mark with
  `#F8FAFC`/`#0B1120` respectively.
- Canonical tokens (`branding/colors.json`, mirrored in
  `android/.../theme/Color.kt` `BrandColors`): `light.background=#F8FAFC`,
  `dark.background=#0B1120`.
- Brand pack copies of the color: `branding/logo/android/colors.xml` and
  `branding/logo/android/res/values/colors.xml` (drop-in source).
- In-app file icons are unaffected: mockup `.fico` uses
  `--fm-surface-elevated` bg + colored text; native `FilesScreen` renders
  no file-type icon. No `values-night/` dir exists yet in either res tree.
- Mockup does not render the launcher icon (only `filemind-mark.svg` in
  the header); MOCKUP_RULES triggers on `branding/colors.{json,css}` edits
  — neither changes here.

### Classification

`BROKEN` — adaptive icon + splash foreground/background share a hue;
left half of the folder mark is invisible. Repair: repoint the
background color at canonical surface tokens (smallest responsible fix —
foreground mark stays untouched).

## Proposed Changes

### Milestone 1 — App resources

- `android/app/src/main/res/values/colors.xml`:
  `ic_launcher_background` → `#F8FAFC` (light surface, matches source
  SVGs + baked PNGs).
- New `android/app/src/main/res/values-night/colors.xml`:
  `ic_launcher_background` → `#0B1120` (dark surface) — icon + splash
  follow the system theme, per BRAND.md "recognizable in both".

### Milestone 2 — Brand pack (drop-in source parity)

- `branding/logo/android/res/values/colors.xml` → `#F8FAFC`.
- New `branding/logo/android/res/values-night/colors.xml` → `#0B1120`.
- `branding/logo/android/colors.xml` (loose reference) → `#F8FAFC`.
- `branding/logo/README.md`: update the `ic_launcher_background=#2563EB`
  mention to the theme-aware pair.

### Milestone 3 — QA + docs

- `gradlew :app:compileDebugKotlin` (or `:app:processDebugResources`)
  green — validates the new resource file.
- CHANGELOG entry; plan → IMPLEMENTED.

## Non-Goals

- No change to the mark's gradient, the monochrome/notification vectors,
  or the baked `mipmap-*dpi` PNGs (already light-bg; still correct for
  the legacy path).
- No `branding/colors.json` / `colors.css` edits → no mockup changes.
- No gradient-drawable background (would re-introduce same-hue overlap).

## Files Expected to Change

`android/app/src/main/res/values/colors.xml`,
`android/app/src/main/res/values-night/colors.xml` (new),
`branding/logo/android/res/values/colors.xml`,
`branding/logo/android/res/values-night/colors.xml` (new),
`branding/logo/android/colors.xml`,
`branding/logo/README.md`, `CHANGELOG.md`, this plan.

## Edge Cases

- Launchers that cache icons keep the last-fetched variant until refresh
  — acceptable; worst case the icon stays light, still readable.
- API 31+ splash at night: `values-night` resolves `#0B1120`, matching
  `splash-dark.svg` intent.
- Monochrome layer unaffected (system-tinted silhouette).

## QA Plan

- `gradlew :app:processDebugResources --console=plain -q` → exit 0
  (resource merge validates the new `values-night` file).
- Grep: no remaining `#2563EB` in `**/res/values*/colors.xml`.
- Manual (user-side): reinstall/refresh icon on device or emulator —
  folder silhouette reads fully in light and dark launcher themes;
  splash bg matches. UNVERIFIED by agent (no runtime).

## Acceptance Criteria

- [x] `ic_launcher_background` is `#F8FAFC` (default) / `#0B1120` (night)
      in app res and brand drop-in res.
- [x] Splash no longer renders the gradient mark on `#2563EB`.
- [x] Resource compile green; docs + CHANGELOG updated.

## Evidence (2026-10-26)

- `gradlew :app:processDebugResources --console=plain -q` → exit 0.
- Merged res verified: `merged.dir/values/values.xml` → `#F8FAFC`;
  `merged.dir/values-night-v8/values-night-v8.xml` → `#0B1120`.
- `grep #2563EB android/app/src/main/res` → only the explanatory
  comment; no color value remains.
- On-device visual confirmation UNVERIFIED — requires reinstall/launcher
  refresh on hardware or emulator (agent has no runtime).

## Commit Plan

`fix(android): launcher icon + splash background off mark hue` →
`docs(plans): PLAN-026 implemented` — each pushed to `origin/main`.

## Approval

Approved by user 2026-10-26 — theme-aware option (light `#F8FAFC` /
night `#0B1120`) selected.

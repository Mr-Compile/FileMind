# PLAN-016 — Android Branding Drop-in + Mockup Logo Integration

Status: COMPLETED (explicit user instruction: "ready the branding in the android first and update the html", 2026-10-24)
Related Feature: F-B2, F-UI1 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal
1. Turn `branding/logo/android/` into a drop-in Android `res/` layout a future Android Studio project can copy verbatim (VectorDrawable foreground/monochrome/notification/splash, `mipmap-anydpi-v26` adaptive XMLs, density PNG fallbacks incl. round + legacy + foreground, `values/colors.xml`, `values-v31/themes.xml` splash theme).
2. Wire the Option 4 logo into `mockup/` (canonical visual reference per `MOCKUP_RULES.md`): real mark in titlebar, favicon, and a theme-aware splash overlay shown on boot (simple/fast per `BRAND.md`, no fake progress).

## Verified State
- `branding/logo/` assets exist (`EXISTING` F-B2). Mockup complete (`EXISTING` F-UI1): `mockup/index.html` titlebar uses text "FM" `.brand-mark` chip (`styles.css:55`); boot sequence at `app.js` DOMContentLoaded → `FM.theme.apply()` … `FM.route()`. Theme = `[data-theme="dark"]` attr, persisted `localStorage.fm-theme`.
- Android uses VectorDrawable XML (not SVG) for adaptive-icon foreground/monochrome + `windowSplashScreenAnimatedIcon`; my existing android/ files are SVG — conversion required.
- Handoff gaps to fix: notification PNGs only cover 24–48 (need 72/96 for xx/xxxhdpi); no round/legacy variants; no `res/` structure.

## Implementation

### M1 — `branding/logo/android/res/` drop-in
- 1.1 `android/ic_launcher_round.svg` + `android/ic_launcher_legacy.svg` (circle / rounded-square brand bg + mark at 0.62) for baked PNG fallbacks.
- 1.2 VectorDrawables (hand-converted pathData, `aapt:attr` inline gradients, foreground group scale 0.62/pivot 54, monochrome `fillType="evenOdd"`): `res/drawable/{ic_launcher_foreground,ic_launcher_monochrome,ic_notification,splash_icon}.xml`.
- 1.3 `res/mipmap-anydpi-v26/{ic_launcher,ic_launcher_round}.xml`, `res/values/colors.xml` (`ic_launcher_background`), `res/values-v31/themes.xml` (`Theme.FileMind.Splash` API-31+ splash theme reference).
- 1.4 Extend `build.mjs` jobs: `mipmap-{mdpi..xxxhdpi}/ic_launcher.png` (legacy), `ic_launcher_round.png`, `ic_launcher_foreground.png` (108–432px), `drawable-*dpi/ic_notification.png` (24–96). Re-run; verify.

### M2 — Mockup branding
- 2.1 `index.html`: favicon `../branding/logo/png/icon-32.png`; `.brand-mark` → `<img src="../branding/logo/filemind-mark.svg">`; early-theme inline `<script>` (mirrors `FM.theme` resolution, kills theme flash before boot); splash overlay `#splash` (theme-aware lockup via two `<img>`s + CSS show/hide on `data-theme`, no progress bar) + `<noscript>` hide.
- 2.2 `styles.css`: brand-mark img rules; `#splash` fixed overlay, palette bg, fade-out class, `prefers-reduced-motion` instant-hide.
- 2.3 `app.js` boot: after `FM.route()` → hide splash (min ~500ms visible, 250ms fade, then `display:none`).

### M3 — Docs + QA + commits
- `branding/logo/README.md` (res/ usage), `FEATURES.md` F-B2 + F-UI1 notes, `CHANGELOG.md`, this plan's criteria.
- Commits: `feat(branding): android res drop-in`, `feat(mockup): real logo + splash`, `docs(*):`.

## Non-Goals
No Android Studio project/Gradle module (that's PLAN-010 scope), no mockup architecture changes, no new logo concepts, no splash progress indicators (brand rule: only real progress).

## QA
- VectorDrawable XML well-formed (`node`/XML parse on each).
- build.mjs re-run: all res PNGs correct density/size; `res/` tree complete.
- Mockup: `index.html` references resolve (paths exist over `file://`); splash hides post-boot; light/dark lockup variant honored; zero-console-error manual pass recorded honestly (browser check disclosed as manual/visual).

## Acceptance Criteria
- [x] `branding/logo/android/res/` copies into a Studio project unchanged (dir layout verified)
- [x] All density PNGs + vectors generated and verified (8 XMLs well-formed, 20 res PNGs asserted)
- [x] Mockup shows real mark in titlebar + favicon + splash on boot, both themes (markup/CSS verified; browser pass disclosed as manual)
- [x] Docs updated; commits scoped (other session's in-flight files untouched)

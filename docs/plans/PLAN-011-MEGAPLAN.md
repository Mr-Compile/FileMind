# PLAN-011 — FileMind Logo Asset Production Megaplan (Option 4 Selected)

Status: READY
Related Feature: F-B2 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal
User selected **Option 4 — "Folder + Mind"** (gradient folder mark + sparkle accent) from `branding/brand-boards/candidate-board-1.png`. Produce the production logo asset set per the pack handoffs: master SVG, transparent PNG size set, multi-resolution ICO + small-size simplified mark (Windows), adaptive icon layers + monochrome launcher + notification mark (Android), light/dark splash assets — stored under `branding/logo/` with a reproducible export script.

## Repository Audit
### Verified Existing State
- Canonical tokens exist: `branding/colors.json` (schemaVersion 1, brand gradient `#2563EB → #4F46E5 → #7C3AED`), `branding/colors.css` (full `--fm-*` coverage after `8865110`), `src/FileMind.Branding` + parity tests `EXISTING`.
- Board `candidate-board-1.png` shows Option 4: rounded blue→violet gradient folder (slightly offset double-folder silhouette) with a 4-point sparkle at lower-right; wordmark "FileMind" (File=white, Mind=blue→violet gradient); tagline "Smarter Files. A Cleaner You."
- Handoff checklists exist: `docs/packs/branding-palette/{windows,android}/HANDOFF.md`. Brand rules `branding/BRAND.md` (splash simple/fast, no gradient semantics, state never color-only).
- Tooling verified on this machine: no ImageMagick/Inkscape/ffmpeg; **Edge headless available** (`msedge --headless --screenshot --default-background-color=00000000` → transparent PNG); **node available** (ICO assembly script — ICO embeds PNG frames directly).
- `mockup/` exists (PLAN-003 in progress, untracked). Logo is not a listed mockup-sync trigger (rule lists contracts/pipeline/host/features/brand tokens/ui-ux intent); mockup unaffected.

### Findings
- Master logo artwork: `MISSING` (board is concept PNG only — SVG must be hand-recreated; approximate, not pixel-identical).
- All production exports: `MISSING`. Export tooling: `MISSING` (node + Edge headless suffices).
- F-B2 status: `PROPOSED` — unblocked by this selection.

## Constraints
- Logo mark uses brand tokens only: gradient `#2563EB → #4F46E5 → #7C3AED`, sparkle in accent `#7C3AED`/white. Neutral backgrounds from palette (`#F8FAFC` light, `#0B1120` dark).
- Assets are checked into `branding/logo/`; regeneration must be reproducible via `branding/logo/build.mjs` (node only, no npm deps, no network).
- No new runtime code, no changes to `src/` or `tests/`, no mockup changes.
- Adaptive-icon foreground respects safe zone (content inside central ~66dp circle of 108dp canvas).

## Implementation Phases

### M0 — Selection Record + Plan
- 0.1 Update `docs/features/FEATURES.md` F-B2 row: candidate selected (Option 4), status note updated.
- 0.2 Update `branding/README.md` known-gaps: selection recorded, deferral note replaced.
- 0.3 This plan. Commits `docs(features):`, `docs(branding):`, `docs(plans):`.

### M1 — Master SVG Artwork
- 1.1 `branding/logo/filemind-mark.svg` — icon only: overlapping rounded-folder silhouette, diagonal brand gradient, 4-point sparkle accent (lower-right, accent color), viewBox 0 0 108 108 matching adaptive-icon canvas.
- 1.2 `branding/logo/filemind-lockup.svg` — horizontal lockup: mark + "FileMind" wordmark (File in text color, Mind in gradient) for both themes (two files or `prefers-color-scheme` — decide at impl; simplest = two files `*-light.svg`/`*-dark.svg`).
- 1.3 `branding/logo/filemind-mark-mono.svg` — single-color silhouette variants (white for notification/dark-on-light) for monochrome launcher + notification-safe marks.
- 1.4 `branding/logo/filemind-mark-simple.svg` — small-size simplified variant (reduced sparkle detail) for ≤32px usage per Windows handoff.
- 1.5 `branding/logo/README.md` — asset inventory, provenance (Option 4 board), regeneration instructions, usage notes (small-size cutoff, monochrome contexts).

### M2 — Export Pipeline + Raster Assets
- 2.1 `branding/logo/build.mjs` — node script: locates `msedge.exe`, renders SVGs at required sizes to `branding/logo/png/` via headless screenshot (transparent bg), then assembles `branding/logo/filemind.ico` (16/24/32/48/64/128/256 PNG frames) by writing ICO container manually.
- 2.2 Windows set: `png/icon-{16,20,24,32,48,64,128,256,512}.png`, `png/icon-simple-{16,24,32}.png`, `filemind.ico`.
- 2.3 Splash: `png/splash-light.png` + `png/splash-dark.png` (1240×600, mark + lockup centered on palette bg — simple/fast per BRAND.md).
- 2.4 Android set (`branding/logo/android/`): `ic_launcher_foreground.svg` (mark in safe zone), `adaptive-icon.xml` (foreground+background), `ic_launcher_background` as brand primary color value doc'd in README (gradient-as-drawable alternative noted), `ic_launcher_monochrome.svg` (silhouette), `png/ic_launcher-{48..192}.png`, `png/notification-icon-{24,36,48}.png`, splash marks.
- 2.5 Run script, verify outputs. Commit `feat(branding): logo assets — Option 4 Folder+Mind`.

### M3 — Docs + QA
- 3.1 FEATURES F-B2 → `INCOMPLETE`/`EXISTING` based on verified outputs; `branding/README.md` + `branding/logo/README.md` final state; `CHANGELOG.md` entry.
- 3.2 QA evidence: XML validity, PNG dims, ICO header parse, visual compare vs board (approximation disclosed).

## Non-Goals
No XAML/Android project integration (no UI project exists), no animated splash, no store listing graphics, no favicon set beyond ICO/PNGs listed, no redesign — mark approximates board Option 4, no new concept work.

## QA Plan
- SVGs well-formed XML; open `filemind-mark.svg` in browser, compare against board region (visual).
- `node build.mjs` reproducible: re-run → byte-identical PNGs (or document nondeterminism from Edge version).
- ICO: parse header (reserved=0, type=1, count) + each frame decodes as PNG with declared dims.
- Adaptive icon: foreground within safe zone (manual check at 66dp circle overlay).
- Parity tests re-run (colors.css untouched by this plan).

## Acceptance Criteria
- [ ] `branding/logo/` contains master SVGs (mark, lockup light/dark, mono, simple) + README
- [ ] `build.mjs` produces full PNG set + `filemind.ico` + android dir, all verified
- [ ] Splash light/dark PNGs on palette backgrounds
- [ ] F-B2 status updated to verified level; CHANGELOG records verified outputs

## Risks
- Hand-recreated mark approximates concept art (disclosed; refine in follow-up if user wants changes).
- Edge headless screenshot API flags could shift across Edge versions — pin flags in script, document fallback (manual export via any browser).
- ICO hand-assembly: mitigate with parser check in QA.

## Commit Plan
`docs(features/branding/plans): logo selection record`, `feat(branding): logo master SVGs`, `feat(branding): export pipeline + raster assets`, `docs(changelog/features): F-B2 verification`.

## Approval
Pending user approval.

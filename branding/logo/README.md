# FileMind Logo Assets

Selected candidate: **Option 4 — "Folder + Mind"** (gradient folder + sparkle) from `branding/brand-boards/candidate-board-1.png`, chosen 2026-10-24. These masters are hand-recreated approximations of the concept art, built from brand tokens (`branding/colors.json`).

## Masters (SVG)
- `filemind-mark.svg` — app icon mark: overlapping folder + 4-point sparkle, brand gradient `#2563EB → #4F46E5 → #7C3AED`.
- `filemind-lockup-light.svg` / `filemind-lockup-dark.svg` — mark + "FileMind" wordmark for light/dark surfaces.
- `filemind-mark-mono.svg` — white silhouette (sparkle punched out) for monochrome/notification contexts.
- `filemind-mark-simple.svg` — simplified folder-only variant for ≤32px sizes.
- `splash-light.svg` / `splash-dark.svg` — 1240×600 splash compositions on palette backgrounds.

## Generated (run `node build.mjs`)
- `png/icon-{16..512}.png`, `png/icon-simple-{16..32}.png`, `png/splash-{light,dark}.png`
- `filemind.ico` — Windows multi-resolution icon (16–256px PNG frames)
- `android/png/ic_launcher-{48..192}.png`, `android/png/notification-icon-{24..48}.png`

## Android handoff (`android/`)
- `ic_launcher_foreground.svg` — mark inside 66dp adaptive safe zone (scale 0.62 on 108dp canvas)
- `ic_launcher_monochrome.svg` — themed-icon silhouette
- `adaptive-icon.xml` — `mipmap-anydpi-v26/ic_launcher.xml` reference
- `colors.xml` — `ic_launcher_background = #2563EB` (brand primary)

## Regenerating
`node branding/logo/build.mjs` — uses Edge headless screenshots (`--default-background-color=00000000` for transparency) and hand-assembles the ICO. No npm deps, no network. Edge path is auto-detected; without Edge, export manually from any browser at the listed sizes.

## Notes
- Sparkle = smart/AI accent usage per `BRAND.md`; keep it violet/white, never recolor to a semantic color.
- Use `icon-simple-*` below 32px — the sparkle detail muddies at small sizes.
- The board showed this as a raster concept; these vectors are the clean rebuild the pack README requested.

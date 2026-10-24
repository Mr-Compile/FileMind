# Changelog

All notable verified project changes should be recorded here. Do not add planned work as released work.

## 2026-10-24 — Branding Palette (PLAN-002)
- Extracted `docs/_zips/04_filemind-branding-palette-pack.zip` → `docs/packs/branding-palette/` and `03_file-organizer-ui-ux-foundation-pack.zip` → `docs/packs/ui-ux-foundation/` as intent sources.
- Added canonical token store `branding/` (`colors.json` with `schemaVersion: 1`, `colors.css`, `BRAND.md`, README, candidate board).
- Added `src/FileMind.Branding` (`BrandTokens`, `ThemeMode`, `ThemePalette`) — platform-neutral, no dependencies.
- Added `tests/FileMind.Branding.Tests` — 8 parity tests proving JSON↔C#↔CSS token equivalence, hex formats, light/dark key parity, palette resolution. All pass; full solution builds with 0 warnings/0 errors.
- Logo production assets remain blocked pending candidate selection (F-B2).

## 2026-10-24 — Logo Assets (PLAN-011)
- User selected **Option 4 "Folder + Mind"** from `branding/brand-boards/candidate-board-1.png`.
- Added `branding/logo/`: master SVGs (mark, light/dark lockups, mono silhouette, small-size simple variant, splash compositions) + `build.mjs` regenerator (Edge headless + hand-assembled ICO, no npm deps).
- Generated: `png/icon-{16..512}.png` + simple variants, `filemind.ico` (7 frames), `png/splash-{light,dark}.png` 1240×600, Android adaptive foreground/monochrome SVGs + `adaptive-icon.xml` + launcher/notification PNGs.
- Verified: PNG dims asserted per render, ICO header+frames parsed, visual check vs board (hand-recreated approximation disclosed).

# Changelog

All notable verified project changes should be recorded here. Do not add planned work as released work.

## 2026-10-24 — Branding Palette (PLAN-002)
- Extracted `docs/_zips/04_filemind-branding-palette-pack.zip` → `docs/packs/branding-palette/` and `03_file-organizer-ui-ux-foundation-pack.zip` → `docs/packs/ui-ux-foundation/` as intent sources.
- Added canonical token store `branding/` (`colors.json` with `schemaVersion: 1`, `colors.css`, `BRAND.md`, README, candidate board).
- Added `src/FileMind.Branding` (`BrandTokens`, `ThemeMode`, `ThemePalette`) — platform-neutral, no dependencies.
- Added `tests/FileMind.Branding.Tests` — 8 parity tests proving JSON↔C#↔CSS token equivalence, hex formats, light/dark key parity, palette resolution. All pass; full solution builds with 0 warnings/0 errors.
- Logo production assets remain blocked pending candidate selection (F-B2).

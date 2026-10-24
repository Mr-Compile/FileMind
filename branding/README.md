# FileMind Branding

Canonical brand token store. `branding/colors.json` is the single source of truth for all FileMind color tokens; platform code and tests consume it from here.

## Contents
- `colors.json` — canonical palette tokens (`schemaVersion: 1`): brand, light theme, dark theme, semantic.
- `colors.css` — CSS custom property mirror (`--fm-*`), light `:root` + `[data-theme="dark"]` overrides.
- `BRAND.md` — brand usage rules (blue dominant, indigo support, violet for smart/AI features, neutral surfaces, no gradients for semantics, never state-by-color-alone, System/Light/Dark themes).
- `brand-boards/` — concept artwork.

## Provenance
Imported from `docs/packs/branding-palette/` (pack `04_filemind-branding-palette-pack.zip`), which remains the intent source of record. `colors.json` here adds `schemaVersion: 1` per the repo's versioned-schema rule; all token values are verbatim from the pack.

## Known gaps
- The pack README claims two candidate boards; only `candidate-board-1.png` was shipped.
- `colors.css` dark overrides declare only `background`/`surface`/`text`; the remaining dark tokens (surfaceElevated, textSecondary, border) are defined in `colors.json` only.
- Logo candidate selected 2026-10-24: **Option 4 "Folder + Mind"** (gradient folder + sparkle). Production assets under `branding/logo/` pending `docs/plans/PLAN-011-MEGAPLAN.md` approval — F-B2.

## Code mirror
`src/FileMind.Branding/BrandTokens.cs` mirrors every token; `tests/FileMind.Branding.Tests/` proves parity with this file and `colors.css`. Edit `colors.json` first, then update the constants — the parity suite fails on drift.

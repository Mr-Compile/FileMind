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

## 2026-10-24 — Interactive UI Mockup (PLAN-003)
- Added `mockup/` — dependency-free static HTML/CSS/JS interactive mockup that opens over `file://`. No build, no external runtime deps.
- Screens: Dashboard, Files, Tools, Search, Devices, Activity (Operation Center + Smart Inbox), Settings — plus Organizer pipeline wizard, Console host simulator, and an Architecture view with EXISTING/MISSING module truth.
- Mock engine mirrors real semantics: Normalizer tokenization, FileKindMap/MimeMap, subject/project matchers (code 65 > alias 45 > keyword 15; project 55/12), per-kind confidence caps + 95/70 bands, DecisionMaker ordering (rule > band), ActionPlanner (Review→Ask items, fingerprints, undo recipes, KeepBoth suffixing), SafetyEngine blockers/warnings, staged TransactionEngine states.
- Console simulator reproduces `FileMind.Windows.Host` verbs and output format on the same mock dataset.
- `branding/colors.css` extended to all 18 tokens in both themes; mockup links it directly so palette cannot drift.
- Added `docs/rules/MOCKUP_RULES.md` (linked from `docs/rules/RULES.md` + `AGENTS.md`): mockup must be updated in the same change when UI, contracts, pipeline behavior, host verbs, feature statuses, or tokens change.
- Verified: 10 routes render over `file://` and HTTP, dark/light/system themes + density, KeepBoth conflict demo, protected-path safety blocker, Smart Inbox truthfulness (rule-routed files never appear in review).

## 2026-10-24 — Android res/ drop-in + mockup logo (PLAN-016)
- `branding/logo/android/res/` — drop-in Android resource tree: VectorDrawable foreground/monochrome/notification/splash, `mipmap-anydpi-v26` adaptive XMLs, density PNG fallbacks (legacy squircle, round, foreground 108–432, notification 24–96), `values/colors.xml`, `values-v31/themes.xml` splash theme. `build.mjs` extended (density jobs + Edge retry logic).
- `mockup/` — real Option-4 mark in titlebar + favicon; theme-aware splash overlay on boot (lockup light/dark, ~500ms + fade, no fake progress per BRAND.md); early-theme inline script prevents theme flash.
- Verified: all res XMLs well-formed, res tree complete, mockup asset paths resolve; PNG dims asserted per render.

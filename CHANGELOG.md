# Changelog

All notable verified project changes should be recorded here. Do not add planned work as released work.

## 2026-10-24 — Local File Intelligence (PLAN-007)
- `FileMind.Intelligence.Local` implemented (was empty scaffold): `IFileAnalyzer` contract, `ExtractionBudget` (byte/char/time caps), `AnalyzerRegistry`, `LocalAnalyzerService` implementing new `ICandidateEnricher` port (Domain.Ports). 8 BCL-only analyzers — zero external deps: plain-text/RTF-strip, OOXML (core.xml props + `w:t`/`a:t` runs via ZipArchive), PDF (Info literals + Tj/TJ incl. FlateDecode inflate), JPEG EXIF IFD0 + PNG IHDR/tEXt, ID3v2 + FLAC Vorbis comments, ISO-BMFF mvhd/tkhd (MP4/MOV), ZIP manifest (listed, never extracted), code language heuristics.
- `ClassifierEngine.Classify` gains optional `extraEvidence` + `contentTokens`: extracted text re-runs subject/project matchers at 0.35 weight (`content-*` evidence); filename wins conflicts with a warning; enrichment evidence capped at 25.
- `FileCandidate.Metadata`/`ExtractedTextRef` merged in `OrganizerPipeline.AnalyzeAsync` (optional `enricher` param); `ExtractedContent` rule conditions now work on real content.
- Smart rename: `{Title}`/`{Author}`/`{Year}` tokens in `ActionPlanner` resolve from extracted metadata (absent → `Unknown`/stem/current-year fallback).
- Host: `enrich <dir>` verb; host csproj references Intelligence.Local.
- Verified: 15 new tests (crafted DOCX/PDF-deflate/PNG/ID3/ZIP fixtures, corruption fail-safety, budgets, cap, content→subject promotion, smart rename); 55/55 Application suite; host e2e (`enrich`/`analyze` on real files). Gaps: MKV/AVI/ID3v1/OOXML-other-parts/encrypted-PDF/OCR remain MISSING.

## 2026-10-24 — Windows Automation (PLAN-005)
- Watch pipeline in `FileMind.Application/Watching`: normalized `WatchEvent`/`WatchEventKind`, bounded `WatcherEventQueue` (dedupe newest-wins + overflow flag), `DebounceService` (`IClock`-driven per-path windows), `FileStabilityChecker` (re-stat; `.crdownload/.part/.partial/.download/.tmp` never stable; still-changing detection), `WatchOrchestrator` (per-folder `ModePolicy` routing: Execute / Smart Inbox / Skip / index-only; Deleted → index removal; Error → rescan warning), `WatchService` drive loop.
- Windows adapters: `WindowsWatcherEventSource` (FileSystemWatcher, overflow→Error event), `WindowsRemovableStorageEvents` (DriveInfo polling), `ConsoleNotificationService`, `SystemClock`.
- `SqliteJobQueue` over `IJobRepository`: pending→running→done/failed; due events persist `watch-event` jobs before processing; RetrySafe failures reschedule with exponential backoff (max 3); `StartupRecoveryService` requeues `running` jobs on boot.
- Host verbs: `watch add|remove|list|run [--seconds N] [--apply]` + `watch-status`; per-folder mode persisted via `watch.mode.<path>` setting; drive attach/detach pauses/resumes watchers during `watch run`.
- Verified: 125 tests pass via `tools/sac-test-runner.fsx`; live E2E — drop file → organized → `[notify]` → Committed op; `.crdownload` untouched. Physical removable-drive attach/detach UNVERIFIED. Explorer integration remains MISSING (PLAN-014 scope).

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

## 2026-10-24 — PLAN-001 residual completion (PLAN-017)
- `FileIndexingService` + `file_index` schema v2 (`content_hash`, `hash_state`): scan/analyze now upsert fingerprints and SHA-256 hashes (64 MiB cap; `sha256:`/`sha256-partial:`); stable_id reused when a moved file's fingerprint matches.
- `StartupRecoveryService` runs on every host invocation: non-terminal ops with no applied items → `Failed`, partially applied → `NeedsAttention`; idempotent.
- `SmartInboxService` persists `Review`/`NeedsReview` decisions to `smart_inbox` deduped per open path; new `smart-inbox [resolve <id>]` verb.
- `OrganizationMode` + `ModePolicy` + `mode` verb: `manual` (preview until `--apply`), `smart` (confident → execute, rest → inbox), `automatic`, `ai-assisted`/`local-only` (AI no-op until PLAN-009); persisted via settings.
- `rules list|show|test <id|file> <dir>|import|export` verbs: import validates via `RuleValidator`, stamps `Provenance=Imported`, rejects newer `schemaVersion`.
- New: `tools/sac-test-runner.fsx` + `tools/sac-run.fsx` — byte-loading `AssemblyLoadContext` harness that runs the full xunit suite and the host under Smart App Control enforcement (unsigned-dll image loads are event-3077-blocked; byte loads are not). Documented in `docs/development/BUILD.md`.
- Verified: 108/108 tests pass via the runner (incl. 18 new residual tests); all host verbs exercised end-to-end on a real temp fixture (real SQLite, real move + undo); mockup console/architecture synced.

## 2026-10-24 — Android res/ drop-in + mockup logo (PLAN-016)
- `branding/logo/android/res/` — drop-in Android resource tree: VectorDrawable foreground/monochrome/notification/splash, `mipmap-anydpi-v26` adaptive XMLs, density PNG fallbacks (legacy squircle, round, foreground 108–432, notification 24–96), `values/colors.xml`, `values-v31/themes.xml` splash theme. `build.mjs` extended (density jobs + Edge retry logic).
- `mockup/` — real Option-4 mark in titlebar + favicon; theme-aware splash overlay on boot (lockup light/dark, ~500ms + fade, no fake progress per BRAND.md); early-theme inline script prevents theme flash.
- Verified: all res XMLs well-formed, res tree complete, mockup asset paths resolve; PNG dims asserted per render.

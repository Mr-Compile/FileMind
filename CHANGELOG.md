# Changelog

All notable verified project changes should be recorded here. Do not add planned work as released work.

## 2026-10-24 — Windows App (PLAN-014)
- `src/FileMind.WinUI` — native WinUI 3 desktop app (Windows App SDK 1.8, `net10.0-windows10.0.19041.0`, win-x64): canonical theme dictionaries (`Themes/FmTheme.xaml` light/dark/system + `FmTypography.xaml`), icon abstraction over Segoe Fluent, `FmBentoCard`/`FmStateView`/`FmStatusChip` controls (all `FmComponentState`/`FmOpState` contract states), `FmErrorPresenter` mapping every `ErrorCode` to friendly title+explanation+recovery, shell (custom title bar, nav rail, global search, operation center, breadcrumbs, min-size), component gallery.
- `FmBackend` composition root mirrors the host wiring — same SqliteStore, `OrganizerPipeline`, `SearchUseCase`, storage/duplicates, SmartInbox, `ModePolicy`, optional Groq advisor. All 9 destinations render live state; zero user-file mutation from UI (only a temp diagnostic crash-log write).
- Operation flow verified on disk via UI Automation: source→analyze→"2 planned actions, safety passed"→execute (files moved)→"Undo last run" (files restored). Files screen: sort/multi-select/context menu/inspector. Devices shows real `DriveInfo`; Android truthful "Planned".
- `tests/FileMind.WinUI.Tests`: `BackendSmokeTests` 3/3 via SAC byte-load runner.
- Fixes found by runtime QA: Files `ApplyFilters` init-order NRE (crash on first nav), icon-only op-center button lacked accessible name, Settings combos unlabeled (now `LabeledBy`), list rows announced record literals (now readable `ToString`), relative destinations resolved against process CWD (now organize-in-place), `Validation`/`TransientIo`/`PersistenceFailure` error codes fell through to generic copy.
- Smart App Control: unsigned `FileMind.*.dll` blocked per-hash; dev-signed apphost satisfies SAC (see `tools/fm-sign.ps1`). MSIX packaging verified; install needs machine-trusted cert (`0x800B0109`) — admin step, tracked in the plan.
- Mockup status badges synced to reality (F-P5/P6/P7/P10 no longer "Planned").

## 2026-10-24 — Android Backend (PLAN-010)
- `android/` Gradle project (wrapper-pinned Gradle 9.1.0, AGP 9.0.1 built-in Kotlin 2.2.10, KSP 2.3.6, compileSdk 36/minSdk 26, JBR 21 toolchain documented in `docs/development/BUILD.md`).
- `android/core`: faithful Kotlin port of the shared engine — contracts (`@Serializable`, `schemaVersion` on rules), normalizer/subject/project matchers/confidence bands/context-over-type bonus, rule engine (nesting ≤3, priority/specificity ordering, stop-processing, conflict detection, schema validation), safety engine, action planner (templates + `{RelativeDir}`/preserve-tree), transaction + undo + rollback, app pipeline (scanner/exclusions/mime/decision/mode/organizer). `Instant.utcTicks` matches .NET `UtcTicks` — golden fingerprint `400:8de541cd86c9000` asserted on both sides.
- `android/platform`: `AndroidFileSystemAdapter` — SAF `DocumentFile`/`DocumentsContract` (enumerate/stat/read/move/copy/rename/createDirectory/verify), MediaStore rows + `mediastore://` virtual folders (RELATIVE_PATH/DISPLAY_NAME, no explicit mkdir → `canCreateFolder=false`), app-private `file://`; `UriGrantStore` persisted-permission bookkeeping with revoke reconciliation; `AndroidCapabilityModel` + `GrantCoverage` + `SafPathNormalizer` + `AndroidProtectedPathProvider` (Android/data|obb|sandbox denied on every volume).
- Room `FileMindDatabase` v2 mirroring every repository port + `OperationStore`; `MIGRATION_1_2` adds `ai_usage`; schemas exported to `platform/schemas/`.
- WorkManager `ScanWorker`/`OrganizeWorker` (unique-work idempotent enqueue, battery/storage constraints, cooperative cancellation via `ensureActive`), `NotificationContracts` (operations/review/alerts channels) + `NotificationService` impl, headless `ShareIntakeActivity` → `ShareIntake` → Smart Inbox.
- Shared fixtures: `manifest.json` extended with an `exclusions` array; new `rules-parity.json`/`profile-parity.json`; Windows consumer `ManifestParityTests` added.
- Verified: `:core:test` 9/9 JUnit parity; `:platform:testDebugUnitTest` 18/18; `assembleDebug` + `compileDebugAndroidTestKotlin` green; Windows suites 96/96 + 208/208 unchanged. Instrumented (`MigrationTest`, `GrantAndAdapterTest`) compile; on-device run UNVERIFIED — no emulator attached. UI out of scope; no deletes; Groq-on-Android deferred.

## 2026-10-24 — Groq AI + Quota Budgeting (PLAN-009 + PLAN-020)
- `FileMind.Intelligence.AI` implemented (was empty scaffold): `GroqChatClient` transport (bounded timeout, retry honoring `retry-after`, 429 circuit breaker, auth-fail latch, `x-ratelimit-*` header sink, per-call usage-ledger write — never throws), `GroqProvider` `IAIProvider` (payload-hash `AiSuggestionCache` → `AiQuotaGate` pre-flight → response shape validation; advisory `AiSuggestion`), `AiPrivacyGate` (per-field `AiConsent`: metadata allowlist `ai.consent.metadata.keys` / `content.*` always stripped / extracted text opt-in `ai.consent.text` / protected paths never sent), `AiAdvisor` `IAiAdvisor` (only in `ai-assisted` mode + below-High confidence → merges capped `ai-signal` evidence, never overrides), `AiRuleDrafter` + `AiQueryInterpreter` (NL→rule JSON draft, NL→`SearchQuery`; advisory), `DotEnv`/`EnvSecretStore` (repo `.env` dev opt-in) + `WindowsSecretStore` (DPAPI `CryptProtectData` under `%LOCALAPPDATA%\FileMind\secrets`).
- PLAN-020 quota: `AiQuotaPolicy` + `AiQuotaGate` — daily (800 req/160k tok) + optional weekly + per-minute token-bucket budgets ~80% under published Groq free-tier limits, all via `ai.quota.*` settings; provider `x-ratelimit-*` headers reconcile counters down; per-request estimate cap; persisted `ai_usage` ledger (schema v4: `at`,`request_hash`,`model`,`purpose`,`prompt_tokens`,`completion_tokens`,`outcome` — no payload contents); cache hits spend zero quota; 429 → breaker + `retry-after` cooldown, auto-recover.
- Versioned `schemas/ai/ai-request.schema.json` + `ai-response.schema.json` (embedded in schema test suite).
- Pipeline: `OrganizerPipeline.AnalyzeAsync` accepts optional `IAiAdvisor` — rescored classification feeds rules+decision; null = deterministic-only.
- Host verbs: `ai-status`, `ai-usage [n]`, `ai-key set|clear|status` (DPAPI), `ai-suggest <file>`, `ai-rule <nl>`, `ai-search <nl>` (interpretation failure → honest plain-text fallback).
- Verified: 26 new `AiFeatureTests` + 4 AI `SchemaTests`; 195/195 suite via `sac-test-runner.fsx`; host e2e on real db (`ai-status` truthful off-by-default, `ai-suggest`→typed disabled error, `ai-search`→fallback). Live Groq call UNVERIFIED (no key on dev machine) — mocked-HTTP suites cover wire shapes. Mockup console simulates the new verbs.

## 2026-10-24 — Search (PLAN-015)
- Schema v3 migration: `file_content_index` FTS5 virtual table (`unicode61`; `path UNINDEXED`, `display_name`, `collection`, `subject`, `project`, `kind`, `content`) — bundled `e_sqlite3` includes FTS5.
- `SqliteStore` implements `ISearchIndex` (upsert/remove/escaped-`MATCH` search with bm25 rank); `file_index` delete clears the FTS row so watcher-driven removals stay consistent.
- `FileIndexingService` takes optional `ISearchIndex` — every scan/analyze populates FTS incrementally with `ExtractedTextRef` content (bounded, local-only).
- `FileMind.Application/Search`: `SearchQuery`/`SearchResultItem`/`SearchUseCase` — FTS over-fetch + payload post-filters (name/ext/kind/older/newer); user text regex-extracted to quoted terms (no operator injection).
- Host `search [text] [--name/--ext/--kind/--older-than/--newer-than/--limit]` verb; truthful empty state.
- Verified: 8 new `SearchTests` (migration, hits, filters, injection-safety, delete-consistency), 72/72 Application suite; e2e on real fixture. Existing indexes backfill FTS on next scan. NL interpretation deferred to PLAN-009 (retrieval has no provider dependency).

## 2026-10-24 — Duplicates & Storage (PLAN-008)
- New Domain contracts (`Duplicates.cs`): `DuplicateGroup`/`DuplicateFile` (per-file size+mtime for undo recipes), `SimilarityCandidate` (score + evidence reasons), `StorageReport`/`StorageBucket`/`StorageFileEntry`, `DuplicatesReport` (groups + similar + warnings + `TotalReclaimable`).
- `FileMind.Application/Duplicates`: `DuplicateDetectionService` — exact dupes via size prefilter → partial hash → full hash; index hashes reused when deep enough, files re-statted before hashing (size drift removes the stale row); over-`maxFullHashBytes` groups stay `Confirmed=false` partial-only and are never planned. Similarity candidates via same-extension + normalized-name token overlap (Jaccard) + size proximity, capped 50.
- `StorageAnalysisService` — kind buckets (`FileKindMap`), age bands (0-30/31-90/91-365/>365d), largest/oldest 10, duplicate-reclaimable estimate.
- `DuplicatePlanBuilder` — review-first quarantine `ActionPlan` (Move + `KeepBoth` + preconditions + `UndoRecipe`s, `RequiresReview`, confirmed groups only). No delete path anywhere.
- `IFileIndexRepository.GetAllAsync` added; `SqliteStore` explicit impl returns `payload_json` candidates (schema unchanged — V2 columns reused).
- Host verbs: `dupes [<prefix>] [--plan] [--apply] [--dest]` (report → optional SafetyEngine-validated quarantine preview → apply executes through `TransactionEngine`, undoable) and `storage [<prefix>]`.
- Verified: 9 new `DuplicatesTests`, 64/64 Application suite via `sac-test-runner.fsx`; live e2e — 3-way exact group detected (62 B reclaimable), similar pair with evidence, `--plan --apply` quarantined 2 copies, `undo` restored both, `history` recorded both ops. `mockup/flows.js` console simulates both verbs.

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

## 2026-10-24 — Cross-platform profiles (PLAN-012)
- `ProfileService` (`FileMind.Application.Profiles`): portable `organizer-profile.json` (schema v1) export, preview diff (`added`/`changed`/`same` per section+id), explicit-policy import (`merge` upserts all, `skip` keeps existing), timestamped `profile-backup-<ts>.json` before apply, and restore.
- `IExclusionRepository.AddAsync` added so profiles can import exclusion patterns.
- Platform-only settings are excluded end-to-end: export emits only `organization.mode` under `preferences`; imports ignore `settings` blobs (verified — `watch.mode.*` never applied).
- Malformed JSON, missing/<1/newer `schemaVersion` rejected before writes; backup+restore round-trip verified; import performs no user-file I/O.
- Verified: 119/119 Application tests pass via sac-test-runner (12 new `ProfileTests`); all `profile` verbs exercised end-to-end on a real DB (export → preview → apply → backup → restore); mockup console synced.

## 2026-10-24 — Preserve source folder tree on organize (PLAN-018)
- `IPathNormalizer.RelativeTo(path, root)`: `""` for root-equal, `null` when not under, case-insensitive on Windows; implemented by `WindowsPathNormalizer` + test normalizer.
- `PlanContext` gains `SourceRoot`/`PreserveTree`; `ActionPlanner` adds the `{RelativeDir}` destination-template token (resolves `""` in naming templates) and appends the source-relative directory inside the resolved category template when `PreserveTree` is set — category-then-tree layout. `OrganizerPipeline.Plan` forwards both via optional params; flat default unchanged.
- Host: `--preserve-tree` on `plan`/`execute`; watch: `watch add <dir> --tree`, `watch tree <dir> [on|off]`, `watch list` shows `tree=`, `Route()` threads owner root + flag through `WatchService`/`WatchJobPayload` to the orchestrator.
- Verified: `plan`/`execute --preserve-tree` produces `School\IT421\receipts\jan\f.pdf` on a real fixture, flat output identical without the flag, `undo` restores both files to their original subpaths; 16 new tests (RelativeTo, planner token/append, watch orchestrator + job-payload path); 224/224 pass via sac-test-runner; mockup console synced.

## 2026-10-24 — Backend QA hardening (PLAN-006 M0–M4)
- `AdapterContractTests`: one shared assertion suite run against both `FakeFileSystemAdapter` and `WindowsFileSystemAdapter` (enumerate, stat, open, exists, mkdir, move/copy/rename × all conflict policies, locked, free space, verify, protected paths). Found + fixed three fake divergences: `Ask` silently overwrote (now typed `DestinationConflict`), `CheckCapabilities` returned Ok for missing paths (now `SourceNotFound`), separator-literal path keys missed `/` vs `\` conflicts.
- `StressTests`: 10k-file index, 50k plan throughput, mid-index cancellation, db-reopen persistence, 10k-event watcher queue + debounce bursts. Exposed a real bottleneck — 10k index took 2m57s via per-statement autocommit; fixed with `PRAGMA journal_mode=WAL` + `synchronous=NORMAL` at `FileMindDb.Open`.
- `FailureInjectionTests`: permission-denied capability block, locked-file at validation and execute, source vanishing before/mid move, disk-full safety block, disposed-db typed `PersistenceFailure`, crash-boundary reconciliation (`Executing`→`NeedsAttention`, pristine→`Failed`) on real SQLite, simulated USB removal. `Fake.VanishOn` extended to Move/Copy/Rename for mid-op disappearance.
- Undo audit (M4): rename-undo round-trip via recipe verified; non-mutating ops (Tag/Ignore/Notify/Ask) commit without recipes; **known limitation** — committed `Copy`/`CreateFolder` have no undo path (no Delete op exists); `UndoService` surfaces this as NeedsReview, never silent.
- Verified: 294/294 tests pass (90 Domain + 196 Application + 8 Branding) via sac-test-runner outside docroot; no silent data-loss path known — see `docs/qa/QA_EVIDENCE_PLAN-006.md`.

## 2026-10-24 — Advanced intelligence M1–M3 (PLAN-013, activated scope)
- Correction learning: `corrections` table (schema v5) + `CorrectionService`; executed undos and `learn record <path> <dest>` feed `MineSuggestionsAsync` — ≥2 agreeing (ext, destination-dir) corrections emit a `Provenance=Learned`, **disabled** draft rule via `learn suggest`. Nothing auto-activates; enabled learned rules still pass `SafetyEngine` (verified).
- Adaptive confidence: `ConfidenceCalibrator` applies bounded ±5/signal, ±20-max `ext:<ext>` adjustments in `OrganizerPipeline.AnalyzeAsync`; executed undo records `Rejected`. Caps, bands, and review floors untouched; `learn calibration` inspects stored adjustments.
- Related/version intelligence: `RelatedFileService` detects `v(N)`, ` (N)`-copy, and year-version chains over the file index (unversioned same-base = order 0); `related [path]` prints chains — suggestion only.
- `RuleProvenance.Learned` added; mockup `learn`/`related` console verbs + help synced.
- Verified: 12 new `LearningTests` (208/208 Application suite); host `learn`/`related` exercised E2E on a real DB — draft rules disabled, chains marked latest-only.

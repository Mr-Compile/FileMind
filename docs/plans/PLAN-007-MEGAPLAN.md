# PLAN-007 — FileMind Local File Intelligence Megaplan (Backend Roadmap Phase 5 / PLAN-001 M6)

Status: IMPLEMENTED (verified 2026-10-24)
Related Feature: F-P5 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Verification Result

- M0–M3 implemented: `IFileAnalyzer`/`ExtractionBudget`/`AnalyzerOutput`, `AnalyzerRegistry`, `LocalAnalyzerService` (`ICandidateEnricher` in `Domain.Ports`), 8 analyzers (plain-text/RTF strip, OOXML core.xml+text runs, PDF Info+Tj/TJ incl. FlateDecode, JPEG EXIF IFD0 + PNG IHDR/tEXt, ID3v2 + FLAC Vorbis comments, ISO-BMFF mvhd/tkhd, ZIP manifest list-only, code language heuristics). Zero external dependencies — BCL only.
- Evidence → classification: `ClassifierEngine.Classify(candidate, extraEvidence, contentTokens)` — content tokens re-run subject/project matchers at 0.35 weight (`content-*` evidence kinds), filename wins on conflict (warning → NeedsReview); enrichment evidence capped at 25 total.
- Smart rename: `{Title}`/`{Author}`/`{Year}` template tokens resolve from extracted metadata in `ActionPlanner.ResolveTemplate`; absent metadata falls back (`Unknown`/stem/current year) — metadata presence IS the reliability gate.
- Host: `enrich <dir>` verb; pipeline wired with `LocalAnalyzerService` as `enricher` in `OrganizerPipeline` (optional — null = pre-PLAN-007 behavior).
- Tests: 15 in `tests/FileMind.Application.Tests/LocalIntelligenceTests.cs` (hand-crafted DOCX/PDF-deflate/PNG/ID3/zip fixtures, corrupt fail-safety, budget truncation, registry, service merge+cap, pipeline content-subject match, smart rename). 55/55 Application suite via SAC runner.
- Host e2e verified: `enrich` shows ID3 title/author, OOXML corrupt warning, 55-char txt extraction + `content-subject-code` evidence; `analyze` promotes `.txt` to subject=IT421 via content.
- Gaps (honest): MKV/WebM/AVI/WAV metadata parsing, ID3v1, OOXML headers/footers beyond `w:t`/`a:t` runs, encrypted-PDF handling (flagged unsupported), OCR — all MISSING/out-of-scope.

## Request / Goal

Implement `docs/packs/backend-roadmap/05-file-intelligence/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M6): plugin-style analyzers returning normalized metadata/content evidence — PDF, DOCX, PPTX, TXT/RTF, spreadsheet, EXIF/image, audio, video, archive-manifest, code-file signals — bounded by size/time limits, fail-safe on corrupt input, feeding richer classification and (gated) smart rename.

## Repository Audit

### Verified Existing State

- `src/FileMind.Intelligence.Local/` project `EXISTING` — empty scaffold only (no source files beyond generated obj). Same for `FileMind.Intelligence.AI` (PLAN-009's target).
- Ports `EXISTING`: `IContentExtractor`, `IMetadataExtractor` (`src/FileMind.Domain/Ports/IFileSystemAdapter.cs`); `Evidence` on `ClassificationResult`.
- Pipeline `EXISTING`: `OrganizerPipeline` → classifier consumes evidence; naming-template resolution in `ActionPlanner`.
- `MISSING`: analyzer interface + registry, all extractors, extraction evidence → classifier wiring, content-classification, smart rename templates, media/screenshot recognition.

### Evidence / Relevant Files

- Spec: `05-file-intelligence/PLAN.md`; contracts `01-core-engine/contracts/classification-result.md`; routing `01-core-engine/rules/` context-over-type behavior.
- `src/FileMind.Domain/Classification/*`, `src/FileMind.Domain/Operations/ActionPlanner.cs`.

### Documentation vs Code Differences

None — untouched intent.

## Findings

- Port surface + project scaffold: `EXISTING`.
- All extractors, registry, evidence wiring, smart rename: `MISSING`.

## Global Constraints (from pack + PLAN-001)

Extraction is local by default; bounded by file-size/time budgets; corrupt/unsupported files fail safely without blocking the scan; parsers remain replaceable (plugin-style, no hard dependency bloat into Domain); smart rename only after extracted evidence is proven reliable; no AI/provider dependency in filesystem execution; extracted content never logged raw.

## Proposed Changes / Implementation Phases

### Milestone 0 — Analyzer Contract + Registry

Exit gate: `IFileAnalyzer` contract defined in `FileMind.Intelligence.Local`; registry resolves analyzers by FileKind/extension; fakes produce evidence end-to-end.
- 0.1 `IFileAnalyzer` (bounded `Analyze(FileCandidate, ExtractionBudget)` → `AnalysisEvidence`), `ExtractionBudget` record (max bytes, max ms), `AnalyzerRegistry`.
- 0.2 Failure taxonomy mapped to existing `ErrorCode` + non-fatal evidence (`AnalyzerFailed` warning, scan continues).

### Milestone 1 — Document + Code Extractors

Exit gate: PDF/DOCX/PPTX/TXT/RTF/spreadsheet extractors return normalized text/metadata within budget on fixture corpus.
- 1.1 Text extractors: PDF (bounded page/text extraction), DOCX/PPTX (XML text + core properties), TXT/RTF, spreadsheet metadata + limited text.
- 1.2 Code-file signals (language, heuristics) — complements `MimeMap`.
- 1.3 Package dependency decision per repo rules (vetted, ≥7-day-old versions; prefer format-level readers over heavy suites). Each dependency justified in plan comments/PR body.

### Milestone 2 — Media + Archive Extractors

- 2.1 EXIF/image metadata, audio tags, video metadata (duration/resolution/container), archive manifest metadata (entries/count — never extract archives to disk).
- 2.2 Screenshot/media recognition signals where metadata supports it (F-P5 scope).

### Milestone 3 — Evidence → Classification + Smart Rename

Exit gate: extracted evidence raises/reduces confidence correctly; smart rename gated by evidence-reliability flag.
- 3.1 `AnalysisEvidence` → `Evidence` mapping into `ClassifierEngine`/`ConfidenceEngine` (weighted, capped, never overrides safety).
- 3.2 Naming-template tokens from extraction (`{Author}`, `{Year}`, `{Title}` etc.) resolved in `ActionPlanner` — only when reliability threshold met.
- 3.3 Smart-rename use case + host verb surface.

### Milestone 4 — Hardening + Docs

- 4.1 Corrupt/truncated/password-protected/oversized fixtures → fail-safe per-file, scan completes.
- 4.2 Budget enforcement tests (time/size cutoffs).
- 4.3 F-P5 status update, MODULES.md entry, CHANGELOG.

## Non-Goals

Cloud/AI extraction, OCR beyond declared extractor scope, mutating files during extraction, semantic embeddings (PLAN-013).

## Files Expected to Change

`src/FileMind.Intelligence.Local/**`, `src/FileMind.Domain/Classification/*` (evidence wiring), `src/FileMind.Application/**` (use case), `src/FileMind.Windows.Host/Program.cs`, csproj dependency additions (justified), docs.

## New Files Required

Extractor implementations, analyzer fixtures (small corpus additions under `tests/fixtures/`), contract tests.

## Security / Privacy Impact

No network. Extracted text stays in-process/db-local; audit/log redaction rules apply; archives never extracted to disk.

## Error Handling / Edge Cases

Corrupt → typed failure evidence; budget exceeded → partial evidence flagged; unsupported → analyzer absent (no error); huge files skipped by size cap; password-protected → typed result.

## QA Plan

Per-extractor unit tests on fixtures + synthetic corrupt files; budget enforcement; E2E analyze→classify→rename-plan on corpus; contracts suite consistency; boundary grep (extractors never mutate).

## Acceptance Criteria

- [ ] Analyzer registry + budgets enforced (size/time)
- [ ] Each declared extractor returns normalized evidence on fixtures
- [ ] Corrupt/unsupported inputs fail safe; scans never block
- [ ] Extracted evidence changes confidence/classification measurably (tests)
- [ ] Smart rename emitted only at reliability threshold
- [ ] F-P5 status reflects verified state

## Risks / Regression Areas

Native/binary parser deps on Windows → pin vetted versions; extraction time blowups → hard budgets; false evidence inflating confidence → caps + review bands preserved.

## Documentation Updates

`FEATURES.md` F-P5, `MODULES.md` (`FileMind.Intelligence.Local`), `DATA_FLOW.md` evidence stage, `CHANGELOG.md`.

## Commit Plan

`feat(intelligence-local): analyzer contract + registry` → `feat(intelligence-local): document/code extractors` → `feat(intelligence-local): media/archive extractors` → `feat(domain|application): evidence wiring + smart rename` → `test(*):` → `docs(*):`.

## Approval

User approval required before implementation.

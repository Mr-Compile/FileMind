# PLAN-002 — FileMind Branding Palette Megaplan (Branding Pack + UI/UX Foundation Pack)

Status: APPROVED
Related Feature: F-B1, F-B2, F-UI1 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal
Unzip `docs/_zips/04_filemind-branding-palette-pack.zip` and implement it in the codebase: extract the branding pack and the UI/UX foundation pack (03) as intent sources under `docs/packs/`, adopt the palette as canonical tokens in a top-level `branding/` directory, mirror tokens as platform-neutral C# constants in a new `FileMind.Branding` project with parity tests, and update affected documentation. Logo production assets are deferred pending candidate selection. No WinUI/XAML or Android resources are generated — no UI project exists yet.

## Repository Audit
### Verified Existing State
- .NET 10 backend only: `FileMind.{Domain, Application, Infrastructure.Sqlite, Platform.Windows, Windows.Host}` + 4 test projects in `FileMind.slnx`. No UI project; WinUI deferred per PLAN-001 non-goals, Android is M8.
- `docs/packs/` held `foundation-pack/` + `backend-roadmap/` only; packs 03 and 04 were unextracted in `docs/_zips/`.
- Working tree contains uncommitted M3 backend work (modified `ActionPlanner.cs`, `Program.cs`, new `Platform.Windows`/`Infrastructure.Sqlite` sources). Branding commits must not sweep these in.

### Pack 04 Contents (verified from zip)
- `palette/colors.json` — 18 tokens: brand primary `#2563EB`, secondary `#4F46E5`, accent `#7C3AED`, 3-stop gradient; light {background `#F8FAFC`, surface `#FFFFFF`, surfaceElevated `#F1F5F9`, textPrimary `#0F172A`, textSecondary `#64748B`, border `#E2E8F0`}; dark {background `#0B1120`, surface `#111827`, surfaceElevated `#1E293B`, textPrimary `#F8FAFC`, textSecondary `#94A3B8`, border `#334155`}; semantic {success `#16A34A`, info `#0284C7`, warning `#D97706`, error `#DC2626`, destructive `#B91C1C`}.
- `palette/colors.css` — `--fm-*` custom properties; light `:root` + `[data-theme="dark"]` overriding only background/surface/text.
- `guidelines/BRAND.md` — brand rules: blue dominant, indigo support, violet for smart/AI features, neutral surfaces, no gradients for semantics, never state-by-color-alone, System/Light/Dark themes, fast simple splash with honest progress.
- `brand-boards/candidate-board-1.png` — 5 logo candidates + splash/icon concepts. Concept artwork, not production assets.
- `windows/HANDOFF.md`, `android/HANDOFF.md` — asset checklists gated on logo selection.
- `README.md`.

### Existing Rules Found
`AGENTS.md` (audit→plan→approve→implement→QA→docs→auto-commit), `docs/rules/RULES.md` (fact labels, source-of-truth, change discipline), `docs/plans/README.md` (statuses; agent may set DRAFT/READY, user approved this plan).

### Documentation vs Code Differences
Pack README claims "Two generated branding/candidate boards"; zip contains exactly one PNG (`candidate-board-1.png`). Recorded as `INCONSISTENT`; no second board fabricated. `colors.css` dark block overrides only 3 of the 6 dark theme tokens (background/surface/text) — asymmetry noted; parity tests compare only CSS-declared props.

## Findings
- Brand intent (palette, rules, handoffs): `EXISTING` in pack → extracted as `docs/packs/branding-palette/`.
- UI/UX foundation intent (pack 03): `EXISTING` in zip → extracted as `docs/packs/ui-ux-foundation/` (intent only, no implementation).
- Canonical token source: `MISSING` → `branding/` created.
- C# token representation: `MISSING` → `src/FileMind.Branding/` + `tests/FileMind.Branding.Tests/`.
- Logo production assets: `MISSING`, blocked on user candidate selection → follow-up plan.
- Pack README board count: `INCONSISTENT` (claims 2, ships 1).

## Global Constraints
- `branding/colors.json` is the single source of truth; code constants must be proven equal by tests.
- `FileMind.Branding` is platform-neutral: no OS APIs, no UI framework references, no package dependencies.
- Versioned schema rule applies: canonical `colors.json` carries `schemaVersion: 1`.
- Commits scoped to branding/docs files only; in-flight M3 changes stay unstaged.
- Brand rules (BRAND.md) are documentation intent; enforced at UI implementation time (future plan).

## Proposed Changes / Implementation Phases

### Milestone 0 — Pack Extraction + Plan
Exit gate: pack dirs present, file trees match zip listings.
- 0.1 Extract 04 → `docs/packs/branding-palette/` (wrapper stripped).
- 0.2 Extract 03 → `docs/packs/ui-ux-foundation/` (wrapper stripped).
- 0.3 This megaplan file.

### Milestone 1 — Canonical Token Store (`branding/`)
Exit gate: `branding/colors.json` parses; README states provenance + deferrals.
- 1.1 `branding/colors.json` — pack tokens verbatim + `schemaVersion: 1`.
- 1.2 `branding/colors.css` — verbatim.
- 1.3 `branding/BRAND.md` — verbatim guidelines.
- 1.4 `branding/README.md` — canonical-source statement, provenance, deferred logo, board-count discrepancy note.
- 1.5 `branding/brand-boards/candidate-board-1.png` — verbatim.

### Milestone 2 — C# Tokens + Parity Tests
Exit gate: `dotnet build` + `dotnet test` green including new parity suite.
- 2.1 `src/FileMind.Branding/` (net10.0, nullable, implicit usings, zero deps): `BrandTokens` (nested `Brand`, `Light`, `Dark`, `Semantic` consts + `Brand.Gradient`), `ThemeMode` enum, `ThemePalette` record, `Palette(ThemeMode)` (System→Light; real OS detection is a UI-layer concern). Added to `FileMind.slnx`.
- 2.2 `tests/FileMind.Branding.Tests/` (xunit): JSON↔C# parity for all tokens + gradient order; `^#[0-9A-Fa-f]{6}$` format; light/dark key parity; `Palette()` correctness; CSS↔JSON parity for declared props; `schemaVersion == 1`. Token files included as linked content `CopyToOutputDirectory`. Added to `FileMind.slnx`.

### Milestone 3 — Documentation
- `FEATURES.md`: F-B1 `EXISTING` (verified), F-B2 `PROPOSED` (blocked on logo selection), F-UI1 intent-only.
- `docs/architecture/MODULES.md`: `FileMind.Branding` module entry.
- `README.md` pointer, `CHANGELOG.md` entry, QA evidence.

## Non-Goals
WinUI project/XAML theme dictionaries, Android resources, splash/icon implementation, logo asset production (blocked on candidate selection), implementing pack 03 content (reconciling its `foundations/02-design-tokens.md` with `branding/colors.json` is a future UI plan), changes to in-flight M3 backend files.

## Files Expected to Change
`FileMind.slnx`, `docs/features/FEATURES.md`, `docs/architecture/MODULES.md`, `README.md`, `CHANGELOG.md`.

## New Files Required
`docs/packs/branding-palette/**`, `docs/packs/ui-ux-foundation/**`, `docs/plans/PLAN-002-MEGAPLAN.md`, `branding/{colors.json,colors.css,BRAND.md,README.md,brand-boards/candidate-board-1.png}`, `src/FileMind.Branding/**`, `tests/FileMind.Branding.Tests/**`.

## Security / Privacy Impact
None — static constants and documentation only. No network, secrets, or filesystem mutation.

## Error Handling / Edge Cases
Parity test fails build on token drift; malformed hex rejected by format test; dark CSS partial-override asymmetry documented.

## QA Plan
`dotnet build` + `dotnet test` green; parity suite proves JSON↔C#↔CSS equivalence; verify `FileMind.Branding` compiles with no package references; QA checklist applied.

## Acceptance Criteria
- [x] Packs extracted, trees match zip contents
- [ ] `branding/colors.json` parses, `schemaVersion: 1`, all 18 tokens + 3 gradient stops
- [ ] `dotnet build`/`dotnet test` green incl. parity suite
- [ ] `FileMind.Branding` has no package/OS-API references
- [ ] FEATURES.md statuses accurate; logo deferred explicitly

## Risks / Regression Areas
Token drift → parity tests; css/json asymmetry → documented + test compares declared props only; schemaVersion deviation from pack copy → documented; unrelated M3 changes → scoped staging.

## Commit Plan
`docs(packs):`, `docs(plans):`, `feat(branding):`, `test(branding):`, `docs(features|architecture|readme|changelog):`. No pushes.

## Approval
Approved by user 2026-10-24 via plan-mode approval.

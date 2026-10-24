# Mockup Sync Rules

Origin: explicit user instruction (PLAN-003, approved 2026-10-24).

## Canonical Role

`mockup/` is the canonical visual reference of the approved FileMind UI until a real UI project (WinUI/Android) ships. It renders intent from `docs/packs/ui-ux-foundation/` with canonical tokens from `branding/`. When a real UI exists, the shipped UI and the mockup must remain visually equivalent; differences must be resolved, not silently accepted.

## Sync Requirement

Any change to the following MUST update `mockup/` in the same change/commit:

- Domain contracts or enums (`src/FileMind.Domain/Contracts/*`) — mock data field names mirror them.
- Pipeline behavior (`OrganizerPipeline`, engines, safety/decision semantics).
- Console host verbs, flags, or output formats (`src/FileMind.Windows.Host/Program.cs`).
- Feature statuses (`docs/features/FEATURES.md`) — implemented features become interactive; the mockup must never present an unimplemented feature as working.
- Brand tokens (`branding/colors.json`, `branding/colors.css`).
- UI/UX intent (`docs/packs/ui-ux-foundation/`).

A mockup that disagrees with code or approved intent is `INCONSISTENT` and must be repaired, not worked around.

## Technical Rules

- Static, offline, dependency-free: HTML + CSS + vanilla JS only. No CDN, frameworks, bundlers, or network calls. Must run from `file://`.
- Tokens: link `branding/colors.css` directly; never copy palette hex values. Extended CSS variables must cite `branding/colors.json` as their source.
- Mock data uses exact contract field names; every mock entity is annotated with the contract file it mirrors.
- Truthful states: unimplemented features are disabled with a status badge (`Missing`/`Planned`); no fake progress; no color-only status; friendly error copy per `ux/06-user-friendly-errors.md`.
- The mockup never mutates real files; simulated execution mutates in-memory mock state only and is labeled as a demo.

## Verification

For any UI-relevant change, manual verification is part of QA: open `mockup/index.html`, walk affected screens in both themes, run the console simulator verbs, confirm zero browser console errors. Record what was checked (and what could not be run) per `docs/qa/QA.md`.

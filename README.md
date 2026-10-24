# FileMind

> Offline-first, privacy-first, reversible smart file organizer for Windows and Android. Backend-first implementation.

## Purpose
FileMind organizes files locally using deterministic rules and classification. Optional Groq AI may suggest, but only the deterministic `Decision -> Safety -> ActionPlan -> Platform Adapter -> Verify -> History` pipeline may mutate files. See `docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/` for the approved product and backend intent.

## Status
Approved intent documented; implementation in progress under `docs/plans/PLAN-001-MEGAPLAN.md`. Check `docs/features/FEATURES.md` for per-feature status labels.

## Documentation
- `AGENTS.md` — mandatory AI-agent workflow and repository truth rules.
- `docs/packs/foundation-pack/` — approved product/architecture/safety intent (source pack).
- `docs/packs/backend-roadmap/` — approved backend implementation intent (contracts, engines, phases, test matrices).
- `docs/product/` — purpose, users, scope, terminology.
- `docs/rules/` — non-negotiable project rules.
- `docs/architecture/` — verified system architecture (updated as code lands).
- `docs/features/` — canonical feature specifications and statuses.
- `docs/plans/` — plan-before-code documents.
- `docs/qa/` — QA and acceptance validation.
- `docs/development/` — build, contribution, release, troubleshooting.

## Development Principle
**Audit → Plan → Approve → Implement → QA → Repair → Document → Commit.**

Do not describe proposed or planned functionality as implemented. The codebase must be inspected to establish actual behavior.

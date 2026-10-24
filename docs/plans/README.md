# Plans

All plans live directly in this folder. Do not create status subfolders.

## Plan Before Code
For meaningful changes, audit first and create/update a plan before implementation. The user must explicitly approve implementation. An AI agent may create `DRAFT` or `READY` plans but MUST NOT self-assign `APPROVED`.

## Allowed Statuses
`DRAFT`, `READY`, `APPROVED`, `IN_PROGRESS`, `BLOCKED`, `COMPLETED`, `CANCELLED`.

## Important
A plan describes intended work. It is never evidence that the work exists or was completed. Verify the codebase.

## Pack → Plan Coverage
Every zip in `docs/_zips/` maps to at least one megaplan. Verified 2026-10-24.

| Zip | Extracted pack | Plan(s) | Plan status |
|---|---|---|---|
| `00_file-organizer-docs-foundation.zip` | repo docs scaffold (`AGENTS.md`, `docs/*`) | PLAN-004 | COMPLETED (retrospective) |
| `01_FileMind_Foundation_Pack.zip` | `docs/packs/foundation-pack/` | PLAN-001, PLAN-008 (F-P6) | APPROVED / READY |
| `02_FileMind_Complete_Backend_Roadmap.zip` | `docs/packs/backend-roadmap/` | PLAN-001 (M0–M3 done), PLAN-005 (automation), PLAN-006 (QA), PLAN-007 (intelligence), PLAN-009 (Groq), PLAN-010 (Android), PLAN-015 (search), PLAN-012 (cross-platform), PLAN-013 (advanced) | APPROVED / READY |
| `03_file-organizer-ui-ux-foundation-pack.zip` | `docs/packs/ui-ux-foundation/` | PLAN-002 (extraction), PLAN-003 (mockup), PLAN-014 (WinUI app) | APPROVED / READY / READY |
| `04_filemind-branding-palette-pack.zip` | `docs/packs/branding-palette/` | PLAN-002 (tokens, done), PLAN-011 (logo assets — Option 4 selected) | APPROVED / READY |

Numbering note: PLAN-011 is the logo-asset plan (written during a concurrent session after the user selected logo Option 4); the search megaplan occupies PLAN-015.

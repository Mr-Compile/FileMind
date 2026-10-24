# Plans

All plans live directly in this folder. Do not create status subfolders.

## Plan Before Code
For meaningful changes, audit first and create/update a plan before implementation. The user must explicitly approve implementation. An AI agent may create `DRAFT` or `READY` plans but MUST NOT self-assign `APPROVED`.

## Allowed Statuses
`DRAFT`, `READY`, `APPROVED`, `IN_PROGRESS`, `BLOCKED`, `IMPLEMENTED`, `COMPLETED`, `CANCELLED`.

`APPROVED` = queued to be coded — implementation work remains. `IMPLEMENTED` = coded and verified functional (user convention, recorded 2026-10-24). `COMPLETED` = fully closed out (implementation + docs/QA wrap-up). `IMPLEMENTED` and `COMPLETED` are both terminal — no remaining work.

## Important
A plan describes intended work. It is never evidence that the work exists or was completed. Verify the codebase.

## Pack → Plan Coverage
Every zip in `docs/_zips/` maps to at least one megaplan. Verified 2026-10-24.

| Zip | Extracted pack | Plan(s) | Plan status |
|---|---|---|---|
| `00_file-organizer-docs-foundation.zip` | repo docs scaffold (`AGENTS.md`, `docs/*`) | PLAN-004 | COMPLETED (retrospective) |
| `01_FileMind_Foundation_Pack.zip` | `docs/packs/foundation-pack/` | PLAN-001, PLAN-008 (F-P6) | APPROVED |
| `02_FileMind_Complete_Backend_Roadmap.zip` | `docs/packs/backend-roadmap/` | PLAN-001 (M0–M3 done), PLAN-005 (automation), PLAN-006 (QA), PLAN-007 (intelligence), PLAN-009 (Groq), PLAN-020 (Groq quota), PLAN-010 (Android), PLAN-015 (search), PLAN-012 (cross-platform), PLAN-013 (advanced) | APPROVED / IMPLEMENTED / DRAFT |
| `03_file-organizer-ui-ux-foundation-pack.zip` | `docs/packs/ui-ux-foundation/` | PLAN-002 (extraction), PLAN-003 (mockup), PLAN-014 (WinUI app) | APPROVED / COMPLETED / APPROVED |
| `04_filemind-branding-palette-pack.zip` | `docs/packs/branding-palette/` | PLAN-002 (tokens, done), PLAN-011 (logo assets — Option 4 selected) | APPROVED / COMPLETED |
| `05_FileMind_CrossPlatform_UX_Pack.zip` | `docs/packs/crossplatform-ux/` | PLAN-019 (cross-platform UX — mockup + shared contracts) | APPROVED |

Numbering note: PLAN-011 is the logo-asset plan (written during a concurrent session after the user selected logo Option 4); the search megaplan occupies PLAN-015.

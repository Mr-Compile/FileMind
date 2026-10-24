# Project Rules

## Source of Truth
The implementation must be audited before making claims about current behavior. Approved product/backend intent lives in `docs/packs/foundation-pack/` and `docs/packs/backend-roadmap/`; repo rules live in `AGENTS.md` and `docs/rules/`.

## Rule Authority
Do not invent rules. A rule must be traceable to explicit user direction or an approved repository rules document. The extracted packs in `docs/packs/` are approved intent sources.

## Approved Backend Rules
From `docs/packs/backend-roadmap/AGENTS.md` and `docs/packs/foundation-pack/09-ai-agent/AI_DEVELOPMENT_RULES.md`:
- Domain/core logic must not call OS filesystem APIs directly.
- Every mutation must originate from an ActionPlan and pass the Safety Engine; verified and recorded in history.
- Classifiers and AI only return structured recommendations; never mutate.
- Never silently overwrite. Protected/system/application paths denied by default.
- Groq is optional; failure/offline/rate limits must never break local organization.
- Android access limited to authorized/OS-supported storage mechanisms.
- Add tests with each feature; do not postpone safety tests.
- Implement only the active phase; preserve module boundaries; small interfaces, explicit result/error types, deterministic local logic, versioned schemas, migrations, idempotent jobs, structured logs.
- No hard-coded API keys.

## Fact Labels
Use: `EXISTING`, `MISSING`, `BROKEN`, `INCOMPLETE`, `INCONSISTENT`, `LEGACY`, `PROPOSED`, `UNVERIFIED`.

## Change Discipline
Repair before replacing. Reuse before duplicating. Plan before coding. Approval before implementation. QA before completion. Documentation before final handoff. Logical commits after verified work — auto-commit on verified completion per `AGENTS.md`.

# AGENTS.md

## Mission
This file governs AI coding agents working in this repository. The repository is the primary source of truth for implementation. Documentation defines approved intent and constraints but must be compared with code before claims are made.

## Mandatory Workflow
1. Audit the relevant codebase before planning.
2. Read relevant product, rules, architecture, feature, plan, and QA documents.
3. Compare documented behavior with actual implementation.
4. Classify findings as `EXISTING`, `MISSING`, `BROKEN`, `INCOMPLETE`, `INCONSISTENT`, `LEGACY`, `PROPOSED`, or `UNVERIFIED`.
5. Repair existing working structures before proposing replacements.
6. Create or update a file in `docs/plans/` before meaningful code changes.
7. Do not implement until the user explicitly approves the plan.
8. Implement only the approved scope.
9. Run QA and repair findings within scope.
10. Update affected documentation to reflect verified final behavior.
11. Commit in small logical feature-based commits automatically once each feature/task is verified complete.

## Repository Truth / Anti-Hallucination Rules
- Never claim a file, feature, service, route, API, dependency, database, module, test, behavior, or rule exists unless verified.
- Never invent project rules. Rules must originate from explicit user instructions, `AGENTS.md`, `docs/rules/`, approved architecture docs, or approved feature docs.
- If no rule exists, say so and mark any recommendation as `PROPOSED`.
- If something cannot be confirmed, mark it `UNVERIFIED`.
- A plan is intent, not evidence of implementation.
- Documentation may be stale. Compare it with code.
- When code and docs conflict, report both states and propose a resolution; do not silently choose one.
- Do not fabricate repository evidence, paths, test results, command output, or completion status.

## Repair Before Replace
When functionality already exists: understand it, reproduce/verify the issue, identify root cause, make the smallest responsible repair, preserve compatible behavior, and validate it. Replacement or large refactoring requires justification in the approved plan.

## Scope Discipline
Audit broadly enough to understand impact; modify narrowly enough to remain inside the approved scope. Do not perform unrelated cleanup. Record unrelated findings for a separate plan.

## Planning Rule
Meaningful features, fixes, refactors, architecture changes, routing changes, storage changes, dependency changes, or cross-platform changes require a plan first. Plans live directly in `docs/plans/`; no plan subfolders are used.

Agents MUST NOT mark a plan `APPROVED` themselves. Approval must come from the user.

## QA Rule
Implementation is not complete because code compiles. Validate against `docs/qa/`, the plan acceptance criteria, existing behavior, regressions, errors, edge cases, and relevant platform behavior. Never claim a check passed unless it was actually run or otherwise verified.

## Documentation Rule
After implementation and QA, update only documentation affected by verified changes. Preserve the distinction between implemented, planned, and proposed behavior.

## Git Rule
Use logical commits such as `feat(scope): ...`, `fix(scope): ...`, `refactor(scope): ...`, `test(scope): ...`, `docs(scope): ...`, `chore(scope): ...`. Avoid vague commits such as `update project`. Do not mix unrelated work into one commit.

## Auto-Commit
When a feature, plan task, or milestone is verified complete (implementation done, relevant QA checks run and passing, affected documentation updated), commit it immediately in a logical feature-scoped commit without waiting for an explicit commit instruction. Never auto-commit incomplete, unverified, or out-of-scope work. Never push unless the user explicitly asks.

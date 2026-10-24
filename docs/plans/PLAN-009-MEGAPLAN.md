# PLAN-009 — FileMind Groq AI Megaplan (Backend Roadmap Phase 6 / PLAN-001 M7)

Status: APPROVED
Related Feature: F-P7 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement `docs/packs/backend-roadmap/06-groq-ai/PLAN.md` (zip `02_FileMind_Complete_Backend_Roadmap.zip`, outline in PLAN-001 M7): `IAIProvider` + `GroqProvider`, privacy/consent gate, request shaping, response schema validation, confidence/review policy, quotas/rate limits, cache, audit trail, secure credential abstraction, offline fallback — advisory only, never organizer authority.

## Repository Audit

### Verified Existing State

- `src/FileMind.Intelligence.AI/` project `EXISTING` — empty scaffold.
- Ports `EXISTING`: `IAIProvider`, `IAiPrivacyGate`, `ISecretStore` (`src/FileMind.Application/Ports/AppPorts.cs`); `IAiAuditRepository` (`Repositories.cs`); `ClassifierSource` enum includes AI source value (verify during implementation audit).
- Deterministic pipeline `EXISTING`; mode policy includes `AI-Assisted`/`Local-Only` names.
- `MISSING`: provider impl, privacy gate impl, request/response schemas, budget/rate-limit/cache, audit persistence, secret store impl, NL rule generation + query interpretation use cases.

### Evidence / Relevant Files

- Spec: `06-groq-ai/PLAN.md`; flow: `Local Result -> low/medium confidence + AI enabled -> privacy gate -> minimize payload -> Groq -> validate structured response -> merge as evidence -> confidence/review policy -> ActionPlan -> Safety`.
- `src/FileMind.Application/Ports/AppPorts.cs` (lines 76–88 AI ports).

### Documentation vs Code Differences

None — untouched intent.

## Findings

- AI ports + scaffold + policy names: `EXISTING`.
- Provider + gate + budgets + audit + secrets + use cases: `MISSING`.

## Global Constraints (verbatim from pack)

Groq is optional, never organizer authority. Model output never invokes filesystem methods. No hard-coded/shared production API keys in distributed binaries. File contents require explicit privacy settings; prefer extracted/minimized text over whole files. Failure/offline/rate-limit never breaks local organization — deterministic fallback always wins ties. All AI calls audited; budgets enforced.

## Recorded Decisions (2026-10-24, user-directed)

- Default model: `openai/gpt-oss-120b` (Groq-hosted OpenAI open-weight model). Model id is configuration, not a constant — `FILEMIND_GROQ_MODEL` env var or `ai.model` setting.
- API key for manual runs / connection testing: repo-root `.env` file holding `GROQ_API_KEY=...`. Verified `.env`/`.env.*` are gitignored (`.gitignore` lines 484–486, `.env.example` exempt). `.env.example` documents the variable. `ISecretStore` remains the stored-credential path for real use; `.env` is the dev opt-in.
- Token/quota budgeting (per-day vs per-week policy, provider-limit headroom): dedicated megaplan `docs/plans/PLAN-020-MEGAPLAN.md` — supersedes the detail of Milestone 2.1 below.

## Proposed Changes / Implementation Phases

### Milestone 0 — Contracts + Privacy Gate

Exit gate: AI request/response schemas + privacy gate compile; gate blocks protected paths and unconsented fields in tests.
- 0.1 AI request/response JSON schemas (versioned) under `schemas/ai/`; `AiSuggestion` evidence shape mapping to `ClassificationResult.Evidence`.
- 0.2 `AiPrivacyGate` impl: per-field consent toggles, protected-path exclusion, payload minimizer (metadata/extracted text only), `Local-Only` mode hard-off.
- 0.3 `ISecretStore` impl (Windows Credential Manager/DPAPI-backed; never in repo). Dev opt-in: minimal repo-root `.env` loader reading `GROQ_API_KEY` (gitignored; see Recorded Decisions) so a key can be pasted manually for runs/connection tests.

### Milestone 1 — GroqProvider

Exit gate: provider returns schema-validated suggestions or typed failure; never throws into pipeline.
- 1.1 `GroqProvider` (HttpClient, bounded timeouts, retry/backoff honoring rate-limit signals).
- 1.2 Request builder (model/messages/payload shaping per purpose; default model `openai/gpt-oss-120b`, overridable via `FILEMIND_GROQ_MODEL` / `ai.model` setting) + response validator against schema → typed error on drift.
- 1.3 Confidence mapper: AI evidence merges at capped weight; review policy preserved.

### Milestone 2 — Budgets, Cache, Audit

- 2.1 Request budget + rate-limit policy + cache keyed by minimized-payload hash — quota/token budgeting policy detailed in `docs/plans/PLAN-020-MEGAPLAN.md` (supersedes this line's detail).
- 2.2 `IAiAuditRepository` impl: timestamped audit records (purpose, payload classes sent — never raw contents, model, outcome).
- 2.3 Offline/disabled paths → deterministic local result unchanged (regression tests).

### Milestone 3 — Use Cases + Host

Exit gate: ambiguous classification assistance + NL rule draft + query interpretation demoable via host verbs; all advisory.
- 3.1 Low/medium-confidence escalation into classifier pipeline (evidence, not verdict).
- 3.2 NL→rule draft (schema-validated `Rule` preview requiring user confirmation), NL→search interpretation (feeds PLAN-011 surface when present; degrades without it).
- 3.3 Host verbs/config: `ai-*` diagnostics, opt-in flags; Smart Inbox assistance hints.
- 3.4 Docs: F-P7 status, privacy doc updates, CHANGELOG.

## Non-Goals

Other providers (PLAN-013 multi-provider), file-content upload without explicit consent design, AI-driven mutations, UI surfaces, semantic index.

## Files Expected to Change

`src/FileMind.Intelligence.AI/**`, `src/FileMind.Application/**` (gate + use cases), `src/FileMind.Infrastructure.Sqlite/**` (audit repo), `src/FileMind.Platform.Windows/**` (secret store impl), `src/FileMind.Windows.Host/Program.cs`, `schemas/ai/*`, docs.

## New Files Required

`schemas/ai/*.schema.json`, provider + gate + audit impls, AI test suites (mocked HTTP), consent fixtures.

## Security / Privacy Impact

Highest-sensitivity plan in the roadmap. Default = off. Per-field consent; protected paths never sent; minimized payloads; no secrets in repo/logs; audit trail queryable; opt-in only.

## Error Handling / Edge Cases

Timeout/rate-limit/5xx → typed fallback to local result; schema-drifted response → rejected + audited; missing key → feature disabled with truthful status; clock skew on budgets; consent revocation mid-session.

## QA Plan

Mocked-HTTP unit tests; gate tests (blocked fields/paths); schema validation tests; fallback regression (AI off vs on → identical local baseline); audit persistence tests; zero-network integration mode.

## Acceptance Criteria

- [ ] AI suggestions merge only as schema-validated evidence
- [ ] Privacy gate blocks unconsented fields + protected paths (tests)
- [ ] No API key in repo/binaries; `ISecretStore` backs credentials
- [ ] Offline/rate-limit/failure → deterministic local behavior unchanged
- [ ] Budgets + audit trail enforced and inspectable
- [ ] F-P7 status updated after verification

## Risks / Regression Areas

Payload leakage → gate tests + minimizer; provider drift → schema validation; latency hangs → hard timeouts + budget; accidental default-on → off-by-default config + tests.

## Documentation Updates

`FEATURES.md` F-P7, `docs/rules/SECURITY_RULES.md` pointer if consent model lands, `DATA_FLOW.md` AI stage, `CHANGELOG.md`.

## Commit Plan

`feat(intelligence-ai): contracts + privacy gate` → `feat(intelligence-ai): GroqProvider + schemas` → `feat(infrastructure-sqlite): AI audit` → `feat(application): AI use cases` → `feat(host): ai verbs` → `test(*):` → `docs(*):`.

## Approval

User approval required before implementation.

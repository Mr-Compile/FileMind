# PLAN-020 — Groq Quota & Token Budgeting (PLAN-009 companion)

Status: IMPLEMENTED (verified 2026-10-24)
Related Feature: F-P7 (see `docs/features/FEATURES.md`)
Created: 2026-10-24
Updated: 2026-10-24

## Verification Result

- `AiQuotaPolicy` + `AiQuotaGate`: daily primary window (800 req/160k tok defaults ≈80% of published free-tier), optional weekly cap (`ai.quota.weekly-requests`, 0=off), per-minute pacing (24 req/6,400 tok), per-request estimate cap (1,024), `max_completion_tokens` bound (512), cache TTL (30m) — all `ai.quota.*` settings.
- Provider reconciliation: `x-ratelimit-remaining-requests` (daily) + `remaining-tokens` (minute) observed per response; 0-remaining denies; 429 → `retry-after` breaker with auto-recover in `GroqChatClient`.
- Pre-flight gate in `GroqProvider.CompleteJsonAsync` — quota-blocked calls never reach HTTP (audited `quota-blocked`); `RecordCall` feeds minute counters immediately.
- `ai_usage` ledger (schema v4): `at`, `request_hash`, `model`, `purpose`, `prompt_tokens`, `completion_tokens`, `outcome`; `IAiUsageLedger` on `SqliteStore`; `ai-usage [n]` verb. Persistence-across-reopen test green.
- Cache hits spend zero quota (payload-hash `AiSuggestionCache`, test proves 2nd identical call = 0 HTTP).
- Exhaustion degrades to deterministic local result — `AiAdvisor`/`AiAssistService` call sites return null on any provider failure; regression covered by advisor tests.
- Tests: 8 `AiQuotaGateTests` + client/provider budget rows inside `AiFeatureTests`.

## Request / Goal

User direction: PLAN-009's Groq integration must not exhaust provider quota — a dedicated budgeting layer with a daily-or-weekly policy (choice delegated to agent; see Proposed Changes). This plan supersedes the detail of PLAN-009 Milestone 2.1 while PLAN-009 retains provider/gate/audit scope.

## Repository Audit

### Verified Existing State

- `IAIProvider`, `IAiPrivacyGate`, `ISecretStore` ports `EXISTING` — `src/FileMind.Application/Ports/AppPorts.cs:76-88`.
- `IAiAuditRepository` port `EXISTING` — `src/FileMind.Application/Ports/Repositories.cs`.
- `ISettingsRepository` on `SqliteStore` `EXISTING` — consumed at `src/FileMind.Windows.Host/Program.cs:115`; settings keys usable for `ai.quota.*` config.
- `FileMind.Intelligence.AI` scaffold `EXISTING`, empty — provider/budget code lands there.
- PLAN-009 M2.1 names "request budget (per session/day) + rate-limit policy + cache" — this plan is the detailed spec for that line.

### Evidence / Relevant Files

- `docs/plans/PLAN-009-MEGAPLAN.md` (Recorded Decisions: `openai/gpt-oss-120b`, dev `.env` key entry).
- `docs/packs/backend-roadmap/06-groq-ai/PLAN.md` — "retry/rate-limit policy, request budget, cache" required components.

### Groq Free-Plan Limits (published samples, org-level)

Verified against Groq rate-limit docs + mirrors on 2026-10-24 for `openai/gpt-oss-120b`:
30 RPM, 1,000 RPD, 8,000 TPM, 200,000 TPD. Groq states the console Limits page is authoritative and exceptions exist → **treat as config defaults, not constants; re-verify at implementation time.** Groq returns `x-ratelimit-*` headers on every response (remaining-requests is daily, remaining-tokens is per-minute) — headers are ground truth for reconciliation.

### Documentation vs Code Differences

None — no AI code exists to differ.

## Findings

- Budget/ledger/pacing/circuit-breaker: `MISSING` (`PROPOSED` here).
- Settings persistence for quota config: `EXISTING`.
- Audit port for usage records: `EXISTING` (ledger may reuse or extend it — decide at implementation).

## Root Cause / Problem

Free-tier quota is small and per-day: a watch-folder burst or a large analyze run can drain 1,000 RPD or 200k TPD mid-session, after which every call 429s until the daily window resets. Without local counters and pacing, the host learns about exhaustion only from repeated 429s.

## Proposed Changes

Layered budget, all limits configurable via `ai.quota.*` settings with conservative defaults (~80% headroom under published free-tier numbers):

1. **Daily window (primary)** — matches Groq's RPD/TPD reset. Defaults: 800 requests/day, 160,000 tokens/day. This is the answer to "per day or week": daily is the provider's own window, so it is the primary gate.
2. **Weekly cap (optional)** — off by default on free tier (redundant vs daily); enabled on paid plans for cost control.
3. **Per-minute pacing** — token-bucket limiter at 24 req/min and 6,400 tok/min; queues rather than rejects when the second axis allows.
4. **Per-request cap** — bounded `max_tokens` + minimized payloads (PLAN-009 gate already minimizes); a single request can never approach the TPM ceiling.
5. **Usage ledger** — SQLite-persisted per-call records (timestamp, model, prompt/completion tokens from Groq `usage`, purpose, outcome). May extend `IAiAuditRepository` or add `ai_usage` table — decide at implementation audit.
6. **Header reconciliation** — parse `x-ratelimit-remaining-*` on each response; when provider-remaining < local-remaining, adopt provider values (self-heals clock drift, other-org-usage, and missed calls).
7. **Pre-flight gate** — before each call, check counters; if a projected call would exceed budget → skip AI and return deterministic local result (never blocks the pipeline — existing constraint).
8. **429 circuit breaker** — parse `retry-after`, cool down, mark provider `IsAvailable=false` with truthful status text; auto-recover after cooldown.
9. **Cache** — minimized-payload hash → cached suggestion (TTL); cache hits spend zero quota (extends PLAN-009 M2.1).

## Non-Goals

- No provider code (PLAN-009), no multi-provider quota (PLAN-013), no UI surfaces, no billing/plan-tier logic beyond configurable numbers.

## Files Expected to Change

`src/FileMind.Intelligence.AI/**` (budget limiter, ledger writer, header parser, circuit breaker), `src/FileMind.Infrastructure.Sqlite/**` (ledger table or audit extension), `src/FileMind.Windows.Host/Program.cs` (`ai-status`/`ai-quota` diagnostic verbs per PLAN-009 M3.3), settings docs.

## New Files Required

Quota limiter + ledger impls; unit test suites.

## Architecture Impact

Additive services inside the AI boundary; `IAIProvider.IsAvailable` already exists for breaker status. No pipeline changes — fallback contract untouched.

## Data / Storage Impact

One new table or audit-repo extension; rows are small (usage metadata only, never payload contents — security rule).

## Routing / Flow Impact

None outside AI path.

## Platform Impact

Windows first; Android quota parity when F-P8 lands (shared counters are per-install, org-level provider limits shared across installs — note in doc).

## Security / Privacy Impact

Ledger stores token counts and purposes only — never payload contents or file paths beyond what PLAN-009 audit already allows. `.env` key stays gitignored.

## Error Handling / Edge Cases

Day-boundary rollover mid-run; clock skew vs provider window (headers win); counter file corruption → rebuild from ledger or start conservative; 429 with missing `retry-after` → exponential backoff ceiling; concurrent hosts draining the same org key (documented limitation — local budgets can't see other installs; headers mitigate).

## Implementation Phases

1. Quota config keys + daily/per-minute counters + pre-flight gate + tests.
2. Header reconciliation + 429 circuit breaker + tests.
3. SQLite usage ledger + `ai-quota`/`ai-status` verbs + tests.
4. Weekly cap + cache integration + docs.

## QA Plan

Mocked responses incl. 429 + `retry-after`; counter rollover test at window boundary; budget-exhausted → local-fallback regression (PLAN-009 contract); header-vs-local drift adoption; ledger persistence across restart.

## Acceptance Criteria

- [ ] AI calls never exceed configured daily/per-minute budgets; exhaustion degrades to local result with truthful status
- [ ] Provider `x-ratelimit-*` headers reconcile local counters
- [ ] 429 triggers breaker + `retry-after` cooldown; recovery automatic
- [ ] Ledger queryable via host verb; contains no payload contents
- [ ] Defaults sit under published free-tier limits (re-verified at implementation)

## Risks / Regression Areas

Over-restrictive defaults starving legit AI use (mitigate: configurable); ledger write latency in hot path (mitigate: async/batched writes); shared org key across machines (documented; headers reconcile).

## Documentation Updates

PLAN-009 M2.1 pointer (done), FEATURES.md F-P7 on completion, this plan's status, usage/quota docs for host verbs.

## Commit Plan

- `feat(intelligence-ai): quota config + daily/per-minute budget gate`
- `feat(intelligence-ai): ratelimit header reconciliation + 429 circuit breaker`
- `feat(infrastructure-sqlite): AI usage ledger`
- `feat(host): ai-quota/ai-status verbs`
- `test(*)`, `docs(*)`

## Approval

User approval required before implementation.

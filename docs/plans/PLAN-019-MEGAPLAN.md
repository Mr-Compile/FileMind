# PLAN-019 — FileMind Cross-Platform UX Megaplan (Cross-Platform UX Pack — zip 05)

Status: APPROVED
Related Feature: F-UI1 (mockup, PLAN-003) + new F-UI3 row for the cross-platform UX layer
Created: 2026-10-24
Updated: 2026-10-24

## Request / Goal

Implement `docs/_zips/05_FileMind_CrossPlatform_UX_Pack.zip` (extracted to `docs/packs/crossplatform-ux/`): one consistent FileMind experience across Windows and Android — shared information architecture, navigation/routing, safe file-operation UX, Help & About, explainability, status vocabulary, and platform adaptations.

User-approved scope (2026-10-24): this plan implements the pack in **`mockup/` + shared UX contracts only**. The mockup is the canonical visual reference (`docs/rules/MOCKUP_RULES.md`) and the only UI surface that exists today — no WinUI project exists (PLAN-014, READY) and no Android app exists (PLAN-010 is backend-only). Native Windows adaptations become requirements feeding PLAN-014 execution; Android adaptations are captured as a contract section for the future Android-UI plan. No native code in this plan.

## Repository Audit

### Verified Existing State

- Pack `EXISTING` at `docs/packs/crossplatform-ux/` (flattened to pack convention): `README.md` (goal + core rule: share concepts/terminology/statuses/flows/safety, adapt presentation per platform), `00_README/SCOPE.md` (13 UX additions), `01_SHARED_UX/SHARED_UX_SYSTEM.md` (IA, lifecycle, 14 statuses, empty states, explainability), `02_WINDOWS/WINDOWS_UX.md`, `03_ANDROID/ANDROID_UX.md`, `04_HELP_ABOUT/HELP_ABOUT_SPEC.md` (9 destinations), `05_SAFETY/SAFE_FILE_OPERATIONS.md` (8-step required flow), `06_ROUTING/NAVIGATION_ROUTING.md`, `07_IMPLEMENTATION_PLAN/AI_IMPLEMENTATION_PLAN.md` (phases 0–7), `08_QA/UX_QA_CHECKLIST.md` (20 items).
- Mockup `EXISTING` (`mockup/` — HTML/CSS/JS, no build, `file://`-capable): hash router with 10 routes `dashboard|files|organizer|tools|search|devices|activity|settings|console|architecture` (`app.js:520-529`); deep-linkable `?theme|density` params (`app.js:531-545`); per-screen state in `FM.state` (filters/selection survive navigation); organizer wizard mirrors the real pipeline `source → scan → analyze → plan → safety → execute → result → undo` (`flows.js:10-247`); warning-triggered `FM.confirm` before execute (`flows.js:136-146`); progress + AbortController cancel (`flows.js:163-209`); result with Undo + View Activity (`flows.js:213-244`); Activity screen = operation center + Smart Inbox + 12-state chip legend (`screens.js:391-489`); canonical op states `Ready|Queued|Checking|Running|Paused|Completed|Completed with warnings|Failed|Cancelled|Offline|Unsupported|Permission required` (`screens.js:451`); decision chips `Organize|Review|Ignore|NoOp` (`screens.js:19`); `FM.empty` helper with next-action slot (`app.js:464`); confidence bars 95/70 bands (`app.js:459`); per-row expandable evidence ("Decision/Rules/evidence" expander, `flows.js:86-89`); plan table "Why" column (`flows.js:111`); protected-path safety blocker demo (`flows.js:51-55`); Settings sections General/Appearance/File behavior/Permissions/Notifications/Advanced/About (`screens.js:498-543`); titlebar `command-box` → search and `Ctrl+K` → search only (`index.html:30`, `app.js:588-590`); `opcenter-btn` badge → activity (`app.js:587`, `app.js:562-566`).
- Backend truth `EXISTING` (cited for contract mapping): `DecisionKind` Organize/Review/Ignore/NoOp, `OperationStatus` Planned/Validating/Executing/Verifying/Committed/Failed/RollingBack/RolledBack/NeedsAttention, `ConflictPolicy`, `FileKind`, `ConfidenceBand` (`mock-data.js:11-24` mirrors `src/FileMind.Domain/Contracts/Enums.cs`); protected paths deny-by-default (`FM_DATA.protectedPrefixes` mirrors `WindowsProtectedPaths`); Smart Inbox persists Review decisions (`docs/features/FEATURES.md` F-P2, EXISTING); `watch` pipeline EXISTING (F-P4).
- No UI project `EXISTING`: `FileMind.slnx` = 8 src + 4 test projects, none UI. PLAN-014 (WinUI 3) READY/unstarted; PLAN-010 (Android backend) READY — explicitly no UI. Explorer integration remains MISSING (PLAN-014 scope).

### Evidence / Relevant Files

- Spec: `docs/packs/crossplatform-ux/**` (9 docs).
- Mockup: `mockup/{index.html,app.js,screens.js,flows.js,styles.css,mock-data.js,README.md}`.
- Rules: `docs/rules/MOCKUP_RULES.md` (mockup must update in the same change for UI/contract/host/feature-status/brand changes; unimplemented features shown disabled + truthful `Planned`/`Missing` label — never fake-functional), `docs/rules/RULES.md`, `docs/rules/QA_RULES.md`.
- Adjacent plans: PLAN-003 (mockup, COMPLETED), PLAN-014 (WinUI app, READY — F-UI2), PLAN-010 (Android backend, READY), PLAN-016 (logo assets incl. Android `res/` drop-in, COMPLETED).
- Feature truth: `docs/features/FEATURES.md` (F-P6 duplicates/large-file MISSING → "Duplicate"/"Large File" chips are contract-only; F-P7 Groq AI MISSING → "AI Suggested" chip contract-only).

### Existing Rules Found

- Status never color-only; every state pairs color + icon + label (ui-ux-foundation components/03, enforced in mockup legend).
- Truthful progress; no fake progress. Friendly errors. UI never mutates — mutations flow `Decision + Safety + ActionPlan → Platform Adapter → Verify → History`.
- MOCKUP_RULES: this plan IS a UI/UX change — mockup updates land in the same commits as the spec/contract changes.
- Plan conventions: statuses `DRAFT|READY|APPROVED|IN_PROGRESS|BLOCKED|COMPLETED|CANCELLED`; agent may file DRAFT or READY, never APPROVED (`docs/plans/README.md`).

### Documentation vs Code Differences

- Pack IA lists "Home"; mockup/ui-ux-foundation name it "Dashboard" — same destination, record the mapping rather than rename.
- Pack lists file-level statuses (Analyzing, Duplicate, Suggested, Protected, Excluded, Modified, New, Large File, AI Suggested) that have no single domain enum — they compose `DecisionKind` + flags + (missing) F-P6/F-P7 signals. No doc/code conflict; a vocabulary mapping is required so both platforms use identical chips.
- Settings IA in pack: General, Appearance, File Management, Privacy & Safety, Updates, Help & About. Mockup: General, Appearance, File behavior, Permissions, Notifications, Advanced, About (inline). Mapping needed; Privacy & Safety and Help & About sections are new.

## Findings

- Shared IA base routes: `EXISTING` (10 routes; Dashboard≡Home).
- Help & About subtree (About, Getting Started, User Guide, What's New, Troubleshooting, Diagnostics, Feedback, Credits, Licenses): `MISSING` (only a small inline About block in Settings, `screens.js:538-543`).
- Operation lifecycle Analyze→Preview→Confirm→Execute→Result→Undo/Activity: `INCOMPLETE` — preview/safety/execute/result/undo exist; the **Confirm** step fires only when warnings exist (`flows.js:137`), no counts summary (moved/renamed/deleted/unchanged), no explicit deletion statement, no irreversible-action label, no undo-availability statement before execute.
- Canonical operation-state chips: `EXISTING`. Pack file-level statuses Protected/Excluded: `EXISTING` (chips in files screen); Analyzing/Suggested/Modified/New: partially expressible today; Duplicate/Large File/AI Suggested: `MISSING` upstream (F-P6/F-P7) — contract + `Planned`-labeled chips only.
- Empty states with next action: `EXISTING` pattern (`FM.empty`), requires audit sweep so no dead-end messages remain.
- Explainability ("Why?" signals + confidence; low-confidence → Needs Review): `EXISTING` as data (evidence expander, Why column, conf bars, Smart Inbox); `INCOMPLETE` as a reusable component — not offered on Smart Inbox items or plan rows as a consistent affordance.
- Breadcrumbs (Windows): `MISSING`. Back/Forward explicit controls: `MISSING` (browser hash only). Route-state restore incl. scroll: `INCOMPLETE` (`FM.state` restores filters/selection; scroll not persisted).
- Command palette (Ctrl+K for navigation + actions): `MISSING` — Ctrl+K and command-box only navigate to Search.
- Keyboard shortcut reference (searchable): `MISSING` — only Ctrl+K exists.
- Task Center (background/minimized ops without blocking browsing): `INCOMPLETE` — active op renders on Dashboard + Activity + titlebar badge; no dedicated always-visible center/panel.
- Protected folders as user-facing UX (mark/unmark, authorization prompt): `MISSING` — engine deny-by-default exists for system paths; no user-managed protected folder list.
- Diagnostics (platform/version/build/health, Copy Diagnostic Info, privacy-safe by default): `MISSING`.
- What's New surface (post-update highlights, retained): `MISSING`.
- Windows adaptations beyond mockup (native nav, shortcuts, acrylic, density): deferred — `PROPOSED` here as PLAN-014 requirements.
- Android adaptations (system back, touch layouts, action sheets, storage-permission explainer, compact About): no Android app exists — `PROPOSED` here as contract-only, input to the future Android-UI plan.
- QA checklist (`08_QA`): becomes this plan's acceptance criteria where mockup-applicable.

## Root Cause / Problem

Not a bug — an absence. The pack defines cross-platform UX intent (shared vocabulary, navigation, help, safety chrome) that postdates PLAN-003's mockup scope. The mockup implements the ui-ux-foundation pack (zip 03) faithfully but was built before these shared-UX requirements existed, so the Help & About subtree, confirm/counts step, command palette, breadcrumbs/back-forward, protected-folder UX, diagnostics, and the shared status vocabulary were never specified for it.

## Proposed Changes

Phases mirror the pack's own `07_IMPLEMENTATION_PLAN/AI_IMPLEMENTATION_PLAN.md` (audit → contracts → navigation → safe ops → help/about → intelligence UX → platform polish → QA), scoped to mockup + contracts.

### Phase A — Shared UX contracts (`docs/product/UX_CONTRACTS.md`, new)

1. **Route IDs** — canonical shared IDs every platform maps to: `home` (Dashboard on Windows mockup), `files`, `search`, `tools`, `activity`, `settings` (+ subsections `general|appearance|file-management|privacy-safety|updates|help-about`), and `help/{about|getting-started|guide|whats-new|troubleshooting|diagnostics|feedback|credits|licenses}`.
2. **Status vocabulary** — single chip vocabulary mapped to domain truth: Analyzing→pipeline-in-flight; Ready→DecisionKind.Organize; Needs Review→DecisionKind.Review / Smart Inbox; Protected→protected-path/user-protected flag; Excluded→exclusion match; Suggested/AI Suggested→ClassifierSource Local/Ai suggestion (Ai pending F-P7); Duplicate/Large File→F-P6 signals (`Planned`); Modified/New→filesystem delta flags; Completed/Failed/Cancelled→OperationStatus. Table keeps icon+color+label per status — never color alone.
3. **Operation lifecycle** — the pack's 8 required steps restated as the canonical UI contract, incl. counts summary, deletion statement, irreversibility label, confirm-always, progress+cancel, result, undo/activity.
4. **Help & About content model** — per-destination content fields + which surfaces are truthful today vs `Planned`-labeled.
5. **Platform capabilities** — Windows: breadcrumbs, back/forward, Ctrl+K palette, shortcuts, multi-select, density, task center, native About. Android: system back semantics, touch targets ≥48dp, action sheets, storage-permission explainer + Manage Access, compact About. Marked as the contract PLAN-014 and the Android-UI plan must satisfy.

### Phase B — Navigation (mockup)

1. Register `help` route family: `#/help` (hub) + `#/help/about|getting-started|guide|whats-new|troubleshooting|diagnostics|feedback|credits|licenses` — all deep-linkable per `06_ROUTING`.
2. Breadcrumbs component on Files (scope→folder) and Tools detail views (desktop pattern per `02_WINDOWS`).
3. Titlebar back/forward buttons → `history.back()/forward()`; document that Android maps to system back (contract).
4. Per-route state + scroll restore: persist scroll offset in `FM.state` per route on navigation; restore on `hashchange` to same route.

### Phase C — Safe operations (mockup)

1. Explicit Confirm step between Safety and Execute for **every** run: counts summary (Move/Copy/Rename/unchanged + explicit "0 deletions — FileMind does not delete files" statement until delete ops exist), irreversible-action label when an item is irreversible (none today — render truthfully), undo-availability statement ("every move verified + recorded; undo restores"). Warning runs keep the existing warning banner + confirm.
2. Protected folders UX: Settings → new **Privacy & Safety** section — user-managed protected-folder list (mark/unmark demo folders, persisted in mock store settings), Protected chip shown in Files/Organizer for protected items, organizer treats user-protected source as authorization-required (banner + explicit authorize affordance) vs system-protected (hard block, existing demo).

### Phase D — Help & About (mockup)

9 screens under the `help` route family, truthful mock content, `Planned` badges where the underlying feature is MISSING:
- **About FileMind** — logo lockup (`branding/logo/`), tagline, version/build (single `FM_DATA.appInfo` source), purpose statement, links to all help destinations.
- **Getting Started** — the real workflow choose folder → analyze → review → approve, action-oriented, links into `#/organizer`.
- **User Guide** — task-organized sections (browsing, search, organizing, duplicates `Planned`, smart suggestions, activity/undo, privacy, settings).
- **What's New** — release highlights list; "show once after update" noted as platform behavior contract.
- **Troubleshooting** — common problems + recovery + links into Diagnostics.
- **Diagnostics** — platform/arch/app version/build/service health mock panel; **Copy Diagnostic Info** button; privacy-safe default (no filenames/paths) with explicit opt-in toggle.
- **Feedback** — report-a-problem/feedback link surface (mock mailto/issue link presentation, labeled).
- **Credits** — team/attribution.
- **Licenses** — third-party notices (mock entries mirroring real deps).

### Phase E — Intelligence UX (mockup)

1. Reusable **Why?** detail component (signals + score contributions + confidence band + sources) applied consistently: analyze rows (exists — extract), plan rows, Smart Inbox items.
2. Low-confidence → Needs Review path already mirrored (DecisionMaker→Review→Smart Inbox); verify and cite in contract.
3. Status legend additions: file-status chips (Duplicate, Large File, AI Suggested) rendered in legend with `Planned · F-P6/F-P7` badges — vocabulary documented before upstream features land.

### Phase F — Windows polish (mockup)

1. Real **Ctrl+K command palette**: overlay listing all routes (incl. help destinations), common actions (New organize run, Go to Smart Inbox, Toggle theme/density, Open shortcuts), fuzzy filter, keyboard navigation, `command-box` click opens it.
2. **Keyboard shortcuts reference**: searchable dialog/screen listing all shortcuts (Ctrl+K, back/forward, etc.); new shortcuts only added if truthful in mockup.
3. Multi-select + desktop density: `EXISTING` — verify against checklist, no work expected.

### Phase G — Android contract (docs only)

`UX_CONTRACTS.md` Android section: shared route ID → Android destination map; system-back semantics table (what each level pops to); storage-permission explainer + Manage Access flow spec; touch-first layout requirements (targets, cards, action sheets); compact About structure; Task Center visibility requirement for long-running tasks (WorkManager-era note for PLAN-010/future UI plan). **No code** — recorded as requirements for the future Android-UI plan.

### Phase H — QA + documentation

1. Run every `08_QA/UX_QA_CHECKLIST.md` item applicable to the mockup; mark platform-only items N/A-with-reason.
2. Update `FEATURES.md` F-UI3 status, `mockup/README.md` (new routes/components), `CHANGELOG.md`, this plan's status.

## Non-Goals

- No WinUI/XAML/native Windows code (PLAN-014 scope).
- No Android app code, no Kotlin, no Gradle (future Android-UI plan; PLAN-010 is backend-only).
- No backend/domain changes — this is a UX layer + docs plan. Mock "protected folder" marking is mock-store state only; any real `IProtectedPathRepository`-style backend work is a separate plan if ever needed.
- No delete operations — none exist in the domain; the confirm step states "no deletions" truthfully.
- No real feedback/telemetry endpoints — Feedback destination is a labeled mock surface.
- No rename of Dashboard→Home; the contract records the mapping.
- No changes to classification, rules, watch, or pipeline logic.

## Files Expected to Change

- `mockup/app.js` — help routes, back/forward, scroll restore, command palette, shortcut handlers, `FM.why` component helper.
- `mockup/screens.js` — breadcrumbs on Files/Tools, Settings Privacy & Safety + Help & About sections (replacing inline About), status legend additions, Why? on inbox items.
- `mockup/flows.js` — explicit Confirm step (counts + deletion statement + irreversibility + undo note), protected-source authorization path.
- `mockup/index.html` — palette/dialog roots, back/forward buttons (if titlebar-mounted).
- `mockup/styles.css` — palette, breadcrumbs, help-layout, protected-chip styles.
- `mockup/mock-data.js` — `FM_DATA.appInfo` (version/build/platform), help content model, diagnostics payload (privacy-safe + opt-in), user-protected folders seed.
- `mockup/README.md` — new routes/components table updates.
- `docs/product/UX_CONTRACTS.md` — new (Phase A/G output).
- `docs/features/FEATURES.md` — F-UI3 row updates.
- `CHANGELOG.md` — verified changes entry.
- This plan's status line.

## New Files Required

- `docs/product/UX_CONTRACTS.md` — shared UX contract (routes, statuses, lifecycle, help model, platform capabilities).
- Optionally `mockup/help.js` if help screens grow `screens.js` past comfortable size — decide at implementation (single-file convention preferred unless >~800 lines).

## Architecture Impact

None on backend. Mockup gains screens/components within existing `FM.screen`/`FM.state` architecture; contracts doc is additive under `docs/product/`.

## Data / Storage Impact

Mock-only: `FM_DATA.appInfo`, help content, diagnostics payload, user-protected-folder list in mock settings. No schema, SQLite, or profile changes.

## Routing / Flow Impact

Mockup router gains the `help/*` family + back/forward + scroll restore; organizer flow gains a Confirm step between Safety and Execute. Host verb routing untouched (distinct system — `docs/architecture/ROUTING.md`).

## Platform Impact

Windows: fully realized at mockup fidelity; native requirements recorded for PLAN-014 (breadcrumbs, palette, shortcuts, density, task center, native About). Android: contract section only; feeds the post-PLAN-010 Android-UI plan. Cross-platform consistency is enforced by the shared route IDs + status vocabulary in `UX_CONTRACTS.md`.

## Security / Privacy Impact

Diagnostics defaults to privacy-safe (no filenames/paths); opt-in toggle explicitly consent-gated — matches `04_HELP_ABOUT` requirement. Feedback surface collects nothing. Protected-folder UX is authorization chrome over existing deny-by-default safety; no weakening of `WindowsProtectedPaths`.

## Error Handling / Edge Cases

- Empty help content → `FM.empty` with next action (no dead ends).
- Unknown `help/*` slug → existing `FM.screens._missing` pattern.
- User-protected folder as organizer source → authorization-required banner (distinct from system-protected hard block).
- Undo of user-protected destination items → unchanged (fingerprint-check already truthful).
- Diagnostics copy with paths opted-in → clearly labeled; default excludes them.
- Back/forward at history bounds → buttons disabled.
- Palette with no matches → empty state with "press Enter to search files" action.
- Dry-run path → confirm step skipped or rephrased ("dry run — nothing moves") consistently.

## Implementation Phases

A. `UX_CONTRACTS.md` (routes, statuses, lifecycle, help model, Windows/Android capability tables).
B. Mockup navigation: help routes, breadcrumbs, back/forward, scroll restore.
C. Mockup safe ops: confirm step + counts/deletion/undo statements; Privacy & Safety protected-folder UX.
D. Mockup Help & About: 9 screens incl. diagnostics + What's New.
E. Mockup intelligence UX: Why? component reuse + status legend additions.
F. Mockup Windows polish: Ctrl+K palette + shortcuts reference.
G. Android contract section (folds into A's doc — no code).
H. QA sweep vs `08_QA` checklist + doc updates (FEATURES F-UI3, mockup README, CHANGELOG, plan status).

## QA Plan

Map `08_QA/UX_QA_CHECKLIST.md` to verification:
- About opens + version accurate → manual check all `#/help/*` routes over `file://` and a served path.
- No dead-end navigation → every empty state + help screen has a next action.
- Back restores prior state → set filters/scroll on Files, navigate away, browser-back and titlebar-back both restore.
- Windows back/forward/breadcrumbs consistent → manual sweep.
- Android back → N/A (no Android app; contract documented) — record as N/A.
- Destructive actions require explicit confirmation → reset-demo + execute paths verified.
- Preview accurately represents operation → plan table == executed items (existing parity, re-verify).
- Irreversible labeled before execution → confirm-step label present (truthful "none today" when empty).
- Undo works where advertised → organizer + Activity undo round-trips.
- Activity records completed/failed/cancelled → existing statuses re-verified + cancelled demo.
- Task Center reflects active ops → badge + dashboard + activity during a run.
- Low-confidence reviewable → Smart Inbox items show Why? + approve/keep.
- Protected-folder enforced → system block + user-protected authorization path.
- Empty states next-action → sweep all `FM.empty` call sites.
- Diagnostics privacy-safe → default payload contains no filenames/paths; opt-in labels clearly.
- Touch/keyboard usable → keyboard: palette + shortcuts sweep; touch: N/A desktop-mockup (contract).
- Existing functionality intact → all 10 existing routes render, console simulator unchanged, organizer demo + protected demo still work, themes/density unaffected.
- Zero console errors on every route; `index.html` still works from `file://` (MOCKUP_RULES).

## Acceptance Criteria

- [ ] `UX_CONTRACTS.md` exists: route IDs, status→domain mapping, 8-step lifecycle, help content model, Windows + Android capability tables.
- [ ] All 9 Help & About destinations reachable via nav + deep link (`#/help/diagnostics` etc.); About shows version/build from `FM_DATA.appInfo`.
- [ ] Execute always passes an explicit Confirm showing counts + deletion statement + irreversibility + undo note.
- [ ] User-protected folders manageable in Settings → Privacy & Safety; protected source requires authorization in organizer.
- [ ] Ctrl+K opens a real palette navigating every route + common actions; searchable shortcut reference exists.
- [ ] Breadcrumbs on Files; titlebar back/forward work with state+scroll restore.
- [ ] Why? affordance on analyze rows, plan rows, and Smart Inbox items.
- [ ] Status legend includes file-level chips with truthful `Planned` labels for F-P6/F-P7 items.
- [ ] Diagnostics copies privacy-safe info; paths only behind explicit opt-in.
- [ ] Every `08_QA` item pass or documented N/A; zero console errors; existing screens/flows unchanged.
- [ ] FEATURES.md F-UI3, mockup README, CHANGELOG updated; commits per plan.

## Risks / Regression Areas

- `FM.route` refactor (help family + scroll restore) could break existing deep links (`?theme|density`, `organizer?src=`) — regression-check all documented links in `docs/development/DEVELOPMENT.md`.
- Confirm-step insertion could break the dry-run→"Run for real" replay path (`flows.js:159`) — verify both directions.
- Palette/shortcut handlers must not steal keys from the console input (`#c-in`) — scope key handlers.
- Settings restructure (About → Help & About) — keep `st-reset` and existing controls working.
- `screens.js` size growth — split `help.js` only if needed (see New Files).

## Documentation Updates

- `docs/product/UX_CONTRACTS.md` (new); `docs/features/FEATURES.md` F-UI3; `mockup/README.md`; `CHANGELOG.md`; this plan's status; `docs/plans/README.md` coverage row already filed with this plan.

## Commit Plan

- `docs(product): shared UX contracts — routes, statuses, lifecycle, help model, platform capabilities` (Phase A+G)
- `feat(mockup): help routes, breadcrumbs, back/forward + scroll restore` (Phase B)
- `feat(mockup): confirm step with counts/deletion statement + protected-folder UX` (Phase C)
- `feat(mockup): Help & About screens — About, guide, What's New, diagnostics, credits, licenses` (Phase D)
- `feat(mockup): reusable Why? explainability + status legend additions` (Phase E)
- `feat(mockup): Ctrl+K command palette + shortcut reference` (Phase F)
- `docs(mockup,features,changelog): PLAN-019 QA pass + F-UI3 + docs sync` (Phase H)

## Approval

User approval required before implementation. Filed READY in the pack→plan queue (`docs/plans/README.md`).

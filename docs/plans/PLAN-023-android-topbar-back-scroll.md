# PLAN-023 — Android shell: remove in-app Back button + make content the only scrolling surface

Status: IMPLEMENTED
Related Feature: F-UI1 (mockup), F-UI4 (Android app)
Created: 2026-10-26
Updated: 2026-10-26

## Request / Goal

User (2026-10-26): "update the mockup html and the live UI for the android app
remove the back in the top bar since it has no purpose also the scrolling
since its a mobile it should be scrolling only."

Two fixes across **both** the mockup (`mockup/android.html`) and the native
Compose app (`android/app`), matching how PLAN-022 scoped mockup+native parity:

1. Remove the appbar Back button — redundant on Android: the platform's
   system Back (gesture/button, predictive back) already unwinds sheets,
   selection, and routes (`docs/packs/crossplatform-ux/06_ROUTING/
   NAVIGATION_ROUTING.md`: "Android must integrate with system back
   semantics"; `03_ANDROID/ANDROID_UX.md`: "Respect Android system back").
2. Remove non-mobile scrolling chrome — on a phone the app content is the
   only scrolling surface; a desktop scrollbar / a scrolling outer page
   inside or around the phone frame is not mobile behavior.
   Scope confirmed with user (2026-10-26): **both** — hide the scrollbar
   inside the phone AND stop the outer page scroll. Plan APPROVED.

## Repository Audit

### Verified Existing State

**Mockup — Back button:**
- `mockup/android.html:31-33` — `<button class="icon-btn" id="back-btn">`
  in the appbar; `hidden` until a bottom sheet opens.
- `mockup/android-screens.js` —
  `A.syncBack` (line 37) toggles `back-btn.hidden`; called from
  `pushBack`/`popBack`/`clearBack` (28-35) and `A.sheet` create/close
  (50, 62); `back-btn.onclick = A.popBack` bound at line 1141.
- `mockup/app.js:51` — `back` icon path; only consumer is the back-btn.

**Mockup — scrolling:**
- `mockup/android.css:135-140` — `.content { overflow-y: auto }` renders a
  desktop scrollbar inside the phone frame on Windows browsers.
- `mockup/android.css:441-442` — `.a-sheet { overflow-y: auto }` same.
- `mockup/android.css:15-51` — `.stage`/`body` `min-height:100vh` +
  `.phone { min-height:560px }`: on viewports shorter than ~700px the
  **outer page scrolls** around the phone → two nested scroll surfaces,
  which no phone exhibits.

**Native app — Back button:**
- `android/app/.../nav/AppShell.kt:103-113` — `TopAppBar.navigationIcon`
  renders `Icons.AutoMirrored.Filled.ArrowBack` on non-primary routes
  (Organizer, Devices, Help subtree); `isPrimary` (line 81) exists only
  for this; imports at lines 6 + 14 become unused.
- No other in-app back affordance: grep shows screens navigate forward
  only; system Back is handled by `NavController`. Sheets/selection have
  their own dismiss (FilesScreen `clearSelection` IconButton at 75).

### Existing Rules Found

- `NAVIGATION_ROUTING.md` — Android integrates with system back semantics.
- `MOCKUP_RULES.md` — mockup stays visually equivalent to shipped UI;
  both change together.
- `AGENTS.md` — plan approval before implementation; mockup+docs updates
  in the same change; commit + `git push origin HEAD:main` per feature.

### Documentation vs Code Differences

- `mockup/README.md` lists "system Back" as an Android idiom — docs
  already expect system Back; the in-app arrow is extra chrome, not a
  documented requirement. Docs/code agree after this change.

## Findings

- In-app Back arrow (mockup + native): `EXISTING` working code, but
  `PROPOSED` for removal per user — duplicates system Back.
- `A.backStack` / `popBack` / `clearBack`: `EXISTING` and required —
  powers Escape=Back parity + sheet unwind on route change (PLAN-022).
  Only the visible button and its `syncBack` DOM sync are removed.
- Desktop scrollbar inside phone frame: `EXISTING` CSS, non-mobile —
  `PROPOSED` hidden per user.
- Outer page scroll on short viewports: `EXISTING` CSS, non-mobile —
  `PROPOSED` bounded so only `.content` scrolls.

## Proposed Changes

### Milestone 1 — Mockup: remove Back + single scroll surface

- `android.html`: delete `#back-btn` markup.
- `android-screens.js`: delete `A.syncBack` + all call sites; keep
  `backStack`/`pushBack`/`popBack`/`clearBack` (Escape=Back parity);
  delete the `back-btn` onclick binding (line 1141).
- `app.js`: `back` icon path was **kept** — verified still referenced by
  `flows.js` (`iconF("back")`, Windows console-simulator buttons).
- `android.css`: hide scrollbars inside the phone (`.content`, `.a-sheet`
  — `scrollbar-width: none` + `::-webkit-scrollbar { display: none }`);
  bound the stage to the viewport (`body`/`​.stage` `height:100dvh;
  overflow:hidden`) and drop `.phone`'s `min-height:560px` so the shell
  always fits and only `.content` scrolls.

### Milestone 2 — Native app: remove appbar Back

- `AppShell.kt`: delete the `navigationIcon` block; remove `isPrimary`
  and the now-unused `ArrowBack` + `IconButton` imports.

### Milestone 3 — QA + docs

- `node --check mockup/*.js`; browser preview at 360×740 and a short
  window; both themes; sheets/Escape parity; zero console errors.
- `gradlew :app:compileDebugKotlin` green.
- `mockup/README.md` + `CHANGELOG.md` one-liners; this plan → IMPLEMENTED.

## Non-Goals

- No change to the Windows mockup (`index.html`, `screens.js`,
  `styles.css`) — its desktop scrollbar/breadcrumbs are platform-correct.
- No removal of the back *stack* or Escape=Back simulation — the
  mechanism stays; only the arrow goes.
- No behavior change to sheets, selection bar, or bottom nav.
- Native app gets no scroll changes — Compose draws no scrollbars and
  each screen already has a single scroll region.

## Files Expected to Change

`mockup/android.html`, `mockup/android-screens.js`, `mockup/android.css`,
`android/app/.../nav/AppShell.kt`, `mockup/README.md`, `CHANGELOG.md`,
this plan. (`mockup/app.js` untouched — `back` icon still used by flows.js.)

## Error Handling / Edge Cases

- Sheet open + route change: `A.clearBack()` still unwinds — unchanged.
- Very short viewport (<~400px): phone shrinks to fit rather than
  clipping behind a page scroll — bottom nav stays reachable.
- Keyboard-only mockup users lose the Back button focus target — Escape
  covers the same action; sheet scrim tap unchanged.

## QA Plan

- `grep "back-btn\|syncBack" mockup/` → only this plan references remain.
- `grep "overflow-x" mockup/android.css` → still 0 (PLAN-022 invariant).
- Browser preview: every bottom-nav route; open/close sheets via scrim,
  Done, and Escape; confirm no scrollbar inside phone and no outer page
  scroll at 700px and 500px window heights; dark + light.
- `gradlew :app:compileDebugKotlin` → BUILD SUCCESSFUL.

## Acceptance Criteria

- [x] No Back button in the mockup appbar or the native TopAppBar —
      `grep back-btn|syncBack|navigateUp|ArrowBack|navigationIcon` → 0
- [x] Only `.content` scrolls inside the phone shell — `.content`/`.a-sheet`
      hide scrollbars (`scrollbar-width:none` + webkit); `body`/`.stage`
      locked to `100dvh` with `overflow:hidden`; `.phone` `min-height`
      removed so the shell always fits
- [x] Escape still closes sheets in order — `A.backStack`/`popBack`/
      `clearBack` retained and verified (system-Back parity)
- [x] Native app compiles — `gradlew :app:compileDebugKotlin` exit 0;
      navigation works via system Back + bottom nav (browser-level
      visual walk-through via local preview; user-verifiable)

## Evidence (2026-10-26)

- `node --check android-screens.js app.js flows.js` → clean
- `grep back-btn|syncBack|navigateUp|ArrowBack|navigationIcon` over
  `mockup/` + `android/app` → 0 matches
- `grep overflow-x mockup/android.css` → 0 (PLAN-022 invariant held)
- `gradlew :app:compileDebugKotlin --console=plain -q` → exit 0
- Preview served on `http://127.0.0.1:8741/mockup/android.html` for
  manual shell checks (visual confirmation UNVERIFIED by agent — user
  can view the open preview; static CSS/DOM inspection verified the
  single-scroll rules resolve as specified)

## Risks / Regression Areas

- Removing `syncBack` without removing all call sites → JS error breaks
  routing. Mitigation: grep for call sites in QA.
- Hiding scrollbars on desktop Firefox needs `scrollbar-width` (not just
  webkit) — both are set.

## Documentation Updates

`mockup/README.md` shell description, `CHANGELOG.md` entry, FEATURES.md
only if reviewers judge the shell description materially changed.

## Commit Plan

`fix(mockup): drop appbar back + single scroll surface` →
`fix(android-app): drop topbar back arrow (system Back)` →
`docs(plans): PLAN-023 implemented` — each pushed to `origin/main`.

## Approval

Approved by user 2026-10-26 with scroll scope = both (inner scrollbar +
outer page scroll removed).

# Routing

Verified navigation behavior (PLAN-028, on-device checked 2026-10-27).

## Android (`android/app`, single-Activity NavHost)

- Primary destinations (bottom bar / rail): Home, Files, Search, Activity,
  Settings. Secondary routes: `organizer`, `tools`, `devices`, `subjects`,
  `help/*` (9 destinations).
- All top-level navigations go through the shell guard
  (`AppShell.guardedNavigate`): when the organizer is mid-execute the guard
  shows the "Run in progress" dialog — **Keep running** (nav proceeds; the
  run continues in the app-scoped `OrganizerViewModel`), **Cancel run**
  (finalizes as `NeedsAttention`, navigates), **Stay** (dismiss).
- System Back on the organizer during execution raises the same dialog via
  `BackHandler`; outside a run it unwinds the NavController stack (no
  in-app Up affordance — PLAN-023).
- Organizer state survives navigation: the VM is app-scoped
  (`FileMindApp.organizerViewModel`), so leaving and returning resumes the
  exact step; `vm.reset()` is the only step clear.
- The operation-detail `ModalBottomSheet` (Activity + Files inspector)
  renders over the shell; the first tap on the scrim dismisses it before
  nav responds — known UX quirk tracked as F-B3.

## Windows (`src/FileMind.WinUI`, ContentFrame + NavigationView)

- Destinations: Dashboard, Files, Organizer, Tools, Search, Devices,
  Activity, Settings, Help & About.
- `OrganizerPage` uses `NavigationCacheMode.Required` — page state (and a
  running pipeline) survives sidebar navigation; the shell shows an
  equivalent "run in progress" confirmation when navigating away
  mid-execute.

## Mockup (`mockup/`)

- `FM.store.activeOp` is the shared running signal: navigation while an op
  is active raises the same guard; state persists in `FM.state.org`.

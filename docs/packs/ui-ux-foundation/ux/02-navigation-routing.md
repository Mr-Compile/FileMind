# Navigation & Routing UX

## Shared rules
- Back returns to the previous meaningful state.
- Preserve search/filter/selection where appropriate.
- Deep routes must recover gracefully if the target disappears.
- Never trap users inside an operation screen.
- Prevent duplicate route stacking.
- Restore sensible state after rotation/relaunch where platform appropriate.

## Windows
Sidebar + content route. Use breadcrumbs for filesystem hierarchy. Back/forward history is separate from filesystem `Up`.

## Android
Bottom navigation for a small set of primary destinations; secondary destinations via app bar/overflow/settings. Android system Back must behave predictably.

Unsaved changes and active destructive operations require explicit handling before leaving.

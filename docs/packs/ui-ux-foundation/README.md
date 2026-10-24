# File Organizer — UI/UX Foundation Pack

A platform-aware UI/UX specification for the File Organizer application.

## Design direction

- **Windows:** Fluent-inspired Minimal Utility UI
- **Android:** Material 3 Minimal Utility UI
- **Shared:** modern, semantic, accessible, color-coded, task-first design system
- **Supporting styles:** Bento Grid for dashboards; restrained glass for overlays/navigation; subtle gradients for branding and active emphasis
- **Avoid:** excessive glass, neumorphism, cyberpunk styling, decorative complexity, technical/raw error output

## Product principles

1. Native-feeling on each platform, consistent in meaning.
2. Status must never depend on color alone.
3. Every long-running operation exposes state and progress.
4. Destructive actions require clear consequence messaging and appropriate confirmation.
5. Errors are written for users, not developers.
6. Advanced/technical details are optional and hidden behind an explicit disclosure when useful.
7. Desktop supports density, keyboard, context menus, drag/drop, and multi-pane layouts.
8. Android prioritizes touch, bottom navigation, sheets, responsive layouts, and progressive disclosure.
9. Dark and light themes are first-class.
10. Icons, labels, motion, and semantic colors communicate together.

## Contents

- `foundations/` — tokens, themes, typography, icons, assets, motion, accessibility
- `components/` — reusable UI component specifications
- `ux/` — navigation, operations, status, validation, errors, restrictions, feedback
- `platforms/` — Windows and Android adaptations
- `screens/` — screen-level UX specifications
- `qa/` — UI/UX acceptance checklist

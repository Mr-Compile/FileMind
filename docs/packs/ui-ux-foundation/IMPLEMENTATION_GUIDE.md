# Implementation Guide for Coding AI

Treat this pack as the UI/UX source of truth unless the existing codebase establishes a compatible rule.

## Before coding
1. Audit the existing codebase and current UI patterns.
2. Do not invent framework/library details that are not present.
3. Identify existing reusable components/tokens before creating duplicates.
4. Produce a plan for the requested UI change.
5. Preserve working behavior and routes unless the plan explicitly changes them.

## Build order
1. Semantic tokens and themes
2. Typography and icon abstraction
3. Core components
4. Status/progress/error system
5. Navigation shell and routing states
6. File/device surfaces
7. Dashboard/Bento
8. Operation center
9. Responsive/platform adaptations
10. Accessibility and UI/UX QA

## Required component contracts
Every asynchronous component should model at least:
`idle | loading | success | empty | warning | error`

Every operation should model relevant states from:
`ready | queued | checking | running | paused | completed | completed_with_warnings | failed | cancelled | offline | unsupported | permission_required`

## Error boundary
Map internal exceptions/codes to stable product error categories. UI consumes friendly title, explanation, recovery actions, severity, and optional diagnostics—not raw exceptions.

## Platform rule
Share semantics and domain state. Adapt presentation and interaction to Windows vs Android rather than forcing pixel-identical UI.

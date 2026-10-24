# FileMind Cross-Platform UX Pack

AI-ready implementation specification for FileMind on Windows and Android.

## Goal
Create one consistent FileMind experience while adapting navigation, controls, density, gestures, and platform conventions for Windows desktop and Android mobile.

## Core rule
Share product concepts, terminology, statuses, flows, and safety behavior. Adapt presentation and interaction per platform.

## Suggested implementation order
1. Shared UX foundations
2. Navigation and routing
3. Safe file-operation flow
4. Help & About
5. Windows adaptations
6. Android adaptations
7. QA

Do not rewrite working architecture blindly. Audit existing code first, reuse existing components/routes/services where appropriate, and document conflicts before implementation.

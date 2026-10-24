# FileMind Foundation Pack

Backend-first planning pack for FileMind, an offline-first smart file organizer for Windows and Android with optional Groq AI.

## Foundation scope
This pack defines product rules, architecture, features, backend flows, routing, storage contracts, safety, and phased implementation. UI design is intentionally excluded.

## Recommended implementation order
1. Foundation and contracts
2. Windows core
3. Smart classification
4. Rule engine
5. Windows automation
6. File intelligence
7. Duplicates/storage
8. Groq AI
9. Android
10. Search/cross-platform

## Non-negotiable rule
AI may analyze and recommend. Only the deterministic Decision + Safety + Platform Action pipeline may mutate files.

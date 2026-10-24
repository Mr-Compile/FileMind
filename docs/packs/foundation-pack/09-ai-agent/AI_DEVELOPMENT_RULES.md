# AI/Vibe Coding Rules

1. Audit existing code before implementing a phase.
2. Implement only the active phase unless a dependency is required.
3. Do not bypass architectural boundaries.
4. UI never performs direct filesystem mutations.
5. AI never performs direct filesystem mutations.
6. All mutations require ActionPlan -> Safety -> Platform Adapter -> Verify -> History.
7. Add/update tests with every behavior change.
8. Preserve backward compatibility for rule/profile schemas or add migrations.
9. Never silently overwrite user files.
10. Never claim undo unless inverse operation is actually possible.
11. Local/offline behavior must remain functional when Groq is unavailable.
12. Do not hard-code API keys.
13. Treat Android permissions as capability grants, not as full filesystem access.
14. Keep platform-specific code outside domain/classifier modules.
15. Prefer small interfaces and deterministic logic over giant services.

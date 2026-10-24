# Modules

Inventory verified modules/components, their responsibilities, dependencies, and ownership boundaries. Do not create an inventory from assumptions.

| Module | Responsibility | Dependencies | Verified |
|---|---|---|---|
| `FileMind.Branding` | Platform-neutral brand color tokens (`BrandTokens`) + `ThemeMode`/`ThemePalette` resolution. Mirrors `branding/colors.json`; parity enforced by `FileMind.Branding.Tests`. | None (net10.0, no packages, no OS/UI APIs) | 2026-10-24 |
| `mockup/` | Dependency-free static HTML/CSS/JS interactive mockup of the approved UI — spec screens, organizer pipeline demo, console host simulator, architecture view. Links `branding/colors.css` directly so tokens can't drift. Sync rule: `docs/rules/MOCKUP_RULES.md`. | None (browser only; links `branding/colors.css`) | 2026-10-24 |

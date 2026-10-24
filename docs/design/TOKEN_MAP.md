# Token Map — ui-ux-foundation spec tokens → `branding/colors.json` → XAML

PLAN-014 M0.2. Source of truth: `branding/colors.json` (schemaVersion 1).
Spec: `docs/packs/ui-ux-foundation/foundations/02-design-tokens.md`.
Emitted XAML: `src/FileMind.WinUI/Themes/FmTheme.xaml`.

`DIRECT` = value taken verbatim from colors.json.
`DERIVED` = no colors.json token exists; value computed from canonical
tokens (formula recorded). DERIVED rows are candidates for a future
colors.json v2 — tracked here, not silently added.

## Color tokens

| Spec token | Source | Light | Dark | Provenance |
|---|---|---|---|---|
| `surface/background` | `light.background` / `dark.background` | `#F8FAFC` | `#0B1120` | DIRECT |
| `surface/primary` | `light.surface` / `dark.surface` | `#FFFFFF` | `#111827` | DIRECT |
| `surface/secondary` | mix(surface → surfaceElevated, 50%) | `#FAFBFD` | `#182131` | DERIVED — spec has 3 surface tiers, colors.json has 2 |
| `surface/elevated` | `light.surfaceElevated` / `dark.surfaceElevated` | `#F1F5F9` | `#1E293B` | DIRECT |
| `text/primary` | `light.textPrimary` / `dark.textPrimary` | `#0F172A` | `#F8FAFC` | DIRECT |
| `text/secondary` | `light.textSecondary` / `dark.textSecondary` | `#64748B` | `#94A3B8` | DIRECT |
| `text/disabled` | `text/secondary` @ 60% alpha | `#9964748B` | `#9994A3B8` | DERIVED — no token; alpha keeps theme link |
| `border/default` | `light.border` / `dark.border` | `#E2E8F0` | `#334155` | DIRECT |
| `accent/primary` | `brand.primary` | `#2563EB` | `#2563EB` | DIRECT (theme-invariant) |
| `status/info` | `semantic.info` | `#0284C7` | `#0284C7` | DIRECT |
| `status/success` | `semantic.success` | `#16A34A` | `#16A34A` | DIRECT |
| `status/warning` | `semantic.warning` | `#D97706` | `#D97706` | DIRECT |
| `status/error` | `semantic.error` | `#DC2626` | `#DC2626` | DIRECT |
| `status/destructive` | `semantic.destructive` | `#B91C1C` | `#B91C1C` | DIRECT |
| `status/offline` | `text/secondary` (muted, no direct token) | `#64748B` | `#94A3B8` | DERIVED |
| `status/neutral` | slate step between secondary/border | `#94A3B8` | `#64748B` | DERIVED |

## Spacing (theme-invariant, `space-N` → `FmSpaceN`)

`1:4  2:8  3:12  4:16  5:20  6:24  8:32  10:40  12:48` — DIRECT from spec.

## Radius (`FmRadius*`)

`Small:6  Medium:10  Large:14  XL:18  Pill:999` — DIRECT from spec
(pill emitted as `CornerRadius` at use-site, value 999 = full).

## Elevation tiers (usage rule, not a color)

`0 page/background · 1 card · 2 menu/popover · 3 dialog/modal` — DIRECT
from spec; applied via `ThemeShadow` receivers as screens land.

## Density

Comfortable default; `FmDensityCompact` boolean resource toggles compact
row heights in file-heavy views (M3+). Windows-only per spec.

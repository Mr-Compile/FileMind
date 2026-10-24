# Design Tokens

Tokens are semantic and platform-independent. Exact native implementation may differ.

## Spacing
`space-1: 4`, `space-2: 8`, `space-3: 12`, `space-4: 16`, `space-5: 20`, `space-6: 24`, `space-8: 32`, `space-10: 40`, `space-12: 48`.

## Radius
- Small: 6
- Medium: 10
- Large: 14
- XL/card hero: 18
- Pill/chip: full

## Semantic colors
Define light and dark values for:
- `surface/background`
- `surface/primary`
- `surface/secondary`
- `surface/elevated`
- `text/primary`
- `text/secondary`
- `text/disabled`
- `border/default`
- `accent/primary`
- `status/info`
- `status/success`
- `status/warning`
- `status/error`
- `status/destructive`
- `status/offline`
- `status/neutral`

Never encode status using color alone. Pair color with icon and label.

## Elevation
Use elevation sparingly:
- 0: page/background
- 1: card
- 2: menu/popover
- 3: dialog/modal

## Density
- Windows: comfortable default; compact option allowed for file-heavy views.
- Android: touch-safe comfortable density; no desktop-style compact controls.

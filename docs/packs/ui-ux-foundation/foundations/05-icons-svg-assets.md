# Icons, SVG & Multimodal Assets

## Icon strategy
Use one coherent icon family per platform implementation while preserving shared semantic names.

Core icon concepts:
- Home/dashboard
- Folder/file/archive/image/video/audio/document
- Search/filter/sort
- Organize/move/copy/rename
- Duplicate detection
- Cleanup/delete/trash
- Storage/drive/device/phone
- Connect/disconnect
- Refresh/rescan
- Play/pause/cancel/retry
- Success/info/warning/error
- Lock/permission/restricted
- Settings/theme/help/about
- History/activity/log
- Back/forward/up/breadcrumb
- More/overflow

## SVG rules
- Store reusable vector assets as SVG source.
- Prefer `currentColor` or semantic tokens where supported.
- Provide light/dark-compatible variants only when tokenized SVG is insufficient.
- Keep viewBox consistent.
- Remove unnecessary embedded raster data.
- Optimize paths without harming rendering.
- Decorative SVGs require no accessibility label; actionable icons do.

## Multimodal assets
Organize assets by purpose:
`icons/`, `illustrations/`, `empty-states/`, `onboarding/`, `device/`, `file-types/`, `brand/`.

Every meaningful image requires accessible text/semantics. Avoid imagery that blocks core functionality if unavailable.

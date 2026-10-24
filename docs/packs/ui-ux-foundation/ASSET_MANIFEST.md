# Asset Manifest

Recommended repository structure:

```text
assets/
├─ brand/
├─ icons/
│  ├─ actions/
│  ├─ navigation/
│  ├─ status/
│  ├─ files/
│  └─ devices/
├─ illustrations/
│  ├─ empty/
│  ├─ errors/
│  └─ onboarding/
└─ themes/
```

## Required semantic status SVG names
- status-info.svg
- status-success.svg
- status-warning.svg
- status-error.svg
- status-running.svg
- status-paused.svg
- status-offline.svg
- status-restricted.svg

Do not create duplicate SVGs solely to change color when the implementation can safely use semantic `currentColor`.

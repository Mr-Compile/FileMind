# Indicators & Progress

## Indicator types
- Determinate progress bar: measurable operations
- Indeterminate progress: unknown duration
- Spinner: short local wait
- Step indicator: multi-stage workflows
- Inline state icon: completed/failed/warning
- Storage meter: capacity, not operation progress
- Activity indicator: background work

## Long operations
Show:
- operation name
- current stage
- percentage when trustworthy
- processed/total when useful
- current item only if useful and not noisy
- elapsed/remaining time only when reliable
- pause/cancel when supported
- safe navigation behavior

Never fake progress percentages.

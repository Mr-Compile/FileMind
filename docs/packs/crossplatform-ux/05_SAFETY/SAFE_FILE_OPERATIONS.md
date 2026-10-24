# Safe File Operations

## Required flow
1. Analyze without mutation.
2. Preview exact proposed changes.
3. Summarize moved/renamed/deleted/unchanged counts.
4. Clearly state whether deletion will occur.
5. Confirm.
6. Execute with progress and cancellation when technically safe.
7. Present result summary.
8. Offer Undo when supported and View Activity always.

## Undo
Treat undo as a first-class capability. If an operation cannot be undone, state this before confirmation.

## Protected folders
Allow folders to be marked Protected. Analysis may be allowed, but mutation requires explicit authorization according to product policy.

## Smart suggestions
Show confidence where meaningful. Low-confidence changes should enter Needs Review.

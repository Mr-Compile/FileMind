# Dialogs, Sheets & Notifications

Use dialogs for decisions requiring immediate acknowledgement. Android may use bottom sheets for secondary choices and contextual actions.

Use snackbars/toasts only for brief, non-critical feedback.

Critical failure or destructive confirmation must not disappear automatically.

Confirmation structure:
- What will happen?
- What is affected?
- Can it be undone?
- Primary explicit action
- Safe cancel action

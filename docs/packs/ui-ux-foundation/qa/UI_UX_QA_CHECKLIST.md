# UI/UX QA Checklist

## Visual
- [ ] Light theme complete
- [ ] Dark theme complete
- [ ] System theme follows OS
- [ ] Semantic colors consistent
- [ ] No status relies only on color
- [ ] SVG/icon rendering verified
- [ ] Text scaling does not break critical layouts

## States
- [ ] Loading defined
- [ ] Empty defined
- [ ] Filtered-empty defined
- [ ] Offline/disconnected defined
- [ ] Permission-blocked defined
- [ ] Partial failure defined
- [ ] Fatal failure defined
- [ ] Disabled states explain restrictions where needed

## Operations
- [ ] Pre-checks occur before destructive work
- [ ] Progress is truthful
- [ ] Cancel/pause behavior is defined
- [ ] Navigation during operations is safe
- [ ] Completion result is summarized
- [ ] Retry/undo/open-location offered when supported

## Errors & validation
- [ ] No raw technical error is primary UI
- [ ] Error says what happened
- [ ] Error offers recovery
- [ ] Field validation is specific
- [ ] Technical details are optional
- [ ] Secrets/private tokens never appear in diagnostics

## Windows
- [ ] Keyboard navigation
- [ ] Visible focus
- [ ] Tooltips for ambiguous icon buttons
- [ ] Back/forward/up behavior verified
- [ ] Context menu behavior verified
- [ ] Drag/drop safety verified
- [ ] Resizing and minimum size verified

## Android
- [ ] Touch targets adequate
- [ ] System Back verified
- [ ] Rotation/state restoration verified where applicable
- [ ] Permission explanation precedes request where useful
- [ ] Bottom navigation/rail adaptation verified
- [ ] Sheets/dialogs do not obscure critical context

## Accessibility
- [ ] Contrast checked
- [ ] Screen-reader names present
- [ ] Focus order logical
- [ ] Reduced motion supported
- [ ] Icon + text semantics used for important state

# QA Checklist

- [ ] Approved plan scope implemented
- [ ] Acceptance criteria checked
- [ ] Relevant existing behavior regression-checked
- [ ] Error/failure paths checked
- [ ] Edge cases checked
- [ ] Relevant platform behavior checked
- [ ] Security/privacy impact checked when applicable
- [ ] Documentation matches verified implementation
- [ ] No unrelated changes introduced
- [ ] In-scope failures repaired and affected checks re-run
- [ ] Unverified/unrun checks clearly disclosed

## Organizer-pipeline additions (when the change touches organize)

- [ ] Classifications match the shared manifest fixtures (both platforms)
- [ ] Destinations resolve to the intended templates (`School/{Subject}/{SubKind}`, `{KindFolder}`, collection/custom)
- [ ] Created folders stay inside the granted scope (SAF tree on Android; no writes outside the source on Windows)
- [ ] Conflict policies honored — never silently overwritten
- [ ] Managed roots (`School/`, `Documents/`, `Images/`, `Videos/`, `Music/`, `Archives/`, `Work/`, `Other/`) excluded from re-scan
- [ ] Re-run idempotent — items already at their destination emit no action; no `(2)` self-duplicates
- [ ] Post-move verify uses the provider-returned URI (not the predicted destination) on SAF
- [ ] Per-item statuses truthful; `NeedsAttention` recoverable and not shown as actively running
- [ ] Undo restores committed items — including items of partially-failed runs — and refuses blind overwrite
- [ ] No data loss on cancel/crash (sources intact until moves verify)
- [ ] Behavior identical across Android/Windows/mockup within platform limits

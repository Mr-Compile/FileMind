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
- [ ] Destinations resolve to the intended templates (`School/{Subject}/{FileKind}`, `Files/{FileKind}`, collection/custom)
- [ ] Created folders stay inside the granted scope (SAF tree on Android; no writes outside the source on Windows)
- [ ] Conflict policies honored — never silently overwritten
- [ ] `organized/` output subtree excluded from re-scan
- [ ] Per-item statuses truthful; `NeedsAttention` recoverable and not shown as actively running
- [ ] Undo restores committed items — including items of partially-failed runs — and refuses blind overwrite
- [ ] No data loss on cancel/crash (sources intact until moves verify)
- [ ] Behavior identical across Android/Windows/mockup within platform limits

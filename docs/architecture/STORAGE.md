# Storage

Document verified storage mechanisms, schemas, lifecycle, migration behavior, paths, and platform differences.

## Verified: portable profile (PLAN-012)

- `organizer-profile.json` is schema-v1 (`schemas/profiles/organizer-profile.schema.json`): `schemaVersion`, `rules`, `collections`, `subjects`, `projects`, `namingTemplates`, `exclusions`, `preferences`.
- `ProfileService.ExportAsync` assembles the doc from the config repositories. Portable only — platform keys (watched folders, `watch.mode.*`, AI secrets/settings) are never exported; `preferences` carries `organization.mode` only.
- `profile import --preview` diffs by id (`added`/`changed`/`same`) without writing; `--apply` requires an explicit `--policy merge|skip`, creates a timestamped backup (`profile-backup-<ts>.json` next to the DB) first, then upserts.
- `profile backup` writes a full-fidelity dump (incl. settings, via `IProfileRepository.ExportJsonAsync`) for same-device restore; `profile restore` replays portable sections only — platform settings are never imported into a foreign device.
- Unknown newer `schemaVersion` and malformed JSON are rejected before any write. Import performs zero user-file operations.

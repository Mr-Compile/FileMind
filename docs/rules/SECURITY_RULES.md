# Security Rules

Audit existing security behavior before changes. Never expose secrets, weaken permission checks, bypass validation, or log sensitive values. Add project-specific security constraints only when verified or explicitly approved.

## Verified project constraints

- **No secrets in source.** API keys live in platform secret stores
  (`EnvSecretStore` dev path, `WindowsSecretStore`/DPAPI on Windows). No
  hard-coded credentials anywhere.
- **Permissions are authoritative.** Android file access happens only
  through SAF persisted tree grants, MediaStore, or app-private storage;
  writes use tree-scoped document URIs. Windows writes respect filesystem
  ACLs; failures map to typed `FileMindError`s.
- **Protected paths denied by default** on both platforms
  (system roots + user-declared paths).
- **Privacy gate before AI.** `AiPrivacyGate` enforces per-field consent,
  excludes protected paths, and minimizes payloads; extracted file
  content only leaves the device under explicit consent.
- **Never silently overwrite** — conflict policies require user intent.
- **No silent deletion** — the core organizer cannot delete files.
- **Structured, privacy-safe logging** — operation errors are typed and
  logged without embedding secrets or unnecessary personal data.

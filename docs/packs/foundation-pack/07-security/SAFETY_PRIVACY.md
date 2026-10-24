# Safety & Privacy

## File safety
Protected system/application locations, permission checks, source existence, destination validation, available space, collision policy, stable-file checks, post-operation verification.

## Conflict default
Keep Both or Ask. Never silently overwrite.

## AI privacy
AI is opt-in. Separate controls for filename, metadata, extracted document content, and future image content. Protected/private paths never go to AI. Minimize transmitted content. Provide local fallback.

## Credentials
Never hard-code shared Groq secrets in distributed clients. Personal/BYOK builds store keys in Windows credential storage or Android Keystore. A production hosted model should use an authenticated backend AI gateway with quotas/rate limits.

## Logging
Do not log document contents, API keys, or sensitive paths unnecessarily. Logs should support redaction and local clearing.

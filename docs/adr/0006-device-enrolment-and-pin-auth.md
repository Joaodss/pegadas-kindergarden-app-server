# 0006. Device enrolment + PIN authentication

- Status: Accepted
- Date: 2026-09-24

## Context

Each room shares one tablet. Educators need to identify themselves in seconds, sometimes offline. The real security boundary is the device.

## Decision

- **Device level**: a coordinator enrols the tablet into a room with email + password (MFA later). The tablet receives a **rotating device refresh token**, stored in Keychain/Keystore. The server keeps only its SHA-256 hash. If a consumed token is presented again, the whole token family is revoked.
- **Person level**: the educator picks their name and types a 6-digit **PIN**, hashed with **Argon2id** (m = 19 MiB, t = 2, p = 1). After 5 failures, lockout is progressive. The server issues a **15-minute access JWT** (ES256) with `sub`, `school_id`, `device_id`, `rooms` and `role`.
- Argon2 verification runs behind a concurrency limiter so CPU-bound hashing cannot starve the API.
- Authorization is secure by default: the fallback policy requires an authenticated user, and anonymous endpoints opt out explicitly. Every `{roomId}`/`{childId}` passes a membership check (BOLA/IDOR protection).
- The signing key lives in Key Vault. In Development only, an ephemeral key is generated at startup.
- No external IdP in the MVP. Tokens follow standard JWT/OIDC, so a managed IdP can take over when parents arrive.

## Consequences

- Custom auth code to write and test carefully (Phase 4). It uses proven primitives (JwtBearer, Argon2id) and no home-made crypto.

# 0005. Offline-first sync contract

- Status: Accepted
- Date: 2026-09-24

## Context

Kindergarten Wi-Fi is unreliable, and recording an entry must never wait for the network. Data is append-mostly, scoped to one room, and usually edited from one tablet, so conflicts are rare.

## Decision

- Entries have a **client-generated UUIDv7** id. **All entry writes go through `POST /v1/sync`**, including online edits and deletes (tombstones), so there is one idempotent write path.
- Push: a batch of at most 500 changes (1 MB) is upserted in **one round trip**: `INSERT … SELECT FROM unnest(...) ON CONFLICT (id) DO UPDATE … WHERE excluded.version > entry.version`. An invalid item is rejected individually and the rest of the batch still commits.
- Pull: `GET /v1/sync?since=<cursor>`. The cursor is opaque (base64 of `(modified_at, id)`). The server re-sends a ~5 s overlap window so rows from transactions that committed late are not missed. The client applies changes idempotently by `id` + `version`.
- Last write wins, by server `version`. History is preserved in the audit log.
- Two timestamps: `started_at` (from the tablet) and `received_at` (server) to detect clock skew.

## Consequences

- No sync engine to operate. The contract is small enough to test end to end.
- If real-time collaboration or frequent conflicts appear, revisit this with a sync engine (PowerSync, ElectricSQL) or change feeds.

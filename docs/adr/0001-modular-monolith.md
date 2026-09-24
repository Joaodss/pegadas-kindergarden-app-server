# 0001. Modular monolith

- Status: Accepted
- Date: 2026-09-24

## Context

A small team, low absolute volume (≤ ~100k entries/day at 50 schools) and a cohesive domain. Parents, alerts, photos and integrations are coming later, and they must not turn a plain monolith into a ball of mud.

## Decision

One deployable (`Pegadas.Api`) made of modules with explicit boundaries: Identity, Organization, Diary, Summaries, Audit, Notifications.

- Each module is a `Pegadas.Modules.<Name>` project whose types are `internal`, except the `<Name>Module : IModule` entry point.
- Other modules may reference only `Pegadas.Modules.<Name>.Contracts`: public interfaces, DTOs and integration events.
- Each module owns one PostgreSQL schema and is the only writer to it. Cross-module reads for reporting go through read-only SQL views.
- `tests/Pegadas.Architecture.Tests` fails the build when these rules are broken.

## Consequences

- One pipeline, one container, one database to operate.
- A module can later move to its own process (worker) or service by replacing in-memory calls with HTTP or messages. Its data is already isolated by schema.
- Contracts projects add some ceremony. That is the price of enforceable boundaries.

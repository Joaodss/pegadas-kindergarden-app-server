# 0003. PostgreSQL, one schema per module, `school_id` everywhere

- Status: Accepted
- Date: 2026-09-24

## Context

The data is relational: history, summaries, audit and permissions. The product will be multi-school, and retrofitting a tenant column later means migrating every table and every query.

## Decision

- **PostgreSQL 17/18**, managed. One database, **one schema per module** (`identity`, `organization`, `diary`, `audit`, `summaries`), plus `hangfire`.
- Every business table has `school_id uuid NOT NULL`. The tenant comes from the access token, **never from the request**.
- EF Core 10 **named query filters** in `ModuleDbContext`:
  - `Tenant`: `school_id = <current school>`. With no tenant in the context, queries return nothing (fail closed).
  - `SoftDelete`: `deleted_at IS NULL`.
- `TrackedEntityInterceptor` stamps `school_id` on insert and refuses to save a row that belongs to another tenant.
- Primary keys are `uuid`, generated as UUIDv7 (`Guid.CreateVersion7()`), on the client for diary entries.
- Ids stay plain `Guid` rather than strongly typed id structs. The sync upsert is raw SQL over arrays, and value converters on every key would add friction there for little safety gain. Revisit this if id mix-ups show up in review.
- `timestamptz` in UTC. The school's local "day" is a `date` computed with `school.time_zone`.
- Snake_case names via `EFCore.NamingConventions`.
- PostgreSQL Row-Level Security is added before the second real school or the parents' portal goes live.

## Consequences

- Cheap to run, and isolation is enforced in one place and covered by tests.
- Jobs must set the tenant explicitly (`ITenantSetter`) before they resolve a module `DbContext`.

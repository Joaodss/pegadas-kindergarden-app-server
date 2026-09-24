# Pegadas Server — Boilerplate Plan (.NET 10 LTS)

> Approved 2026-09-24. Phases 0–1 are implemented in this repo; see §10 for what comes next. Deviations decided during implementation are recorded in the ADRs (`docs/adr/`).

## Context

The `PegadasKindergardenAppServer` repo is empty (only `README.md`). The product context lives in `C:\Users\joaoa\Desktop\Projects\KindergardenApp\docs\`:
- `arquitetura-tecnica-pegadas.md`: the technical architecture. It sets the stack: modular monolith, ASP.NET Core .NET 10 LTS, PostgreSQL, offline-first sync, device + PIN auth, outbox + Hangfire in the same process, Azure App Service.
- `userflows-e-arquitetura.md`: the MVP educator flows (A–F): start the day, quick entry, group entry, nap, history, close the day.

(The doc references `modelo-de-dados-e-permissoes.md`, but that file is missing. The data model below is derived from the two docs. Check it against that file if it exists somewhere.)

This plan sets up the server boilerplate and the order of work after it. It follows the architecture doc's decisions and adds the concrete details the doc leaves open: libraries, folder layout, tables, endpoints, cross-cutting infrastructure and performance choices.

**Decisions confirmed with the user:** code, DB and API all use **English** names, with a PT→EN glossary. Local dev uses **Docker Compose**.

---

## 1. Glossary (PT domain → EN code)

| PT | EN (code/API/DB) |
|---|---|
| Escola | School (tenant, `school_id`) |
| Sala | Room |
| Criança | Child |
| Educador/a · Coordenação · Encarregado | Educator · Coordinator · Guardian (roles of `StaffMember` / future `Guardian`) |
| Registo | **DiaryEntry** (avoids clashing with the C# `record` keyword) |
| Tipo de registo | EntryType |
| Dia da sala · Fechar o dia · Lacunas | RoomDay · CloseDay · Gaps |
| Sesta · Presença (chegada/saída) | Nap · Attendance (arrival/departure = system entry types) |
| Ficha (alergias, notas) | ChildHealthProfile |
| Registo em grupo | Group entry (`group_id`) |
| Dispositivo · Auditoria | Device · Audit |

---

## 2. Technology stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | **.NET 10 LTS**, C# 14 | Supported until Nov 2028 |
| API style | **Minimal APIs**, route groups `/v1`, `TypedResults` | Lowest overhead, AOT-friendly, OpenAPI metadata |
| OpenAPI | Built-in `Microsoft.AspNetCore.OpenApi` (OpenAPI 3.1) + **Scalar** UI (dev/staging only) | Build-time doc generation (`Microsoft.Extensions.ApiDescription.Server`) → `openapi/v1.json` artifact for the mobile TS client |
| Validation | Built-in .NET 10 minimal-API validation (`AddValidation()` + DataAnnotations), custom validators where needed | Avoids FluentValidation dependency |
| Use-case dispatch | Plain handler classes injected into endpoints (no MediatR, which has a commercial license now) | Vertical slices |
| ORM | **EF Core 10 + Npgsql**, one `DbContext` per module, snake_case naming (`EFCore.NamingConventions`) | EF 10 **named query filters** for `Tenant` + `SoftDelete` |
| DB | **PostgreSQL 17/18** (latest version the Azure Flexible Server offers) | `uuid` PKs from `Guid.CreateVersion7()`, `jsonb`, partial indexes |
| Jobs | **Hangfire + Hangfire.PostgreSql** (schema `hangfire`) | Outbox processor + cron jobs; `Workers:Enabled` flag |
| Caching | **HybridCache** (in-memory only, no Redis) | Entry-type catalog, room membership, min-version |
| Auth | JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`), ES256 signing key in Key Vault | Custom device + PIN flow; **Argon2id** via `Konscious.Security.Cryptography.Argon2` behind `IPasswordHasher` |
| Rate limiting | Built-in `Microsoft.AspNetCore.RateLimiting` | |
| Observability | **OpenTelemetry** (traces/metrics/logs) → OTLP locally, **Azure Monitor exporter** in cloud | Logs without PII via `Microsoft.Extensions.Compliance.Redaction` + data classification |
| Resilience | `Microsoft.Extensions.Http.Resilience` for outbound HTTP (future push/email) | |
| Tests | xUnit v3, **Testcontainers.PostgreSql**, Respawn, **NetArchTest** (architecture), Bogus (synthetic data) | |
| Container | Multi-stage Dockerfile → `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra` (non-root; includes ICU + tzdata for the school time zone) | |
| IaC / CI | **Bicep**, **GitHub Actions** with OIDC to Azure, GHCR or ACR Basic | |

Repo hygiene: `global.json` (pin SDK 10.0.x), `Directory.Build.props` (nullable, `TreatWarningsAsErrors`, analyzers, `InvariantGlobalization=false`), **Central Package Management** (`Directory.Packages.props`), `.editorconfig`, `.gitattributes`, `.dockerignore`, Dependabot, `docs/adr/`.

---

## 3. Folder structure

```
PegadasKindergardenAppServer/
├─ Pegadas.slnx
├─ global.json · Directory.Build.props · Directory.Packages.props · .editorconfig
├─ docker-compose.yml               ← postgres:18, mailpit, aspire-dashboard (OTLP UI), api
├─ Dockerfile
├─ docs/
│  ├─ adr/ 0001-modular-monolith.md … 00NN-*.md
│  ├─ glossary.md · boilerplate-plan.md (this plan)
├─ infra/
│  ├─ main.bicep · main.parameters.{staging,prod}.json
│  ├─ modules/ appservice.bicep · postgres.bicep · keyvault.bicep · storage.bicep · monitoring.bicep · network.bicep · dns.bicep · identity.bicep
│  └─ sql/ roles.sql                ← pegadas_migrator (owner) / pegadas_app (DML; INSERT+SELECT only on audit)
├─ .github/workflows/ ci.yml · deploy.yml · codeql.yml
├─ src/
│  ├─ Pegadas.Api/                  ← host only: Program.cs, composition, middleware, auth, rate limiting, OpenAPI, health
│  ├─ Pegadas.SharedKernel/         ← pure types: Entity, IDomainEvent, Result/Error, tenant/soft-delete/tracking markers (ids stay `Guid`, see ADR-0003)
│  ├─ Pegadas.BuildingBlocks/       ← shared infra: ModuleDbContext base, interceptors (audit fields, outbox, soft delete),
│  │                                   ITenantContext, ICurrentActor, outbox processor, Hangfire setup, ProblemDetails mapping,
│  │                                   endpoint filters (room access), JSON source-gen options, OTel setup
│  ├─ Modules/
│  │  ├─ Identity/     Pegadas.Modules.Identity + .Contracts
│  │  ├─ Organization/ Pegadas.Modules.Organization + .Contracts
│  │  ├─ Diary/        Pegadas.Modules.Diary + .Contracts        ← core: entries, naps, room day, sync
│  │  ├─ Summaries/    Pegadas.Modules.Summaries                 ← read-only; SQL views over diary/organization
│  │  ├─ Audit/        Pegadas.Modules.Audit + .Contracts
│  │  └─ Notifications/Pegadas.Modules.Notifications + .Contracts ← INotificationSender, IPushChannel, IEmailChannel (no-op)
│  └─ Tools/
│     └─ Pegadas.Tools.Seeder/      ← synthetic data (Bogus) for local/staging ONLY
└─ tests/
   ├─ Pegadas.Architecture.Tests/   ← module boundaries: only *.Contracts are referenced across modules
   ├─ Pegadas.Modules.{Identity,Organization,Diary,Summaries}.Tests/  ← unit (domain + handlers)
   └─ Pegadas.Api.IntegrationTests/ ← WebApplicationFactory + Testcontainers Postgres; authz/tenant-isolation suites
```

**Inside each module** (vertical slices):
```
Pegadas.Modules.Diary/
  DiaryModule.cs                ← AddDiaryModule(IServiceCollection, IConfiguration) + MapDiaryEndpoints(IEndpointRouteBuilder)
  Domain/                       ← DiaryEntry, RoomDay, domain events (EntryRecorded, DayClosed…)
  Features/
    Sync/Push/  PushSyncEndpoint.cs · PushSyncHandler.cs · PushSyncRequest.cs · PushSyncResponse.cs
    Sync/Pull/  …
    GetRoomToday/ … CloseDay/ …
  Infrastructure/
    DiaryDbContext.cs · Configurations/ · Migrations/ · Jobs/
  Internal/                     ← everything `internal`; the module exposes only DiaryModule + Contracts
```
Boundary rules (enforced by NetArchTest): modules reference only other modules' `.Contracts`. Each module writes only to its own schema. Cross-module reads go through Contracts interfaces or read-only SQL views (Summaries).

---

## 4. Database structure

One PostgreSQL database with **one schema per module** plus `hangfire`. Every business table has `school_id uuid NOT NULL`. Timestamps are `timestamptz` in UTC. A `day date` column is computed in the school's time zone (`school.time_zone`, e.g. `Europe/Lisbon`). IDs are UUIDv7. Soft delete uses `deleted_at`. Optimistic concurrency uses `version int`, mapped to EF's concurrency token.

### `organization`
| Table | Key columns |
|---|---|
| `school` | id, name, time_zone, settings jsonb (inactivity lock minutes, retention days…), created_at |
| `room` | id, school_id, name, color, sort_order, archived_at, updated_at, version |
| `staff_member` | id, school_id, display_name, email (nullable), role (`educator`/`coordinator`), active, updated_at |
| `room_staff` | room_id, staff_member_id, school_id (PK room_id+staff_member_id) |
| `child` | id, school_id, room_id, first_name, last_name, preferred_name, birth_date, photo_ref (future), archived_at, retention_until, updated_at, deleted_at, version |
| `child_health_profile` | child_id (PK), school_id, allergies, dietary_restrictions, important_notes, updated_at, version. Kept in a **separate table** because it is Art. 9 GDPR data; every read is audited |
| `entry_type` | id, school_id, key (`pee`,`poop`,`meal`,`fluids`,`nap`,`activity`,`mood`,`observation`,`note`,`arrival`,`departure`), name, icon, color, measure_kind (`none`/`count`/`volume_ml`/`duration`/`scale`/`text`), unit, quick_options jsonb, is_system, active, updated_at |
| `room_entry_type` | room_id, entry_type_id, school_id, sort_order, visible (quick-bar config per room) |

### `identity`
| Table | Key columns |
|---|---|
| `staff_credential` | staff_member_id (PK), school_id, password_hash (nullable, for enrolment/coordination), pin_hash (Argon2id), failed_pin_count, locked_until, mfa_secret (future), updated_at |
| `device` | id, school_id, room_id, name, platform, app_version, enrolled_by, enrolled_at, last_seen_at, last_sync_at, revoked_at |
| `device_refresh_token` | id, device_id, school_id, token_hash (SHA-256), family_id, expires_at, consumed_at, revoked_at, replaced_by_id. Rotation plus reuse detection: if a consumed token is presented again, the whole family is revoked |

### `diary`
| Table | Key columns / notes |
|---|---|
| `entry` | As in doc §3.3, translated: id (client UUIDv7), school_id, room_id, child_id, entry_type_id, day, started_at, ended_at, value_num numeric(10,2), unit, value_text, details jsonb, origin (`individual`/`group`), group_id, author_id, device_id, received_at, modified_at, deleted_at, version |
| `room_day` | school_id, room_id, day (PK room_id+day), status (`open`/`closed`), closed_by, closed_at, reopened_by, reopened_at, version |
| `outbox_message` | id, occurred_at, type, payload jsonb, processed_at, attempts, last_error (one per module schema; the same shape in `organization`/`identity`) |
| `processed_message` | message_id, handler, processed_at (for idempotent handlers) |

Indexes on `diary.entry`:
- `(school_id, child_id, started_at DESC)`: history
- `(school_id, room_id, day)`: today and day summary
- `(school_id, room_id, modified_at, id)`: sync pull cursor
- Partial `(room_id) WHERE ended_at IS NULL AND deleted_at IS NULL AND entry_type_id = <nap>`: ongoing naps. In practice this is keyed on a `kind` column copied from the type (`kind = 'nap'`), because an index predicate can't reference a type id that differs per school

### `audit`
- `audit_event`: id, school_id, actor_staff_id, device_id, action (`create`/`update`/`delete`/`read_sensitive`/`revoke_device`/…), entity_type, entity_id, occurred_at, changes jsonb (diff), correlation_id. **Append-only**: `pegadas_app` gets INSERT + SELECT only. Index `(school_id, entity_type, entity_id, occurred_at)`.

### `summaries`
- SQL **views only** at first (`summaries.v_child_daily_counts`, `summaries.v_room_day_gaps`), created by migrations in the Summaries module. They become a pre-computed `daily_summary` read model later, only when metrics call for it.

### Migrations
- One EF migrations set per DbContext. CI builds **EF migration bundles** (`efbundle-<module>`) and applies them before deploy, **never at app startup**.
- `infra/sql/roles.sql` creates `pegadas_migrator` / `pegadas_app` and the grants.
- **Expand/contract** rule documented in an ADR (two app versions in the field).

---

## 5. API endpoints (MVP)

Conventions:
- `/v1` prefix and `application/problem+json` errors (RFC 9457).
- The `X-App-Version` header is checked by middleware. Returns **426** below the minimum version.
- camelCase JSON, enums as strings.
- Keyset (cursor) pagination.
- Tenant comes from the token and **never from the request**.
- Every `{roomId}`/`{childId}` passes a membership check (BOLA/IDOR protection).

| Area | Method & route | Auth | Purpose |
|---|---|---|---|
| Ops | `GET /health/live`, `GET /health/ready` | none | Liveness; readiness (DB + Hangfire) |
| Meta | `GET /v1/meta/min-version` | none | Forced-update check |
| Auth | `POST /v1/auth/device/enroll` | email + password (coordinator; MFA later) | Registers the tablet to a room → device refresh token |
| Auth | `GET /v1/auth/device/staff` | device token | Staff list for the PIN screen (room of the device) |
| Auth | `POST /v1/auth/pin` | device token + staffId + PIN | Access JWT (15 min: `sub`, `school_id`, `device_id`, `rooms`, `role`) + rotated device token |
| Auth | `POST /v1/auth/refresh` | device refresh token | Rotate. Reuse → family revoked |
| Auth | `POST /v1/auth/lock` | access | End the person session (device stays enrolled) |
| Devices | `GET /v1/devices` · `POST /v1/devices/{id}/revoke` | coordinator | Manage/revoke tablets (audited) |
| Staff | `GET/POST /v1/staff` · `PATCH /v1/staff/{id}` · `POST /v1/staff/{id}/pin` | coordinator (PIN change: self) | Manage educators, set/reset PIN |
| Rooms | `GET/POST /v1/rooms` · `PATCH /v1/rooms/{id}` · `PUT /v1/rooms/{id}/staff` | coordinator (GET: educator scoped) | Rooms + assignments |
| Children | `GET /v1/rooms/{roomId}/children` · `POST /v1/children` · `GET/PATCH /v1/children/{id}` · `POST /v1/children/{id}/archive` | educator (own rooms) | Settings › Room and children |
| Children | `GET/PUT /v1/children/{id}/health-profile` | educator (own rooms) | Sensitive data. **Read is audited** |
| Entry types | `GET /v1/entry-types` · `POST/PATCH /v1/entry-types/{id}` | coordinator (GET: all) | School catalog |
| Entry types | `GET/PUT /v1/rooms/{roomId}/entry-types` | educator (own rooms) | Quick-bar config per room |
| **Sync** | `POST /v1/sync` | educator | Batch (≤500 changes, ≤1 MB) of entry upserts/tombstones → `{accepted[], rejected[{id, code}], serverTime}` |
| **Sync** | `GET /v1/sync?since=<cursor>&limit=` | educator/device | Changes for the device's room since cursor: entries, children, entry types, room_day → `{changes, nextCursor, hasMore}` |
| Diary | `GET /v1/rooms/{roomId}/today` | educator | Snapshot: children + attendance + counters + ongoing naps + day status (first load / resync) |
| Diary | `GET /v1/rooms/{roomId}/days/{date}/summary` | educator | Day summary + gaps |
| Diary | `POST /v1/rooms/{roomId}/days/{date}/close` · `…/reopen` | educator · coordinator | Close day (one transaction: status + audit + outbox `DayClosed`) |
| Summaries | `GET /v1/children/{id}/history?from=&to=&types=&cursor=` | educator | Timeline, keyset-paginated |
| Summaries | `GET /v1/children/{id}/summary?period=day\|week\|month&date=` | educator | Indicators per period |
| Jobs UI | `/ops/hangfire` | coordinator + `Ops:DashboardEnabled` | Off in prod by default |

**All entry writes go through `/v1/sync`**, including online edits and undo-after-sync. That gives one idempotent code path.

Sync semantics:
- `INSERT … ON CONFLICT (id) DO UPDATE WHERE excluded.version > entry.version`.
- An invalid item is rejected individually and the rest of the batch still commits.
- `started_at` comes from the tablet and `received_at` from the server, so clock skew can be detected.
- The cursor is opaque base64 of `(modified_at, id)`. Pull uses a small overlap window (~5 s) so concurrent commits are not missed. The client applies changes idempotently (by id + version).

**Future (not in boilerplate):** `/v1/portal/...` (guardians), `/v1/children/{id}/export`, photo upload via signed URL, alerts.

---

## 6. Cross-cutting infrastructure (in `Pegadas.Api` + `Pegadas.BuildingBlocks`)

1. **Composition**: `Program.cs` calls `builder.AddPegadasDefaults()` (OTel, health, problem details, JSON, options validation). Then each module calls `AddXModule()` and `app.MapXEndpoints()` under `app.MapGroup("/v1")`.
2. **Configuration**:
   - Strongly typed options with `ValidateDataAnnotations().ValidateOnStart()`.
   - Azure Key Vault config provider via **managed identity** (`DefaultAzureCredential`).
   - `appsettings.{Development,Staging,Production}.json`, with no secrets in the repo.
3. **Tenant and actor**: `ITenantContext` / `ICurrentActor` are populated from JWT claims. EF named filters `Tenant` and `SoftDelete` are applied in the `ModuleDbContext` base. Jobs set the tenant explicitly.
4. **EF interceptors**:
   - `AuditableEntityInterceptor` sets `modified_at` and bumps `version`.
   - `OutboxInterceptor` turns domain events into `outbox_message` rows in the same transaction.
   - `AuditInterceptor` writes `audit_event` through the Audit Contracts in the same transaction.
5. **Outbox + jobs**:
   - A hosted `OutboxProcessor` per module polls every ~2 s (a `PeriodicTimer`, not a Hangfire recurring job, which would write ~8.6k job rows per module per day; see ADR-0007). It uses `SELECT … FOR UPDATE SKIP LOCKED LIMIT n`, dispatches to in-process handlers, and applies retry/backoff.
   - `processed_message` makes handlers idempotent.
   - Everything is guarded by `Workers:Enabled`, so the API and worker can later run as separate processes from the same image.
6. **AuthN/AuthZ**:
   - JWT bearer validation (issuer, audience, ES256, 30 s clock skew).
   - Policies `Educator` and `Coordinator`.
   - A reusable **`RoomAccessFilter` / `ChildAccessFilter`** endpoint filter resolves the room from the route and checks it against the token's `rooms` claim. Membership is looked up via cached Organization contracts for child→room.
   - Device revocation is checked on refresh/PIN and via a cached "revoked devices" set.
7. **PIN security**:
   - Argon2id (m=19 MiB, t=2, p=1).
   - 5 failures trigger progressive lockout, stored in `staff_credential`.
   - Verification runs under a `SemaphoreSlim`/concurrency limiter so CPU-heavy hashing can't starve the API.
8. **Rate limiting policies**:
   - `auth`: fixed window per IP, strict.
   - `device`: token bucket partitioned by `device_id` claim.
   - `sync`: concurrency 1 per device.
   - The global limiter returns 429 + `Retry-After`.
9. **HTTP pipeline order**:
   - ForwardedHeaders (App Service), HSTS, security headers.
   - ExceptionHandler → ProblemDetails (no internal details), request size limits.
   - App-version middleware, rate limiter, authN/authZ, response compression, endpoints.
   - CORS off (enabled later only for the portal domain).
10. **Observability**:
    - ActivitySource/Meter per module, with custom metrics: `sync.batch.size`, `sync.rejected`, `sync.lag_seconds` per device, `outbox.pending`, `pin.failures`.
    - Correlation id in ProblemDetails `traceId`.
    - **No PII in logs**: `[PersonalData]`/`[SensitiveData]` classifications + redactors, and only IDs are logged.
11. **Errors**: `Result<T>`/`Error` in SharedKernel, mapped to `TypedResults.Problem` with stable `type` URIs and error codes that the mobile app can switch on.
12. **Notifications stub**: `INotificationSender` → `NoOpNotificationSender`, and `IEmailChannel` → Mailpit (SMTP) locally, no-op in prod MVP.
13. **Time**: `TimeProvider` is injected everywhere (testable). `ISchoolClock` converts to the school's local day.
14. **Data Protection**: keys persisted in the DB (used by the Hangfire dashboard cookie). Not critical, but avoids restarts invalidating cookies.

---

## 7. Performance choices

- **Minimal APIs + System.Text.Json source generation** (`JsonSerializerContext` per module, registered in `ConfigureHttpJsonOptions`). This avoids reflection and cuts allocations.
- **Sync push as a single round trip**: bulk upsert via `INSERT … SELECT FROM unnest(@ids, @…) ON CONFLICT …`, using Npgsql array parameters with `RETURNING` to classify accepted items. It does not use per-row EF tracking. Validation runs against a **cached entry-type catalog** and **cached room roster** (HybridCache, invalidated by outbox events).
- **Reads**:
  - `AsNoTracking` + projection to DTOs (`Select`) on every GET.
  - **EF compiled queries** for hot paths (`today`, `sync pull`).
  - Keyset pagination only (no OFFSET).
  - The `today` endpoint is one query per concern, then assembled in memory. No N+1.
- **DbContext pooling** (`AddDbContextPool`). The tenant is set on the pooled context through a scoped `ITenantContext` accessor resolved per request (the EF 10 pattern for pooled contexts with per-request state).
- **Npgsql**: `NpgsqlDataSource` singleton with connection pooling, `jsonb` mapped to typed POCOs (`OwnsOne(...).ToJson()`), and prepared statements via EF.
- **Response compression** (Brotli/Gzip) on large JSON (sync pull, history). Output cache / `ETag` + `If-None-Match` on `entry-types` and `min-version`.
- **Request limits**: Kestrel `MaxRequestBodySize` per endpoint (sync 1 MB) and a sync batch cap of 500.
- **Runtime**:
  - Server GC with DATAS (the .NET 10 default).
  - `TieredPGO` on (default), and ReadyToRun publish for faster cold start on App Service restarts.
  - Chiseled image for smaller size and faster pulls.
- **CPU-heavy work off the request path**: Argon2 is concurrency-limited. Exports and notifications run only in jobs.
- **Index strategy** from §4. Partitioning or BRIN only after tens of millions of rows. A benchmark project (`BenchmarkDotNet`) is optional, to track sync throughput later.
- **Load test target**: 50 schools at the meal-time peak (~dozens of req/s). Sync p95 < 200 ms, today p95 < 150 ms on App Service B2 + PG B1ms. Validate with k6 against staging.

---

## 8. External services & infrastructure

**MVP (Azure, EU region: Spain Central or West Europe), all in Bicep:**

| Resource | SKU / notes |
|---|---|
| App Service Plan Linux | B2. Two web apps: `pegadas-api` (prod) and `pegadas-api-staging`, container, Always On, managed TLS, HTTPS only, system-assigned managed identity |
| Azure Database for PostgreSQL Flexible Server | B1ms, 32 GB, PITR 14 days. **VNet integration + private access** (health data). Separate prod/staging servers. Staging can be stopped outside working hours |
| VNet + subnets + private DNS zone | App Service VNet integration → PG private access |
| Key Vault | RBAC mode. JWT signing key, DB connection strings. Web apps get `Key Vault Secrets User` |
| Storage Account | Immutable (WORM) container for logical backups. Future: photos/exports |
| Log Analytics + Application Insights | OTel via Azure Monitor exporter, 30–90-day retention. Alerts: 5xx rate, p95 latency, job failures, DB CPU/storage, outbox backlog |
| Azure DNS | `api.pegadas.pt`, `staging-api.pegadas.pt` |
| Container registry | GHCR (free) or ACR Basic |
| Entra ID app + federated credential | GitHub Actions OIDC (no long-lived secrets) |
| Backups | Managed PITR, plus a daily logical backup: Azure Backup vault long-term retention for PG Flexible (immutable) as preferred, or a scheduled `pg_dump` job as fallback. Quarterly restore test (runbook in `docs/`) |

**Local (docker-compose):** `postgres:18`, `mailpit` (SMTP UI), `mcr.microsoft.com/dotnet/aspire-dashboard` (OTLP traces/logs/metrics UI), `api` (optional; normally run via `dotnet run`).

**Future (interfaces defined now, no implementation):**
- Expo Push Service or FCM v1 (`IPushChannel`)
- Azure Communication Services Email (`IEmailChannel`)
- Blob storage with signed URLs (`IFileStorage`)
- Managed IdP for guardians (Entra External ID)
- Sentry on the mobile side only (EU region)

---

## 9. CI/CD

- **`ci.yml` (PRs)**:
  - restore → build (warnings as errors) → unit tests + architecture tests.
  - Integration tests (Testcontainers) → generate `openapi/v1.json` and fail if it changed without being committed (the contract check for mobile).
  - `dotnet format --verify-no-changes`, vulnerable-package check, CodeQL (separate workflow), secret scanning.
- **`deploy.yml`**:
  - On merge to `main`: build image (tag = SHA) → push → build EF bundles → apply to **staging** → deploy staging → smoke test (`/health/ready`, `/v1/meta/min-version`).
  - **Production**: manual approval (GitHub environment) → bundles → deploy → health check.
- **Bicep**: `what-if` on PRs that touch `infra/`, deployed with a manual approval.

---

## 10. Implementation phases (next steps)

Each phase ends deployable and green in CI.

| # | Phase | Deliverables |
|---|---|---|
| 0 | **Repo foundation** | `global.json`, solution + projects skeleton (§3), build props, CPM, editorconfig, Dockerfile, docker-compose, `ci.yml`, ADRs 0001–0010 (the doc §10.3 decisions + naming + Hangfire + sync cursor), glossary |
| 1 | **Host + building blocks** | `Pegadas.Api` pipeline (§6.1–6.2, 6.9–6.11, 6.13), health checks, OpenAPI + Scalar, ProblemDetails, app-version middleware, rate-limiter skeleton, OTel. SharedKernel (ids, Result, events). ModuleDbContext base + interceptors + tenant filters. Outbox + Hangfire wiring with `Workers:Enabled`. NetArchTest rules. Integration test harness |
| 2 | **Walking skeleton deploy** | Bicep (§8), `deploy.yml`, staging live at `staging-api…` returning health + min-version. This comes **before features**, as the doc recommends |
| 3 | **Organization** | school/room/staff/child/health-profile/entry-type tables + migration. System entry types seeded per school. Endpoints (§5). Audited health-profile reads. HybridCache catalog/roster |
| 4 | **Identity** | Credentials, Argon2id hasher, enrol, staff list, PIN, refresh rotation with reuse detection, lock, device revoke. JWT issuance, policies, Room/Child access filters, `auth` rate limits |
| 5 | **Diary + sync (core)** | `entry`, `room_day` tables. Bulk-upsert sync push, cursor pull, today snapshot, day summary + gaps, close/reopen day (single transaction + outbox `DayClosed`). Audit writes |
| 6 | **Summaries** | Views + history (keyset) + period summary endpoints |
| 7 | **Jobs** | Expired-token cleanup, GDPR retention (anonymise archived children after `retention_until`), unclosed-day flag, sync reconciliation (devices with stale `last_sync_at`) |
| 8 | **Hardening** | Authorization test matrix per endpoint, cross-tenant access tests, k6 load test on staging, alert rules, restore-test runbook, security headers review against the OWASP API Top 10 |
| 9 | **Seeder + mobile handoff** | `Pegadas.Tools.Seeder` (synthetic data for local/staging), published OpenAPI artifact for the Expo TS client generator |

Parallel non-code tracks from the doc: start the DPIA/AIPD and the Art. 28 processor contract, and write ADRs for each decision.

---

## 11. Verification

- **Local**:
  - Run `docker compose up -d`, then the EF bundles or `dotnet ef database update` per context, then `dotnet run --project src/Pegadas.Api`.
  - Open `/scalar/v1`, and check that `/health/ready` returns 200 and that traces show up in the Aspire dashboard (http://localhost:18888).
- **Tests**:
  - `dotnet test` runs unit, architecture (module boundaries) and integration tests (Testcontainers Postgres).
  - Key integration scenarios: enrol → PIN → sync push (resend the same batch twice, so there are no duplicates) → pull from a second device → close day → outbox job processes `DayClosed`.
  - Cross-tenant and cross-room access return 404/403.
  - Revoked-device refresh fails. A reused refresh token revokes the family.
- **Contract**: CI fails if `openapi/v1.json` drifts.
- **Staging**: smoke tests after every deploy, and a k6 scenario simulating the meal-time peak against the §7 p95 targets.

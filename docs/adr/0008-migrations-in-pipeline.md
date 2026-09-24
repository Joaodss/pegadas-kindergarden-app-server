# 0008. Migrations run in the pipeline, expand/contract

- Status: Accepted
- Date: 2026-09-24

## Context

Tablets update apps slowly, so the server must accept the last two app versions. Running migrations at application startup races across instances and gives the app DDL rights.

## Decision

- EF Core migrations, one set per module `DbContext`, reviewed in PRs. History table: `<schema>.__ef_migrations_history`.
- CI builds **EF migration bundles** and applies them as a pipeline step **before** deploying, **never at startup**.
- Two database roles: `pegadas_migrator` owns the schemas; `pegadas_app` gets DML only, and only `INSERT`/`SELECT` on `audit.audit_event`.
  - Exception: Hangfire installs and upgrades its own `hangfire` schema at startup, so `pegadas_app` owns that schema only.
- **Expand/contract** for breaking changes: first add the new column and write both, then drop the old one once no supported app version uses it.

## Consequences

- A deploy is "migrate, then swap". A failed migration stops the deploy before new code runs.

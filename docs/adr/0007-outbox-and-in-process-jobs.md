# 0007. Outbox and in-process background jobs

- Status: Accepted
- Date: 2026-09-24

## Context

Some work must not run inside the HTTP request: notifications, retention, reconciliation, and later exports. Volume is low, and a message broker would be one more service to run with no current consumer that needs it.

## Decision

- **Transactional outbox**: domain events raised by entities are written to `<schema>.outbox_message` by `OutboxInterceptor`, **in the same transaction** as the data.
- **Outbox processing**: `OutboxProcessor<TContext>` is a hosted service per module. It polls every ~2 s with `SELECT … FOR UPDATE SKIP LOCKED`, dispatches to in-process `IDomainEventHandler<T>` handlers, and retries with a capped attempt count. `processed_message` records `(message, handler)` so a handler is not re-run for a message once its run has been committed.
  - *Deviation from the plan:* the plan had a Hangfire recurring job every 5–10 s. That would write ~8.6k Hangfire job rows per module per day for mostly empty polls. A `PeriodicTimer` loop is cheaper, and `SKIP LOCKED` keeps it safe with several instances.
- **Hangfire + PostgreSQL storage** (schema `hangfire`) runs **cron jobs** and **fire-and-forget jobs with retries**. The dashboard is at `/ops/hangfire`, coordinator only, and off by default.
- Everything above runs only when `Workers:Enabled=true`. The same image can later run as API (`false`) and worker (`true`) processes without code changes.

## Consequences

- Delivery is at-least-once. Handlers must be idempotent, which also protects a future broker migration.
- Moving to a broker later only swaps the outbox dispatcher. Producers do not change.

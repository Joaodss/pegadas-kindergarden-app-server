# Architecture Decision Records

Each ADR records one decision: the context, what was decided, and what it costs. ADRs are immutable once accepted. To change a decision, write a new ADR that supersedes the old one and update the old one's status.

Source of most decisions: `KindergardenApp/docs/arquitetura-tecnica-pegadas.md` (§10.3 "Decisões a tomar agora").

| # | Title | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith | Accepted |
| [0002](0002-dotnet-minimal-apis-vertical-slices.md) | .NET 10, Minimal APIs and vertical slices | Accepted |
| [0003](0003-postgresql-schema-per-module-multi-tenancy.md) | PostgreSQL, one schema per module, `school_id` everywhere | Accepted |
| [0004](0004-english-naming.md) | English names in code, database and API | Accepted |
| [0005](0005-offline-first-sync.md) | Offline-first sync contract | Accepted |
| [0006](0006-device-enrolment-and-pin-auth.md) | Device enrolment + PIN authentication | Accepted |
| [0007](0007-outbox-and-in-process-jobs.md) | Outbox and in-process background jobs | Accepted |
| [0008](0008-migrations-in-pipeline.md) | Migrations run in the pipeline, expand/contract | Accepted |
| [0009](0009-observability-without-pii.md) | OpenTelemetry and no PII in telemetry | Accepted |
| [0010](0010-azure-hosting.md) | Azure App Service + PostgreSQL Flexible Server | Accepted |

Template: copy [`0000-template.md`](0000-template.md).

# 0009. OpenTelemetry and no PII in telemetry

- Status: Accepted
- Date: 2026-09-24

## Context

The data is about children, including health data (GDPR Art. 9). Telemetry is shipped to third-party backends and kept for weeks.

## Decision

- **OpenTelemetry** for traces, metrics and logs. Export goes to Azure Monitor when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, or to any OTLP endpoint (`OTEL_EXPORTER_OTLP_ENDPOINT`), such as the Aspire dashboard locally.
- Instrumentation: ASP.NET Core (health probes excluded), HttpClient, Npgsql, runtime, plus the `Pegadas` ActivitySource and Meter for domain metrics (`sync.batch.size`, `outbox.pending`, …).
- **No PII in logs, traces or metrics.** Log only ids. Parameters that could carry personal data are annotated with `[PersonalData]`/`[SensitiveData]` (the `PegadasTaxonomy` classifications) and erased by the redactor. Request bodies are never logged.
- ProblemDetails responses carry the `traceId` for support correlation.

## Consequences

- Debugging uses ids and the audit log, not names or notes in logs.
- New log statements need a redaction review in PRs.

# 0010. Azure App Service + PostgreSQL Flexible Server

- Status: Accepted
- Date: 2026-09-24

## Context

The target is one always-on container (API + jobs), managed PostgreSQL in the EU, low cost (~45–75 €/month for prod + staging), and minimal operations.

## Decision

- **Azure App Service Linux (B2)** runs the container with Always On. Staging runs on the same plan.
- **Azure Database for PostgreSQL Flexible Server (B1ms)** with PITR 14 days, reached over private access through App Service VNet integration.
- Key Vault (secrets, JWT signing key) accessed with managed identity. Application Insights + Log Analytics. Azure DNS. A storage account with an immutable container for logical backups.
- EU region (Spain Central or West Europe). **Bicep** for all resources. GitHub Actions deploys with OIDC, with no long-lived secrets.
- Image: `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra`. It runs as non-root, is small, and includes ICU and tzdata for school time zones.

## Consequences

- The decision is reversible: standard container, standard PostgreSQL, OpenTelemetry and IaC mean moving clouds takes days, not months.
- The Basic tier has no deployment slots, so a deploy restarts the app for a few seconds. The offline-first client tolerates that.

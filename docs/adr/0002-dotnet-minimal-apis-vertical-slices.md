# 0002. .NET 10, Minimal APIs and vertical slices

- Status: Accepted
- Date: 2026-09-24

## Context

The architecture doc picks ASP.NET Core on .NET 10 LTS (supported until Nov 2028). Performance matters most in the sync endpoint and the "today" snapshot. The team is small, so layers that add no value are a cost.

## Decision

- **Minimal APIs** with route groups (`/v1`) and `TypedResults`. No MVC controllers.
- **Vertical slices**: one folder per use case (`Features/<UseCase>/`) holding the endpoint, handler and request/response types. No generic repository or service layers.
- **No mediator library.** Endpoints call handler classes injected by DI. MediatR is now commercially licensed, and a mediator adds indirection with no benefit here.
- Built-in .NET 10 validation (`AddValidation()` + DataAnnotations), built-in OpenAPI 3.1 document generation, Scalar UI outside production.
- `System.Text.Json` source generation for request/response types on hot paths.
- Endpoints are mapped explicitly by each module (no assembly scanning), which keeps startup fast and trimming-friendly.

## Consequences

- Less magic and fewer packages. Each slice can be read top to bottom.
- Cross-cutting behaviour goes into endpoint filters and middleware, not mediator pipelines.

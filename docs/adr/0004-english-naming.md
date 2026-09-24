# 0004. English names in code, database and API

- Status: Accepted
- Date: 2026-09-24

## Context

The product documents are written in Portuguese (`Registo`, `Sala`, `/v1/salas/{id}/hoje`). .NET libraries, tooling and most contributors work in English, and mixing both languages in identifiers is error-prone.

## Decision

C# identifiers, database schemas/tables/columns and API routes/JSON are in **English**. [`docs/glossary.md`](../glossary.md) maps every Portuguese domain term to its English name. `Registo` becomes `DiaryEntry` to avoid clashing with the C# `record` keyword.

User-facing text (app UI, emails) remains Portuguese and is the mobile app's responsibility. API error `detail` strings are for developers; the app switches on the stable error `code`.

## Consequences

- Talking about the domain needs the glossary. New terms are added there first.

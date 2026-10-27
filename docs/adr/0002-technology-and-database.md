# ADR-0002: .NET 10 LTS, ASP.NET Core, EF Core and SQLite

- **Status:** Accepted
- **Decided by:** Developer (execution contract: ASP.NET Core Web API, supported .NET LTS, EF Core, SQLite, xUnit).
  Versions verified by the agent against live sources on 26 October 2026.

## Context

The role is a .NET position; the brief requires a RESTful API and a persistent database; reviewers must be able to run
the project with minimal set-up.

## Decision

- **.NET 10 (LTS)**, SDK pinned in `global.json` to 10.0.100 or any later 10.0 feature band (developed with 10.0.403).
  The official release index showed .NET 8 and .NET 9 both reach end of support on 10 November 2026, two weeks after this
  work, while .NET 10 is supported until November 2028.
- **ASP.NET Core Web API with controllers**, built-in OpenAPI document generation and Swagger UI for browsing.
- **EF Core 10** with migrations; **SQLite** as the database for this submission.
- Package versions are managed centrally (`Directory.Packages.props`) and were taken from NuGet at the time of writing,
  not from memory. Warnings, including vulnerable-package warnings, fail the build.
- Tests use **xUnit v3 (4.0.2) on Microsoft Testing Platform**; xUnit 4 requires `dotnet test` to run in MTP mode on the
  .NET 10 SDK, configured in `global.json`.

## Alternatives considered

- **PostgreSQL or SQL Server in Docker.** Closer to production, but reviewers would need Docker (not available on the
  development machine either). The schema and the double-booking guard are portable, so this is a configuration change later.
- **EF Core in-memory provider.** No transactions or constraints, so it cannot prove double-booking prevention.
- **Dapper.** Full SQL control, but hand-written mapping and migrations for no benefit at this size.
- **Minimal APIs.** Equally capable; controllers were chosen for familiarity with ASP.NET MVC/Web API conventions and
  automatic model validation.

## Consequences

- One `dotnet run` starts everything; the database file is created, migrated and seeded automatically in development.
- SQLite allows one writer at a time — fine for this scope, and the first thing to change for production (ADR-0003).
- SQLite lacks a timestamp type, so instants are stored as Unix milliseconds (ADR-0004).

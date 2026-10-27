# ADR-0001: Layered architecture with four projects

- **Status:** Accepted
- **Decided by:** Developer (execution contract recommended this structure "as a starting point")

## Context

The service has real business rules (allocation, time, idempotency) that must be testable without HTTP or a database,
and a persistence concern (double-booking prevention) that must be tested with one. Reviewers need to find things
quickly, and the code should be able to grow (new provider, new endpoints, events) without rewrites.

## Decision

Four projects with dependencies pointing inwards: **Domain** (no dependencies), **Application** (use cases, ports),
**Infrastructure** (EF Core, SQLite), **Api** (HTTP, composition root). Controllers stay thin; expected failures are
returned as `Result<T>`/`Error` values, and exceptions are reserved for bugs. Command and query handlers are plain
classes (lightweight CQRS) rather than a mediator.

The application layer uses EF Core's `DbSet<T>` through `ISchedulerDbContext` instead of repositories; the only
provider-specific behaviour, recognising a unique violation, is behind `IConstraintViolationDetector`.

## Alternatives considered

- **Single project with folders.** Less ceremony, but nothing stops a controller from querying the database or the domain
  from referencing ASP.NET; the boundaries would rely on discipline.
- **Repository per aggregate.** Hides EF Core completely, but adds a layer of pass-through code and makes efficient,
  query-specific projections harder. Not justified for one storage technology.
- **MediatR pipeline.** Useful for cross-cutting behaviours at scale; here it would add indirection, and v13+ requires a
  commercial licence.
- **Microservices.** No team-size or scaling reason in scope; documented as a future evolution instead.

## Consequences

- Dependency direction is enforced by project references and `ArchitectureTests`.
- Domain rules are unit tested in milliseconds; use cases are tested through the real API.
- The application layer is tied to EF Core abstractions — accepted, and limited to the persistence port.

---
description: "Use when writing or reviewing C# in src/: domain invariants, application handlers, EF Core queries and mappings, controllers, error codes, time handling, logging and telemetry for the Service Scheduler."
applyTo: "src/**/*.cs"
---
# C# and .NET rules for `src/`

- Layering: Domain references nothing; Application references Domain and EF Core abstractions only; Infrastructure
  implements `ISchedulerDbContext` and `IConstraintViolationDetector`; Api is the composition root.
- Expected outcomes return `Result<T>` with an `Error` from `SchedulingErrors` (or `AppointmentErrors` in the domain).
  Throw only for bugs, such as an invalid `AppointmentAllocation` reaching `Appointment.Confirm`.
- Error kinds map to exactly one status: Validation 400, NotFound 404 (resource in the URL), BusinessRule 422 (including
  unknown ids in a body), Conflict 409. Do not add ad-hoc `StatusCode(...)` results in controllers.
- Time: inject `TimeProvider`; keep instants UTC (`DateTimeOffset` with zero offset); compare on the 15-minute grid via
  `Slots` and `TimeRange`; local-time logic belongs in `Dealership`.
- Queries: `AsNoTracking`, filter in SQL, project to DTOs, load collections in a fixed number of queries. Use `List<T>`
  (not arrays) for `Contains` in LINQ to keep translation predictable.
- Writes: build aggregates through their factory methods; one `SaveChangesAsync` per unit of work; catch
  `DbUpdateException` only through `IConstraintViolationDetector`.
- Logging: `[LoggerMessage]` static partial methods with explicit `EventId`; structured fields are ids and codes only.
- Metrics and spans: add instruments to `SchedulerTelemetry`; tags must be low-cardinality and contain no personal data.
- Every public API change needs XML doc comments (they feed the OpenAPI document) and `ProducesResponseType` attributes
  with `application/problem+json` for error statuses.

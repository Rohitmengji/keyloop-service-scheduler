# ADR-0006: Test against the real database, with a fake clock

- **Status:** Accepted
- **Decided by:** Developer (execution contract: real SQLite integration tests where database behaviour matters; no
  concurrency claims based on in-memory mocks; deterministic, isolated tests).

## Context

The most important guarantees (no double booking, atomic writes, idempotent retries) are enforced by the database.
Date rules depend on "now". Tests must stay green when run months later and must not interfere with each other when run
in parallel.

## Decision

- **Unit tests** for pure domain logic (grid, ranges, opening hours, occupancy, aggregate invariants, fingerprints, layering).
- **Integration tests** through `WebApplicationFactory<Program>`: the real middleware, controllers, handlers, EF Core and a
  **fresh SQLite file per test**, created by the real migration and seeded with the same demo data used in development.
- **`FakeTimeProvider`** fixes "now" at Sunday 1 November 2026 12:00 UTC; tests that need time to pass advance it.
- **Assertions on the wire contract** (JSON fields, status codes, headers, problem `code`) rather than internal types.
- **Concurrency tests** send simultaneous HTTP requests; one test bypasses the application layer to prove the constraint
  itself. Their sensitivity was checked by disabling conflict handling and watching them fail.
- xUnit's own assertions; no mocking library (no persistence mocks are needed) and no FluentAssertions (commercial licence from v8).

## Alternatives considered

- **Mocked repositories.** Fast, but would pass with the double-booking guard removed.
- **EF Core in-memory provider.** No transactions or constraints.
- **One shared database per test class.** Faster, but tests would see each other's bookings.
- **Testcontainers with PostgreSQL.** Best fidelity for production, but requires Docker; a natural addition with ADR-0002's
  production database.

## Consequences

- 92 tests run in about six seconds locally, in parallel, with no external dependencies.
- Test data is realistic and shared with the demo, so the walkthrough and the tests tell the same story.

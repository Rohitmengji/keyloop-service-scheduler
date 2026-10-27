---
description: "Use when writing or reviewing tests: xUnit v3 on Microsoft Testing Platform, WebApplicationFactory integration tests against SQLite, FakeTimeProvider, concurrency and idempotency races, problem-details assertions, DemoData ids."
applyTo: "tests/**/*.cs"
---
# Test rules for `tests/`

- Unit tests (`ServiceScheduler.UnitTests`) cover pure domain and application logic; use `Fixtures` for valid objects and
  `with` expressions to vary one thing per test.
- Integration tests derive from `ApiTestBase`: each test gets its own `SchedulerApiFactory`, SQLite file and fixed clock
  (Sunday 2026-11-01 12:00 UTC). Use `Monday(hour, minute)` for London times and `DemoData` ids for seeded records.
- Assert on the wire contract: status code, JSON fields via `Text("a.b")`, headers, and `AssertProblemAsync(response,
  status, code)` for errors. Do not deserialise into production DTOs.
- Database guarantees must be proven against SQLite, never with mocks. When bypassing the API, use a fresh scope or
  `ChangeTracker.Clear()` so EF's identity map does not hide the database's behaviour.
- Races: start all requests before awaiting (`Task.WhenAll`), assert exact counts, then assert the database state.
  If a race test passes, check it can fail (temporarily break the guard) before trusting it.
- Pass `TestContext.Current.CancellationToken` (the `Ct` property) to every call that accepts a token; xUnit's analyzer enforces it.
- Name tests as behaviour sentences (`A_vehicle_cannot_be_booked_into_two_overlapping_appointments...`).
- Never skip, weaken or delete a failing test to get a green run; fix the cause or report it.

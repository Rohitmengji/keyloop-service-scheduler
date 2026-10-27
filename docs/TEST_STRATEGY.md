# Test strategy

## Principles

1. **Test behaviour where it lives.** Rules about time and slots are pure functions, so they are unit tested. Double
   booking, transactions and idempotency live in the database, so they are tested against a real database.
2. **No mocks for persistence.** A mocked repository would pass the concurrency tests even with the double-booking guard
   removed. The only test doubles are a fake clock (`FakeTimeProvider`) and, in one test, a deliberately failing service
   to exercise the 500 path.
3. **Deterministic.** Every integration test gets its own API instance and SQLite file, migrated and seeded identically,
   with the clock fixed at Sunday 1 November 2026 12:00 UTC. Nothing depends on the real date, so tests will not start
   failing next month.
4. **Black-box contract.** Integration tests send JSON over HTTP and assert on the JSON, status codes, headers and
   problem `code`s that clients rely on, not on internal types.

## Suites

| Suite | Count | What it covers |
|---|---|---|
| `ServiceScheduler.UnitTests` | 37 | 15-minute grid, time ranges, overlap edges (back-to-back vs one-slot overlap), opening hours with daylight saving and a +05:30 zone, candidate starts on clock-change days, `Appointment` invariants and cancellation, request fingerprints, layer dependencies |
| `ServiceScheduler.IntegrationTests` | 55 | Booking success and persistence (including across an application restart), every validation and business-rule error code, bay/technician/vehicle conflicts, back-to-back bookings, simultaneous requests, simultaneous idempotent retries, transaction rollback, cancellation (including simultaneous), availability, catalogue, health, OpenAPI document, unknown routes, the 413 and generic 500 paths, business metrics |
| `scripts/smoke-test.sh` | 21 checks | The running API end to end with cURL, in CI after the build |

The scenario-to-test mapping required by the assessment is in [REQUIREMENTS.md](REQUIREMENTS.md#assessment-test-checklist).

## Concurrency tests and how they were validated

`ConcurrencyTests` fire simultaneous HTTP requests (8 cars for 2 bays; 6 cars for the single EV technician; 6 retries
with one idempotency key) at a SQLite file and assert the exact number of bookings and that no bay or technician appears
twice. A separate test bypasses the application layer, inserts two clashing appointments directly and checks that the
database rejects the second **and** that none of its rows (not even its free bay's slots) were written.

A passing race test can be vacuous if the requests never actually race. To rule that out, the unique-violation detector
was temporarily changed to always return `false` (so lost races surface as 500s instead of being retried). All four
concurrency tests failed in three out of three runs, then passed again once the change was reverted. They were then run
ten times in a row without a failure.

## Running

```bash
dotnet test                                    # everything (Microsoft Testing Platform mode via global.json)
dotnet test --project tests/ServiceScheduler.UnitTests
dotnet test --project tests/ServiceScheduler.IntegrationTests -- --filter-class ServiceScheduler.IntegrationTests.ConcurrencyTests
dotnet test -- --coverage --coverage-output-format cobertura   # line coverage
./scripts/smoke-test.sh                        # against a running API
```

## Coverage

Line coverage of hand-written production code (excluding generated migrations and compiler-generated sources) from the
Release test run: **97.5%** overall — Domain 96.2%, Application 99.5%, Infrastructure 100%, Api 91.9%. Coverage is a
by-product, not a target; the uncovered lines are defensive guards (for example the invariant checks for impossible
allocations) and the client-disconnect branch of the exception handler.

## Not covered by automated tests

- Behaviour on PostgreSQL or SQL Server (the design is portable, but only SQLite is exercised).
- Sustained load and latency percentiles (no load test was run; see [TRADEOFFS.md](TRADEOFFS.md)).
- OTLP export to a real collector (configuration is standard; the exporter is only enabled when an endpoint is set).
- Client disconnect mid-request (the 499 branch).

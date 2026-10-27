# Keyloop Unified Service Scheduler

[![CI](https://github.com/Rohitmengji/keyloop-service-scheduler/actions/workflows/ci.yml/badge.svg)](https://github.com/Rohitmengji/keyloop-service-scheduler/actions/workflows/ci.yml)

A backend service that replaces manual workshop booking for car dealerships. A client asks for a **vehicle**, a
**service type**, a **dealership** and a **start time**; the service confirms the appointment only if a **service bay and a
technician qualified for that service are both free for the entire duration**, then stores an appointment that links the
customer, vehicle, technician and bay. Simultaneous requests can never double-book a bay, a technician or a vehicle: the
database itself guarantees it.

Keyloop technical assessment, **Scenario A (Ownership)**, backend layer implemented, client layer stubbed with Swagger UI,
an executable `.http` collection and a cURL smoke test.

| | |
|---|---|
| Stack | .NET 10 (LTS), ASP.NET Core Web API, EF Core 10, SQLite, OpenTelemetry, xUnit v3 |
| System design | [docs/SYSTEM_DESIGN.md](docs/SYSTEM_DESIGN.md) |
| Tests | 89 automated tests (37 unit, 52 integration against a real database) + 21-check live smoke test |

## Quick start

Prerequisite: **.NET SDK 10.0.100 or later 10.0.x** (`dotnet --list-sdks`). Nothing else: no database server, Docker or secrets.

```bash
git clone https://github.com/Rohitmengji/keyloop-service-scheduler.git
cd keyloop-service-scheduler
dotnet run --project src/ServiceScheduler.Api
```

- Swagger UI: <http://localhost:5080/swagger> (the database is created, migrated and seeded with demo data automatically)
- Guided walkthrough: [docs/DEMO_GUIDE.md](docs/DEMO_GUIDE.md), or run [samples/scheduler.http](samples/scheduler.http) in VS Code (REST Client), Visual Studio or Rider

```bash
dotnet test                    # all 89 tests
./scripts/smoke-test.sh        # 21 end-to-end checks against the running API (needs curl and jq)
```

## API at a glance

| Method and path | Purpose |
|---|---|
| `GET /api/v1/service-types` | Services and their durations |
| `GET /api/v1/dealerships` | Dealerships, active bays, technicians and their qualifications |
| `GET /api/v1/dealerships/{id}/availability?serviceTypeId=&date=` | Start times when a bay and a qualified technician are free |
| `POST /api/v1/appointments` | Book (`201`); optional `Idempotency-Key` makes retries safe |
| `GET /api/v1/appointments/{id}` | Read an appointment |
| `POST /api/v1/appointments/{id}/cancel` | Cancel and release the slots |

```bash
curl -s -X POST http://localhost:5080/api/v1/appointments \
  -H 'Content-Type: application/json' -H "Idempotency-Key: $(uuidgen)" \
  -d '{"dealershipId":"de000000-0000-0000-0000-000000000001","customerId":"c0000000-0000-0000-0000-000000000001",
       "vehicleId":"ca000000-0000-0000-0000-000000000001","serviceTypeId":"5e000000-0000-0000-0000-000000000001",
       "startTime":"<a start from the availability endpoint, e.g. 2026-11-02T09:00:00+00:00>"}'
```

Errors are RFC 9457 problem documents with a stable `code`: 400 malformed request, 404 unknown resource in the URL,
409 the time is taken, 422 a business rule is broken. Full contract: [docs/API_CONTRACT.md](docs/API_CONTRACT.md).

## How double booking is prevented

Appointments sit on a 15-minute grid. Confirming one inserts a reservation row for **every 15-minute slot** it covers, for
**its bay, its technician and its vehicle**, in the same transaction as the appointment. The reservation table's primary
key is `(ResourceKind, ResourceId, SlotStartUtc)`, so two overlapping appointments can never both be stored for the same
resource, whichever API instance handles them. The service checks availability first to give precise errors, and if it
loses a race to a simultaneous request it re-reads and tries the next free bay or technician.
Details: [docs/adr/0003-double-booking-prevention.md](docs/adr/0003-double-booking-prevention.md).

## Project structure

```text
src/ServiceScheduler.Domain          Appointment aggregate, slot grid, opening hours (no dependencies)
src/ServiceScheduler.Application     Use cases: book, cancel, availability, queries; error codes; telemetry
src/ServiceScheduler.Infrastructure  EF Core mappings, constraints, migrations, SQLite, demo data
src/ServiceScheduler.Api             Controllers, problem details, OpenAPI, health checks, OpenTelemetry
tests/                               Unit and integration tests
samples/, scripts/                   .http walkthrough, smoke test
docs/                                Design, decisions, contract, operations
```

More in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Tests and quality gates

- **Unit tests (37):** slot grid, overlap edges, opening hours across daylight-saving changes and a +05:30 zone, the
  appointment's invariants and cancellation rules, request fingerprints, layer dependencies.
- **Integration tests (52):** the real HTTP pipeline against a fresh SQLite file per test, with a fixed fake clock: every
  error code, bay/technician/vehicle conflicts, back-to-back bookings, simultaneous bookings and retries, transaction
  rollback, cancellation, availability, health, OpenAPI document, the generic 500 path, business metrics.
- The concurrency tests were checked by deliberately disabling conflict handling: they failed every time.
- Line coverage of hand-written code: 97.6%.
- **CI** (GitHub Actions): formatting, Release build with warnings and vulnerable packages as errors, EF migration drift
  check, tests with TRX and coverage reports, and a job that starts the API and runs the smoke test.

Strategy and scenario mapping: [docs/TEST_STRATEGY.md](docs/TEST_STRATEGY.md), [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md).

## Documentation

| Document | Contents |
|---|---|
| [SYSTEM_DESIGN.md](docs/SYSTEM_DESIGN.md) | **System design document**: architecture diagram, components, data flow, technologies, observability, GenAI in design |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Code structure, dependency rule, request pipeline |
| [DATA_MODEL.md](docs/DATA_MODEL.md) | ER diagram, constraints, lifecycle, demo data |
| [API_CONTRACT.md](docs/API_CONTRACT.md) | Endpoints, examples, error codes |
| [DECISIONS.md](docs/DECISIONS.md) | Index of the six ADRs |
| [REQUIREMENTS.md](docs/REQUIREMENTS.md) | Requirement → code → test traceability |
| [ASSUMPTIONS.md](docs/ASSUMPTIONS.md) / [TRADEOFFS.md](docs/TRADEOFFS.md) | What was assumed, what was traded off, what comes next |
| [SECURITY.md](docs/SECURITY.md) / [OBSERVABILITY.md](docs/OBSERVABILITY.md) | Controls and gaps; logs, traces, metrics, New Relic |
| [TEST_STRATEGY.md](docs/TEST_STRATEGY.md) / [RUNBOOK.md](docs/RUNBOOK.md) | Testing approach; configuration, migrations, troubleshooting |
| [DEMO_GUIDE.md](docs/DEMO_GUIDE.md) / [IMPLEMENTATION_STATUS.md](docs/IMPLEMENTATION_STATUS.md) | Reviewer walkthrough; verified status |
| [AI_COLLABORATION.md](docs/AI_COLLABORATION.md) | Detailed AI collaboration record and verification log |

## AI Collaboration Narrative

**Strategy.** I used GitHub Copilot in agent mode as the implementer and kept the decisions and the quality bar with me.
Before any code was written I gave the agent a written execution contract: the scenario and layer, the stack, the
documents and test scenarios required, coding and security standards, a phased plan, and when to stop and ask. I also set
ground rules for delivery: commits only under my personal Git identity, no AI tool listed as an author, and nothing pushed
without my confirmation.

**Verification.** I treated generated code as untrusted until something other than the AI confirmed it. Versions were
checked against the .NET release index and NuGet rather than taken from the model's memory, which moved the project to
.NET 10 (both .NET 8 and 9 leave support on 10 November 2026) and surfaced that xUnit 4 needs Microsoft Testing Platform
mode. Every milestone had to end with real output: a warnings-as-errors build, the test suite, and a smoke test against the
running API. Reading actual responses, not only status codes, exposed three contract defects, and the integration tests
caught a fourth that the smoke test had missed: every business error was going out as `application/json` instead of
`application/problem+json`. Because a race test that passes proves little, the conflict handling was deliberately disabled
to confirm the concurrency tests fail without it, then they were run repeatedly to rule out flakiness. Finally, every CI
step was run locally with the same commands before pushing.

**Ownership and quality.** The double-booking guarantee lives in the database, where a test bypasses the application
entirely to prove it, so correctness does not depend on the generated code being perfect. Quality gates (formatting,
analyzers, vulnerable-package audit, migration drift, tests, live smoke test) run on every push. The decisions I made and
the ones the agent proposed are recorded separately, with their review status, in [docs/DECISIONS.md](docs/DECISIONS.md);
the full log of what was checked and what was fixed is in [docs/AI_COLLABORATION.md](docs/AI_COLLABORATION.md).

## Limitations

No authentication or authorisation (documented as a gateway and identity concern), SQLite's single writer, first-fit
allocation, no load test. See [docs/TRADEOFFS.md](docs/TRADEOFFS.md) and [docs/SECURITY.md](docs/SECURITY.md).

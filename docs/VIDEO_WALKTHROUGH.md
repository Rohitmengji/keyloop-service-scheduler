# Video walkthrough plan (7-9 minutes)

A presentation plan for recording the submission video. It uses only features that exist in this repository. Plain-
language explanations of .NET terms are in the glossary at the end, so you can explain each part confidently.

## Set-up before recording

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"   # only if .NET was installed user-locally
rm -f src/ServiceScheduler.Api/scheduler.db*        # fresh demo data
dotnet run --project src/ServiceScheduler.Api        # leave running
```

Open: the GitHub repository (README), `docs/SYSTEM_DESIGN.md` on GitHub, Swagger UI at <http://localhost:5080/swagger>,
the latest green GitHub Actions run, and in your editor `Appointment.cs`, `BookAppointmentHandler.cs`,
`AppointmentConfigurations.cs` and `ConcurrencyTests.cs`. Increase font sizes; silence notifications.

---

## 0:00-0:45 — Introduction and scenario

**Say:** who you are and your current role (keep any experience claims accurate); that you chose **Scenario A, the
Unified Service Scheduler**, and implemented the **backend** as a REST API with a persistent database; what the video covers.

**Show:** README top section.

**Evidence:** README states the three requirements and the one-command start.

## 0:45-2:00 — System design and key decisions

**Say:**
- The problem: manual booking leads to double-booked bays and technicians and unqualified assignments.
- Architecture: clients (stubbed by Swagger, a `.http` file and a cURL script) call a stateless ASP.NET Core API with four
  layers — API (HTTP only), Application (use cases), Domain (rules), Infrastructure (database). Dependencies point inwards.
- Decisions: .NET 10 because it is the current long-term-support release; SQLite so reviewers need nothing installed,
  with a clear path to PostgreSQL; times stored in UTC and always sent with an offset; one HTTP status per kind of error.

**Show:** SYSTEM_DESIGN section 4 (architecture diagram), then section 6 (sequence diagram).

**Evidence:** the diagrams match the code layout in `src/`.

## 2:00-4:30 — Live API demonstration (Swagger UI)

| Step | Action | What to say |
|---|---|---|
| 1 | `GET /api/v1/dealerships` → Try it out → Execute | Two dealerships. Manchester has two active bays; Chloe is the only EV-qualified technician; Pune has nobody for MOT. |
| 2 | `GET /api/v1/dealerships/{dealershipId}/availability` with `de000000-0000-0000-0000-000000000001` and `5e000000-0000-0000-0000-000000000001` | Free start times for tomorrow, in local time, with free bay and technician counts. Copy the first `start`. |
| 3 | `POST /api/v1/appointments`: paste the start into `startTime`, set `Idempotency-Key` to `demo-1`, Execute | 201 Created: Bay 1 and a qualified technician allocated; times in the dealership's offset. |
| 4 | Execute again unchanged | Same appointment id and `Idempotent-Replayed: true` — a retry cannot double-book. |
| 5 | Body with Emma (`c0000000-...-002`) and her Golf (`ca000000-...-003`), same start, clear the key | 201 in Bay 2. |
| 6 | Body with Arjun (`c0000000-...-003`) and his Creta (`ca000000-...-004`), same start | 409 `service_bay_unavailable` — the error explains exactly why. |
| 7 | `GET /api/v1/appointments/{id}` with the first id | The stored record with customer, vehicle, bay and technician. |
| 8 | (Optional) remove `+00:00` from a start time | 400 — the API never guesses a time zone. |

**Evidence:** status codes and `code` values on screen; they match `docs/API_CONTRACT.md`.

## 4:30-6:00 — Booking correctness, database and testing

**Say:**
- Each booking writes one reservation row per 15-minute slot for its bay, its technician and the vehicle, in the same
  transaction as the appointment.
- The reservation table's primary key is (resource kind, resource id, slot start), so the database refuses to store two
  claims on the same resource and slot, even when two servers handle simultaneous requests. A request that loses that
  race re-reads availability and tries the next free resource.
- 92 automated tests: 37 unit tests for rules such as daylight saving, and 55 integration tests that run the real API
  against a fresh SQLite file each time, with a fixed clock. They include simultaneous bookings, simultaneous retries,
  rollback, and records surviving an application restart.

**Show:** `Appointment.cs` (`Confirm`), `AppointmentConfigurations.cs` (`HasKey` on `SlotReservation`),
`ConcurrencyTests.cs` (first test), then run `dotnet test` in a second terminal.

**Evidence:** `total: 92, failed: 0` in the terminal.

## 6:00-7:30 — AI collaboration and verification

**Say:**
- You used GitHub Copilot in agent mode as the implementer, directed by a written execution contract: scenario, stack,
  required documents and tests, standards, and when to stop and ask. You kept decisions and publishing approval.
- AI output was treated as untrusted until independently checked: versions from live sources (which is how .NET 10 and
  xUnit's new test runner mode were chosen), warnings-as-errors builds, tests, a smoke test against the running API, and
  CI.
- Real defects found that way: error responses sent with the wrong content type; a timestamp precision mismatch;
  misleading validation errors; a database test that never reached the database; and, in the final audit, oversized
  requests returning 500 instead of 413, which only a live server check revealed.
- Race tests were checked by deliberately disabling conflict handling: they failed, proving they test the real guard.

**Show:** `docs/AI_COLLABORATION.md` ("Issues found and fixed" and the decision provenance table), then the green
GitHub Actions run.

**Evidence:** the verification log and CI run page.

## 7:30-8:30 — Trade-offs, lessons and conclusion

**Say:**
- Trade-offs: fixed 15-minute grid (simple, database-enforced, coarser), SQLite (zero set-up, one writer at a time),
  first-fit allocation, no authentication in this scope.
- Lessons: with AI writing most code, the engineer's job is defining "correct" and building proof; time zones were the
  trickiest area (availability scans UTC instants to stay correct on clock-change days).
- Next: identity and dealership-level permissions, PostgreSQL, technician shifts, notifications.
- Close: the README runs everything with one command; the design document is in `docs/`.

**Show:** README "Limitations", or camera only.

---

## Glossary (for explaining in your own words)

| Term | Plain explanation |
|---|---|
| ASP.NET Core Web API, controller | Microsoft's web framework. A controller is a class whose methods answer HTTP requests (`AppointmentsController.Book` handles `POST /appointments`). |
| Dependency injection | The framework creates objects and hands them their dependencies, so a handler receives a database context without creating one. |
| EF Core, DbContext, migration | The ORM: C# classes map to tables; `SchedulerDbContext` is the session with the database; a migration is a versioned script that creates or changes tables. |
| Aggregate (DDD) | A cluster of objects changed together through one root. `Appointment` owns its slot reservations and refuses invalid allocations. |
| `Result<T>` / problem details | Handlers return either a value or an error code instead of throwing; the API turns errors into a standard JSON error format (RFC 9457). |
| Idempotency key | A client-chosen id for a request; sending it again returns the first result instead of repeating the action. |
| Primary key / unique constraint | A database rule that no two rows can share these values; here it makes double booking physically impossible. |
| `TimeProvider` / fake clock | An injectable "now", so tests can fix the date and stay deterministic. |
| `WebApplicationFactory` | Runs the real API in memory for tests, so they exercise real HTTP handling and the real database. |
| OpenTelemetry | A vendor-neutral standard for logs, traces and metrics; can send data to New Relic and similar tools. |

# AGENTS.md — engineering contract

Rules for anyone changing this repository, human or AI agent. Scoped details live in `.github/instructions/`;
specialised agents in `.github/agents/`. If a rule here conflicts with another file, this file wins.

## What this service is

A booking API for dealership workshops (Keyloop assessment, Scenario A). Its core promise: **a service bay, a technician
or a vehicle is never booked twice for overlapping time, and every appointment has a technician qualified for the
service.** Read [docs/SYSTEM_DESIGN.md](docs/SYSTEM_DESIGN.md) before making design changes.

## Commands

```bash
dotnet build ServiceScheduler.slnx                       # warnings are errors
dotnet test                                              # all tests (MTP mode, set in global.json)
dotnet format ServiceScheduler.slnx --verify-no-changes  # formatting gate used by CI
dotnet run --project src/ServiceScheduler.Api            # http://localhost:5080/swagger
./scripts/smoke-test.sh                                  # against the running API
dotnet tool restore                                      # once, for dotnet-ef
dotnet ef migrations add <Name> --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api
```

## Non-negotiable invariants

1. `PK_SlotReservations (ResourceKind, ResourceId, SlotStartUtc)` is the double-booking guard. Never remove it, widen it,
   or replace it with an application-level check.
2. An appointment and its reservations are written in one `SaveChanges` call. Never split them.
3. Every booked slot reserves the bay, the technician **and** the vehicle.
4. Instants are UTC `DateTimeOffset`; API input must carry an explicit offset; opening hours are local to the dealership's
   IANA zone. Use the injected `TimeProvider`, never `DateTime.Now`/`UtcNow`.
5. Error `code` values are public contract. Add new ones; do not rename or repurpose existing ones.

## Architecture rules

- Dependencies point inwards: Api → Application → Domain; Infrastructure → Application. The domain references nothing.
- Business rules go in the domain (invariants) or application handlers (ordering, queries); never in controllers or EF configurations.
- Controllers translate HTTP to one handler call and map `Result<T>` with `ProblemFor`. No queries in controllers.
- Expected failures are `Error` values from `SchedulingErrors`; exceptions are for bugs only.
- Lightweight CQRS with plain handler classes. Do not add MediatR, AutoMapper, FluentAssertions or other dependencies
  without an ADR.

## Coding standards

- C# latest, nullable enabled, file-scoped namespaces, `sealed` by default, primary constructors for services.
- Pass `CancellationToken` through every async call. Use `AsNoTracking` for reads and project to DTOs; no N+1 queries.
- Log with `[LoggerMessage]` methods and event ids; log ids and codes only — never names, emails, VINs, registrations or notes.
- Keep comments for what the code cannot say; one short line.

## Tests

- Every new rule gets a test; every new error code gets an integration test asserting status and `code`.
- Persistence, concurrency and transaction behaviour is tested against SQLite through `SchedulerApiFactory`, never mocks.
- Use `FakeTimeProvider` (fixed at 2026-11-01 12:00 UTC) and the seeded `DemoData` ids; never depend on the real date.
- Do not weaken or delete a failing test to make a change pass; fix the cause or report it.

## Persistence

- Any model change needs a migration in the same change; CI fails on drift. Never edit a migration that has been pushed.
- New invariants that the database can enforce get a constraint (unique, check, foreign key) as well as a domain check.

## Documentation

- Keep docs true to the code. If behaviour changes, update `API_CONTRACT.md`, `SYSTEM_DESIGN.md` and the relevant ADR.
- Significant decisions need an ADR in `docs/adr/` and a row in `docs/DECISIONS.md`, including who decided.
- `docs/AI_COLLABORATION.md` records only events that happened. Never invent prompts, reviews, test results or CI runs.

## Security

- No secrets, tokens or real personal data in the repository, logs or tests (demo data uses `example.com`).
- Validate at the boundary and re-check invariants in the domain. Error responses never include exception details.

## Git

- Commit as `rohit mengji <rohitmengjih@gmail.com>` (repository-local config). Never add AI tools as author or
  `Co-authored-by`.
- Do not push, create repositories, or change remote settings without the developer's explicit confirmation.

## Definition of done

- [ ] `dotnet build` with no warnings; `dotnet format --verify-no-changes` clean
- [ ] `dotnet test` all green; new behaviour covered
- [ ] Migration added and drift check clean if the model changed
- [ ] Smoke test passes against a running instance if the API changed
- [ ] Documentation and ADRs updated; claims match observed results

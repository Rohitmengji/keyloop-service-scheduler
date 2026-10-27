# AI collaboration record

How GenAI was used to design, build and verify this service, kept factual. Summary in the
[README](../README.md#ai-collaboration-narrative).

## Tools and roles

| | Role |
|---|---|
| **Developer (Rohit Mengji)** | Chose the scenario and the layer, set the technology stack, provided the execution contract the agent worked to, set the Git identity rules, and owns every decision and the final code. |
| **GitHub Copilot, agent mode in VS Code (Claude Opus 5.5 model)** | Researched current versions, proposed the design, wrote the code, tests, scripts, CI and documentation, and ran every verification step, inside the limits of the contract. |

All work in this repository was produced in one continuous Copilot agent session on 26-27 October 2026.

## How the agent was directed

1. **Inputs.** The Keyloop brief, the job description, and a written **execution contract** from the developer: scenario A,
   backend only, ASP.NET Core with EF Core and SQLite, xUnit, a list of required documents and tests, quality standards,
   a phased plan, and explicit stop conditions.
2. **Ground rules set by the developer in conversation:** commit only with the personal identity
   (`rohitmengjih@gmail.com`), never add an AI tool as author or co-author, and ask before creating or pushing a public repository.
3. **Evidence over assertion.** The contract required real command output at each milestone and banned claims such as
   "tests pass" or "CI is green" without a run. This record only lists results that were actually observed.
4. **Phased execution:** audit, design, foundation, domain and persistence, application and API, tests, security and
   observability, CI and documentation, verification, delivery.

## Decision provenance

| Decision | Origin | Status |
|---|---|---|
| Scenario A, backend layer, Swagger/`.http`/cURL as the client stub | Developer | Final |
| ASP.NET Core, EF Core, SQLite, xUnit, GitHub Actions | Developer | Final |
| Four-project layering, no mediator, no microservices | Developer (contract) | Final |
| Investigate reservation key `(ResourceKind, ResourceId, SlotStartUtc)`; real-database concurrency tests | Developer (contract) | Implemented |
| .NET 10 instead of 8 (both 8 and 9 end support on 10 Nov 2026), xUnit 4 on Microsoft Testing Platform | Agent, from live sources | Implemented |
| 15-minute grid; vehicle as a third reserved resource; 3 allocation attempts; first-fit allocation | Agent | Awaiting developer sign-off |
| Reject timestamps without an offset; instant-based availability scan for DST safety | Agent | Awaiting developer sign-off |
| 422 for unknown ids in a body; `POST .../cancel`; idempotency semantics (replay header, 422 on reuse) | Agent | Awaiting developer sign-off |
| Avoid MediatR, AutoMapper, FluentAssertions (commercial licences since 2025) | Agent | Implemented |

## Verification approach

The agent's output was treated as untrusted until checked by something other than the agent's own reasoning:

- **Live sources instead of model memory** for every version: the .NET release index, NuGet, xUnit release notes, and
  GitHub for Actions releases and their commit SHAs.
- **The compiler as a reviewer:** warnings as errors, nullable reference types, analyzers, NuGet vulnerability audit.
- **The running system:** a smoke test against the real API, plus manual inspection of actual JSON responses, not just status codes.
- **Tests that can fail:** the concurrency tests were checked by deliberately disabling conflict handling (they failed),
  then run repeatedly to rule out flakiness.
- **The real pipeline:** every CI step was executed locally with the same commands before the first push, then confirmed on GitHub Actions.

## Verification log

Chronological; every entry was observed in this session.

| # | What was checked | Result |
|---|---|---|
| 1 | .NET release index | 10.0 is the active LTS (SDK 10.0.403, support to 2028-11-14); 8.0 and 9.0 end support on 2026-11-10 |
| 2 | NuGet for every package | EF Core 10.0.14, OpenTelemetry 1.20.1, Swashbuckle SwaggerUI 10.3.1, xunit.v3 4.0.2 and others pinned centrally |
| 3 | xUnit 4.0 release notes and Microsoft's `dotnet test` docs | xUnit 4 uses Microsoft Testing Platform v2, which the .NET 10 SDK only runs in MTP mode, so `global.json` sets the test runner |
| 4 | Build after each layer | 0 warnings, 0 errors each time |
| 5 | Generated migration, read line by line | Reservation primary key correct; all instants stored as INTEGER including the nullable one; check constraints and unique idempotency index present |
| 6 | Smoke test against the running API | First run aborted on a script bug (health endpoint returns text, not JSON); after the fix, 21 of 21 checks passed |
| 7 | Manual review of real responses | Found three contract defects (items B1-B3 below); each fixed and re-checked live |
| 8 | First full test run | 61 of 88 passed. Two root causes (B4, T1); after fixes 88 of 88 |
| 9 | Mutation check | Conflict detector forced to `false`: all 4 concurrency tests failed in 3 of 3 runs; change reverted |
| 10 | Flakiness | Concurrency and cancellation classes: 10 consecutive green runs |
| 11 | Coverage run | Exposed an environment-dependent test (T2); fixed; a test for the 500 path was added; 89 of 89 |
| 12 | Formatting | `dotnet format --verify-no-changes` exit 0 |
| 13 | CI emulation | Restore, format, Release build, migration drift check, tests with TRX and coverage, and the smoke job on a fresh database all passed |
| 14 | Pre-commit review | Found B5 and T3; both fixed |
| 15 | Commits and a clean clone | All commits authored by the personal identity with no trailers; a fresh clone built with 0 warnings and passed 89 of 89 tests |
| 16 | First GitHub Actions run (after the developer confirmed the push) | Both jobs green on Ubuntu: 0 warnings, no migration drift, 89 of 89 tests, smoke test 21 of 21 |
| 17 | Developer's final-audit directive: independent security review in a separate read-only session | No confirmed vulnerabilities; two inaccurate statements in the review rejected after checking the code (see [SECURITY.md](SECURITY.md#review-performed-27-october-2026)) |
| 18 | Gap review against the directive | No test for persistence across a restart or for `no_service_bays`; request bodies only limited on booking. Added two tests and a 32 KB server-wide limit |
| 19 | Full reviewer-experience run (restore, format, build, vulnerability audit, migration drift, whitespace, live API, Swagger, smoke test, restart, size limits) | First attempt checked nothing: the script ran from the wrong directory (T4). Second attempt: all passed except a 20 KB body returning **500** (B6) |
| 20 | After fixing B6 | Live 413; 92 of 92 tests; 97.5% line coverage of hand-written code; no vulnerable packages |
| 21 | Audit commits pushed (developer confirmed) | GitHub Actions run 40465027142 green: 0 warnings, no drift, 92 of 92 tests, smoke 21 of 21 |

## Issues found and fixed

Product defects:

| ID | Defect | How it was found | Fix |
|---|---|---|---|
| B0 | A retried request with an idempotency key could get 409 `vehicle_unavailable` instead of its original appointment, if the first attempt committed between the retry's key check and its availability read | Agent's race review of its own design, before coding | Re-check the key on every conflict path (`ReplayOrAsync`); covered by a simultaneous-retry test |
| B1 | `createdAtUtc` in the 201 response had microseconds, but the stored value has milliseconds, so a later read differed | Inspecting real responses | Truncate the clock to milliseconds before creating the appointment |
| B2 | 400 responses carried a misleading extra error, "The request field is required" | Inspecting real responses | `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` |
| B3 | Validation errors were keyed by C# names (`StartTime`) while the contract is camelCase | Inspecting real responses | `SystemTextJsonValidationMetadataProvider` |
| B4 | Every business error was sent as `application/json` instead of `application/problem+json`: a class-level `[Produces]` filter overwrote the content type. The smoke test (status codes and fields only) missed it | 26 failing integration tests | Removed the class-level filter; success media types declared per response |
| B5 | The generated `.gitignore` did not exclude SQLite files, so the development database would have been committed | Pre-commit review | Added `*.db`, `*.db-shm`, `*.db-wal` |
| B6 | A request body over the size limit returned 500 and was logged as an error, because the custom exception handler treated Kestrel's `BadHttpRequestException` as unexpected. The in-memory test server does not enforce Kestrel limits, so no test could see it | Live check against the real server | Keep the exception's status (413) with a problem document; test injects the exception directly |

Test and tooling defects:

| ID | Defect | Fix |
|---|---|---|
| T1 | The database-constraint test failed inside EF Core's identity map before reaching SQLite, so it was not testing the constraint | Clear the change tracker between the two writes, as separate requests would |
| T2 | The architecture test required "only `System.*` references", which coverage instrumentation breaks | Assert the forbidden references instead |
| T3 | The smoke test's "outside opening hours" request (07:00 UTC) is a valid time during British Summer Time | Use 05:00 UTC, closed in both seasons |
| T4 | The verification script changed into its own folder (`/tmp`) instead of the repository, so its first run checked nothing | Point it at the repository; the output was read rather than trusting the exit status |

## What was generated

The agent wrote all source code, tests, scripts, CI configuration and documentation in this repository. The developer
directed the work through the contract and conversation, and the review status of the agent's design proposals is shown
above.

## Limits of the verification

- No load test; no run against PostgreSQL or SQL Server; OTLP export not tested against a real backend.
- Decisions marked "awaiting developer sign-off" have not yet been reviewed by the developer.

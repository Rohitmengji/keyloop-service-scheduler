# Implementation status

Last updated: 27 October 2026.

## Phases

| Phase | Status | Evidence |
|---|---|---|
| 0. Environment and workspace audit | Done | .NET SDK and Docker absent; .NET 10.0.403 installed user-locally; GitHub CLI signed in to the personal account |
| 1. Requirements and design | Done | [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md), [REQUIREMENTS.md](REQUIREMENTS.md), six ADRs |
| 2. Repository foundation | Done | Solution, central package management, analyzers, `.editorconfig`, agent and instruction files |
| 3. Domain and persistence | Done | Aggregate with invariants; migration `InitialSchema` with keys, checks and indexes |
| 4. Application and API | Done | Six endpoints, problem details with codes, idempotency, OpenAPI, health checks |
| 5. Testing | Done | 89 tests passing; concurrency tests validated by a mutation check |
| 6. Security and observability | Done (scope of this submission) | [SECURITY.md](SECURITY.md), [OBSERVABILITY.md](OBSERVABILITY.md); identity and rate limiting documented as production gaps |
| 7. CI and documentation | Done | `.github/workflows/ci.yml` emulated locally step by step; documentation set complete |
| 8. Verification | Done locally | See below |
| 9. Demo preparation | Done | [DEMO_GUIDE.md](DEMO_GUIDE.md), `samples/scheduler.http`, `scripts/smoke-test.sh` |
| 10. GitHub delivery | Done | Pushed to the personal account; first CI run green (see below) |

## Requirement status

| Requirement | Status |
|---|---|
| Resource-constrained booking (FR1) | Implemented and tested |
| Availability of bay **and** qualified technician for the whole duration (FR2) | Implemented and tested, including simultaneous requests |
| Persistent appointment record (FR3) | Implemented and tested |
| Supporting: availability, read, cancel, idempotent retries (FR4-FR7) | Implemented and tested |

## Latest local verification (26-27 October 2026, macOS arm64, SDK 10.0.403)

| Check | Command | Result |
|---|---|---|
| Formatting | `dotnet format ServiceScheduler.slnx --verify-no-changes` | Exit 0 |
| Build | `dotnet build ServiceScheduler.slnx -c Release` | 0 warnings, 0 errors (warnings are errors) |
| Migration drift | `dotnet ef migrations has-pending-model-changes ...` | "No changes have been made to the model since the last migration." |
| Tests | `dotnet test --solution ServiceScheduler.slnx -c Release -- --report-xunit-trx --coverage` | 89 passed, 0 failed |
| Coverage (hand-written code) | cobertura from the run above | 97.6% of lines |
| Concurrency test sensitivity | conflict detector forced to `false` | 4 of 4 concurrency tests failed in 3 of 3 runs; reverted |
| Flakiness | concurrency and cancellation classes run 10 times | 10 of 10 green |
| Live API | `scripts/smoke-test.sh` against `dotnet run` on a fresh database | 21 of 21 checks passed; no errors in the API log |
| Clean clone | `git clone` into an empty folder, then Release build and tests | 0 warnings; 89 passed, 0 failed |

## Continuous integration (GitHub Actions, Ubuntu)

First run on 27 October 2026 ([run 40462693170](https://github.com/Rohitmengji/keyloop-service-scheduler/actions/runs/40462693170)):
both jobs succeeded. Build 0 warnings and 0 errors; no migration drift; 89 of 89 tests passed; the smoke test passed
21 of 21 checks against the API started on the runner.

## Outstanding

- [ ] Developer sign-off on the agent-proposed decisions in ADR-0003, ADR-0004 and ADR-0005.
- [ ] Record the 5-10 minute video walkthrough.

## Known limitations

No authentication or authorisation; SQLite single-writer; no load test; no OTLP export exercised against a real backend.
See [TRADEOFFS.md](TRADEOFFS.md) and [SECURITY.md](SECURITY.md).

# Final verification report

Date: 27 October 2026. Environment: macOS 26.7.1 arm64, .NET SDK 10.0.403 (repository pins 10.0.100 or later
10.0.x), no Docker. Every result below was observed directly; nothing is extrapolated.

**Status key:** PASS = verified by actual evidence · FAIL = verification failed · BLOCKED = environment prevented
verification · NOT RUN = not attempted.

## 1. Executive summary

The Scenario A backend is complete. All three brief requirements, plus availability, read, cancellation and idempotent
retries, are implemented and verified by 92 automated tests and a live end-to-end run. The final audit found and fixed one
real defect (oversized request bodies returned 500 instead of 413) and closed two test gaps (restart persistence,
dealerships without bays). There are no known failing checks. Outstanding items are external: recording the video and
sending the submission.

## 2. Implemented requirements and evidence

| Requirement | Status | Evidence |
|---|---|---|
| Book for vehicle, service type, dealership and time | PASS | `BookingTests`, smoke test "book Sam's Focus" (201) |
| Bay **and** qualified technician free for the whole duration | PASS | `ResourceAllocationTests`, `ConcurrencyTests`, smoke test "a third car ... finds no free bay" (409) |
| Persistent appointment linking customer, vehicle, technician and bay | PASS | `BookingTests` (reads back, 12 reservation rows), `RestartPersistenceTests`, live restart check |
| Conflicts rejected without corrupting state | PASS | `ConcurrencyTests.The_database_rejects_a_second_claim...` (no partial rows) |
| Availability reflects persisted reservations | PASS | `AvailabilityTests.Fully_booked_times_disappear...` |
| Idempotent creation (replay and mismatch) | PASS | `IdempotencyTests`, simultaneous-retry test, smoke test replay (201 + header) and mismatch (422) |
| Cancellation releases slots | PASS | `CancellationTests`, smoke test |

Requirement-by-requirement detail: [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) and [REQUIREMENTS.md](REQUIREMENTS.md).

## 3. Architecture and major decisions

Four layers (Domain, Application, Infrastructure, Api), EF Core with SQLite, double booking prevented by a database
primary key on per-slot reservations for bay, technician and vehicle, UTC storage with explicit offsets on input,
RFC 9457 problem details with stable codes. See [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md) and [DECISIONS.md](DECISIONS.md).

## 4. Build results

| Check | Command | Exit code | Result | Status |
|---|---|---|---|---|
| Restore | `dotnet restore ServiceScheduler.slnx` | 0 | Restored | PASS |
| Tool restore | `dotnet tool restore` | 0 | dotnet-ef 10.0.14 | PASS |
| Formatting | `dotnet format ServiceScheduler.slnx --verify-no-changes --no-restore` | 0 | No changes needed | PASS |
| Build (Release, warnings as errors) | `dotnet build ServiceScheduler.slnx -c Release --no-restore` | 0 | 0 warnings, 0 errors | PASS |
| Migration drift | `dotnet ef migrations has-pending-model-changes --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api -c Release --no-build` | 0 | "No changes have been made to the model since the last migration." | PASS |
| Database initialisation | `dotnet run` on a deleted database | — | Migrated, seeded, `/health/ready` = Healthy | PASS |
| Whitespace | `git diff --check <empty tree>` | 0 | No whitespace errors | PASS |

## 5. Test results

| Suite | Command | Exit code | Result | Status |
|---|---|---|---|---|
| All tests | `dotnet test --solution ServiceScheduler.slnx -c Release --no-build` | 0 | 92 total, 92 passed, 0 failed | PASS |
| Unit | (part of the above) | — | 37 passed | PASS |
| Integration (real SQLite per test) | (part of the above) | — | 55 passed | PASS |
| Coverage | same, with `-- --coverage --coverage-output-format cobertura` | 0 | 97.5% of hand-written lines (Domain 96.2, Application 99.5, Infrastructure 100, Api 91.9) | PASS |
| Concurrency test sensitivity | conflict detector forced to `false` (26 Oct) | — | 4 of 4 concurrency tests failed in 3 of 3 runs; reverted | PASS |
| Flakiness | concurrency and cancellation classes ×10 (26 Oct) | — | 10 of 10 green | PASS |

## 6. API smoke-test results (live server, fresh database)

| Check | Result | Status |
|---|---|---|
| `/health/ready` | `Healthy` | PASS |
| Swagger UI | HTTP 200; booking operation pre-filled with demo ids (checked in a browser) | PASS |
| OpenAPI document | OpenAPI 3.1.1, 6 paths | PASS |
| `scripts/smoke-test.sh` | "All 21 checks passed": availability, booking, idempotent replay, key mismatch, vehicle/bay conflicts, opening hours, grid alignment, missing offset, ownership, unqualified technician, read back, cancel twice, rebook, 404 | PASS |
| Restart persistence | Appointment identical before and after stopping and restarting the API; no re-seed | PASS |
| Oversized body | 20 KB booking body → 413 (was 500 before the fix) | PASS |
| API log | No error entries | PASS |
| `samples/scheduler.http` in an IDE | Not executed; requests mirror the smoke test | NOT RUN |

## 7. Security review findings

| Finding | Severity | Status |
|---|---|---|
| Oversized request body returned 500 and was logged as an error | Low | Fixed and tested |
| Request-body limit only on the booking endpoint | Low | Fixed: 32 KB server-wide |
| Idempotency keys not scoped per client | Low | Accepted; documented (needs authentication first) |
| `AllowedHosts: *`; list endpoints unpaged | Info | Documented production gaps |
| No authentication, authorisation, rate limiting, audit | — | Out of scope; documented, not claimed |
| SQL injection, error leakage, personal data in logs, secrets, CI permissions | — | Checked, no issues ([SECURITY.md](SECURITY.md#review-performed-27-october-2026)) |

## 8. Dependency review

| Check | Result | Status |
|---|---|---|
| `dotnet list ServiceScheduler.slnx package --vulnerable --include-transitive` | No vulnerable packages in all 6 projects | PASS |
| NuGet audit in the build (warnings as errors) | Build succeeded | PASS |
| Versions | Pinned centrally in `Directory.Packages.props`; not upgraded during the audit | PASS |

## 9. CI status

| Run | Commit | Result | Status |
|---|---|---|---|
| [40462693170](https://github.com/Rohitmengji/keyloop-service-scheduler/actions/runs/40462693170) | `3a30495` and earlier | Both jobs green: 0 warnings, no drift, 89/89 tests, smoke 21/21 | PASS |
| Latest run on `e5a2df6` | `e5a2df6` | Both jobs green | PASS |
| Run including this audit's changes | not yet pushed | — | NOT RUN |

## 10. Documentation status

| Item | Status |
|---|---|
| README (build, run, test, API demo, AI Collaboration Narrative) | PASS — followed from a clean clone |
| System design (all required sections, diagrams parse) | PASS |
| Relative links and anchors across all Markdown | PASS (checked by script on 26 Oct; re-checked after this audit) |
| Test counts, coverage and example dates match current results | PASS |
| Agent-proposed decisions marked as awaiting sign-off | PASS (ADR-0003 to 0005 still await the developer) |

## 11. Known limitations

SQLite allows one writer at a time (correctness holds; throughput is limited). No authentication or authorisation. No load
test. OTLP export not exercised against a real backend. PostgreSQL not tried (Docker unavailable). The `.http` collection
was not run inside an IDE.

## 12. Remaining manual submission steps

1. Review and sign off ADR-0003, ADR-0004 and ADR-0005.
2. Push this audit's commits and confirm the CI run is green.
3. Record the video using [VIDEO_WALKTHROUGH.md](VIDEO_WALKTHROUGH.md) (Loom), and check it is viewable with the link.
4. Reply to the recruiter with the repository URL and the video link before Friday 30 October 2026.

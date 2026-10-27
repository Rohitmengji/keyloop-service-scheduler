# Implementation status

Last updated: 27 October 2026. Acceptance criteria come from the official brief (`KeyloopCodingChallange.pdf`,
Scenario A). Evidence for every check is in [FINAL_VERIFICATION_REPORT.md](FINAL_VERIFICATION_REPORT.md).

Status values: **Implemented and verified**, **Implemented but not verified**, **Partially implemented**,
**Not implemented**, **Blocked by the environment**.

## Scenario A core requirements

| Requirement (brief) | Status | Implementation | Evidence |
|---|---|---|---|
| 1. Resource-constrained booking: request an appointment for a vehicle, service type and dealership at a desired time | Implemented and verified | `Api/Controllers/AppointmentsController.cs`, `Api/Contracts/BookAppointmentRequest.cs`, `Application/Appointments/BookAppointmentHandler.cs` | `BookingTests.Booking_a_free_slot_confirms_a_persistent_appointment_with_a_bay_and_a_qualified_technician`; smoke test |
| 2. Real-time availability: a service bay **and** a qualified technician free for the entire duration | Implemented and verified | `Domain/Scheduling/ResourceOccupancy.cs`, allocation loop in `BookAppointmentHandler.cs`, `PK_SlotReservations` in `Infrastructure/Persistence/Configurations/AppointmentConfigurations.cs` | `ResourceAllocationTests.*`, `ConcurrencyTests.*`, `ResourceOccupancyTests.*`, `AppointmentTests.An_unqualified_technician_cannot_be_allocated` |
| 3. Confirmed appointment record associating customer, vehicle, technician and bay | Implemented and verified | `Domain/Appointments/Appointment.cs`, migration `Persistence/Migrations/*_InitialSchema.cs` | `BookingTests.Booking_a_free_slot...`, `RestartPersistenceTests.Appointments_and_their_reservations_survive_an_application_restart` |

## Functional checklist from the final-audit directive

| # | Capability | Status | Evidence |
|---|---|---|---|
| 1 | Accept a request for dealership, vehicle, service type and start time | Implemented and verified | `BookingTests.*` |
| 2 | Bay availability for the full duration | Implemented and verified | `ResourceAllocationTests.When_every_active_bay_is_taken_the_next_booking_is_a_conflict` (partial overlap) |
| 3 | Technician availability **and** qualification for the full duration | Implemented and verified | `ResourceAllocationTests.When_the_only_qualified_technician_is_busy...`, `BookingTests.A_dealership_with_no_qualified_technician_cannot_offer_the_service` |
| 4 | Persist the appointment with all relationships | Implemented and verified | `BookingTests.Booking_a_free_slot...` (12 reservation rows checked), foreign keys in the migration |
| 5 | Reject conflicts without corrupting the database | Implemented and verified | `ConcurrencyTests.The_database_rejects_a_second_claim_on_a_slot_and_rolls_back_the_whole_booking` |
| 6 | Meaningful errors for invalid input and conflicts | Implemented and verified | Every error code has an integration test ([REQUIREMENTS.md](REQUIREMENTS.md)) |
| 7 | Availability endpoint reflects persisted reservations | Implemented and verified | `AvailabilityTests.Fully_booked_times_disappear_while_back_to_back_starts_remain` |
| 8 | Idempotent creation | Implemented and verified | `IdempotencyTests.*`, `ConcurrencyTests.Simultaneous_retries_with_one_idempotency_key_create_one_appointment` |
| 9 | Records survive an application restart | Implemented and verified | `RestartPersistenceTests.*`; live: same appointment returned after stopping and restarting `dotnet run` |
| 10 | Cancellation releases slots (extension) | Implemented and verified | `CancellationTests.*` |

## Deliverables

| Deliverable | Status | Evidence |
|---|---|---|
| System design document (diagram, components, data flow, technologies, observability, GenAI in design) | Implemented and verified | [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md); all Mermaid diagrams parsed with Mermaid 11 |
| RESTful API with a persistent database | Implemented and verified | SQLite file, EF Core migrations, live run |
| Client stub: OpenAPI, Swagger UI, cURL | Implemented and verified | `/openapi/v1.json` (OpenAPI 3.1.1, 6 paths), Swagger UI HTTP 200, `scripts/smoke-test.sh` 21/21 |
| Client stub: `samples/scheduler.http` | Implemented but not verified | Not executed in an IDE; the same requests are exercised by the smoke test |
| README with build, run, test and AI Collaboration Narrative | Implemented and verified | Followed from a clean clone |
| Test suite for core business logic | Implemented and verified | 92 tests pass locally and on GitHub Actions (run 40465027142) |
| Video walkthrough (5-10 minutes) | Not implemented | Developer records it; plan in [VIDEO_WALKTHROUGH.md](VIDEO_WALKTHROUGH.md) |

## Non-functional

| Area | Status | Evidence / note |
|---|---|---|
| Concurrency safety | Implemented and verified | Simultaneous-request tests; mutation check showed they fail without conflict handling |
| Time-zone and DST correctness | Implemented and verified | `OpeningHoursTests.*`, offset tests in `BookingTests` |
| Error handling (no internal details, correct 4xx for server-detected errors) | Implemented and verified | `ApiSurfaceTests.Unexpected_failures...`, `...Server_detected_client_errors...`; live 413 |
| Health checks | Implemented and verified | `ApiSurfaceTests.Health_endpoints_report_healthy`; CI readiness wait |
| Structured logging and metrics | Implemented and verified (locally) | `ApiSurfaceTests.Rejected_bookings_are_counted_by_reason` |
| OpenTelemetry export to a backend (New Relic etc.) | Implemented but not verified | Exporter only enabled when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; no backend available |
| Production database (PostgreSQL / SQL Server) | Not implemented | Documented migration path; Docker unavailable on this machine to try it (blocked by the environment) |
| Authentication, authorisation, rate limiting | Not implemented (out of scope) | Documented in [SECURITY.md](SECURITY.md) |
| Load / performance testing | Not implemented | No measured latency figures are claimed |

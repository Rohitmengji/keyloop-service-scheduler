# Requirements and traceability

Every requirement maps to the code that implements it and the tests that prove it. Test names are the method names in
`tests/`.

## Functional requirements

| ID | Requirement | Implementation | Verified by |
|---|---|---|---|
| FR1 | Request an appointment for a vehicle, service type and dealership at a desired time | `BookAppointmentRequest`, `AppointmentsController.Book`, `BookAppointmentHandler` | `BookingTests.Booking_a_free_slot_confirms_a_persistent_appointment_with_a_bay_and_a_qualified_technician` |
| FR2 | A service bay **and** a qualified technician must be free for the **entire** duration | `ResourceOccupancy`, allocation loop in `BookAppointmentHandler`, `PK_SlotReservations` | `ResourceAllocationTests.*`, `ConcurrencyTests.*`, `ResourceOccupancyTests.*`, `AppointmentTests.An_unqualified_technician_cannot_be_allocated` |
| FR3 | Persist an appointment linking customer, vehicle, technician and bay | `Appointment.Confirm`, `AppointmentConfiguration`, migration `InitialSchema` | `BookingTests.Booking_a_free_slot...` (reads back via `Location`, counts reservation rows), `RestartPersistenceTests.Appointments_and_their_reservations_survive_an_application_restart`, `AppointmentTests.Confirming_reserves_the_bay_technician_and_vehicle_for_every_slot` |
| FR4 | List bookable start times | `GetAvailabilityHandler`, `Dealership.CandidateRanges` | `AvailabilityTests.*`, `OpeningHoursTests.Candidate_*` |
| FR5 | Read an appointment | `AppointmentQueries.GetByIdAsync` | `BookingTests.Booking_a_free_slot...`, `CancellationTests.Unknown_appointments_are_not_found` |
| FR6 | Cancel and release resources | `CancelAppointmentHandler`, `Appointment.Cancel` | `CancellationTests.*`, `AppointmentTests.Cancelling_*`, `AppointmentTests.An_appointment_that_has_started_cannot_be_cancelled` |
| FR7 | Retries never create duplicates | Idempotency key + fingerprint on `Appointment`, `FindReplayAsync`, unique index | `IdempotencyTests.*`, `ConcurrencyTests.Simultaneous_retries_with_one_idempotency_key_create_one_appointment`, `RequestFingerprintTests.*` |

## Non-functional requirements

| Requirement | Implementation | Verified by |
|---|---|---|
| No double booking under concurrency, across instances | Reservation primary key + retry loop | `ConcurrencyTests.*` (plus a mutation check, see [TEST_STRATEGY.md](TEST_STRATEGY.md)) |
| Atomic booking | Single `SaveChanges` transaction | `ConcurrencyTests.The_database_rejects_a_second_claim_on_a_slot_and_rolls_back_the_whole_booking` |
| Time-zone and DST correctness | `ExplicitOffsetDateTimeOffsetConverter`, `Dealership.IsOpenThroughout`, UTC storage | `OpeningHoursTests.*`, `BookingTests.Timestamps_without_an_explicit_utc_offset_are_rejected`, `...another_offset...`, `...own_time_zone` |
| Errors never leak internals | `UnhandledExceptionHandler`, problem details | `ApiSurfaceTests.Unexpected_failures_return_a_generic_problem_without_internal_details`, `ApiSurfaceTests.Server_detected_client_errors_keep_their_status_instead_of_becoming_500` |
| Business metrics | `SchedulerTelemetry` | `ApiSurfaceTests.Rejected_bookings_are_counted_by_reason` |
| Layering | Project references | `ArchitectureTests.*` |
| Documented contract | OpenAPI generation, XML comments | `ApiSurfaceTests.The_openapi_document_describes_the_booking_contract` |
| Operability | Health checks | `ApiSurfaceTests.Health_endpoints_report_healthy` |

## Assessment test checklist

The execution contract listed scenarios that must be tested. Each is covered:

| Scenario | Test |
|---|---|
| Valid booking | `BookingTests.Booking_a_free_slot_confirms_a_persistent_appointment_with_a_bay_and_a_qualified_technician` |
| Invalid dealership / vehicle / service type (and customer) | `BookingTests.Unknown_ids_in_the_request_are_unprocessable` (4 cases) |
| No qualified technician | `BookingTests.A_dealership_with_no_qualified_technician_cannot_offer_the_service` |
| No active service bays | `BookingTests.A_dealership_without_active_bays_cannot_take_bookings` |
| Unavailable service bay | `ResourceAllocationTests.When_every_active_bay_is_taken_the_next_booking_is_a_conflict` |
| Unavailable technician / overlapping appointment | `ResourceAllocationTests.When_the_only_qualified_technician_is_busy_an_overlapping_booking_is_a_conflict` |
| Back-to-back appointments | `ResourceAllocationTests.Back_to_back_appointments_can_share_a_bay_and_technician`, `ResourceOccupancyTests` |
| Vehicle double booking | `ResourceAllocationTests.A_vehicle_cannot_be_booked_into_two_overlapping_appointments_even_at_different_dealerships` |
| Multiple concurrent booking requests | `ConcurrencyTests.Simultaneous_requests_for_the_last_bays_never_double_book`, `...only_qualified_technician...` |
| Persistent appointment creation | `BookingTests.Booking_a_free_slot...` |
| Persistence across application restarts | `RestartPersistenceTests.Appointments_and_their_reservations_survive_an_application_restart` |
| Transaction rollback | `ConcurrencyTests.The_database_rejects_a_second_claim_on_a_slot_and_rolls_back_the_whole_booking` |
| Invalid timestamps | `BookingTests.Timestamps_without_an_explicit_utc_offset_are_rejected`, `...on_the_15_minute_grid`, `...in_the_future_and_within_the_booking_horizon` |
| Time-zone conversions | `BookingTests.The_same_instant_sent_in_another_offset...`, `...dealerships_own_time_zone`, `OpeningHoursTests.*` |
| Outside operating hours | `BookingTests.The_whole_service_must_fit_inside_opening_hours`, `...may_finish_exactly_at_closing_time` |
| Idempotent replay | `IdempotencyTests.Retrying_with_the_same_key_returns_the_original_appointment_instead_of_booking_again` |
| Idempotency key reused with different data | `IdempotencyTests.Reusing_a_key_for_a_different_booking_is_rejected` |
| Database constraint enforcement | `ConcurrencyTests.The_database_rejects_a_second_claim...` |
| Cancellation and slot release | `CancellationTests.Cancelling_releases_the_slots_so_the_time_can_be_booked_again` |

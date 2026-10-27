# Assumptions

The brief is intentionally ambiguous. These are the assumptions made, why, and what would change if they were wrong.

| # | Assumption | Rationale | If it changes |
|---|---|---|---|
| A1 | Appointments start on a **15-minute grid** and durations are whole multiples of 15 minutes. | Workshop diaries are planned in quarter hours; a grid lets the database enforce non-overlap with a primary key. | Finer grids multiply reservation rows; free-form times would need an exclusion constraint (PostgreSQL) instead. |
| A2 | A booking needs **one bay, one technician and the vehicle** for the whole duration. | The brief names bay and technician; a vehicle cannot be in two appointments at once. | Multi-technician jobs would allocate several technicians; drop-off/collect windows would separate vehicle time from bay time. |
| A3 | **Any active bay can host any service.** | The brief only constrains technicians by qualification. | Add bay capabilities (lift, MOT lane, EV charger) mirroring technician qualifications. |
| A4 | A technician is **qualified per service type**. | Simple, matches "qualified technician" in the brief. | Skills with levels or certifications with expiry dates would replace the join table. |
| A5 | Each dealership has **one opening window per day, every day**, in its own IANA time zone; technicians work those hours. | Keeps the model small while making time-zone handling real. | Per-weekday hours, holidays and technician shifts become availability calendars per resource. |
| A6 | **Customers and vehicles already exist** in another system (CRM/DMS); this service references them. | Customer management is not part of the scenario. | Add create/lookup endpoints or sync from the DMS. |
| A7 | The **customer must be the vehicle's registered keeper**. | Prevents booking someone else's car. | Fleet and family use would add authorised drivers. |
| A8 | **Allocation is first fit** in a stable order (bays and technicians by name). | Predictable and explainable; the brief does not ask for optimisation. | Least-loaded technician or skill-matching strategies plug into the same step. |
| A9 | The service picks the bay and technician; **clients cannot request a specific technician**. | Matches "check availability of a bay and a qualified technician". | Add optional preferred technician, falling back to any. |
| A10 | Appointments can be booked **up to 90 days ahead** (configurable) and only in the future. | Bounds data growth and matches typical workshop horizons. | Change `Scheduling:BookingHorizonDays`. |
| A11 | **Cancellation** is allowed until the start time and keeps the record. | Common workshop policy; history matters. | Cut-off windows or fees would add rules to `Appointment.Cancel`. |
| A12 | **Idempotency keys are optional**, global and kept with the appointment. | Easy for reviewers to try; still safe because the vehicle rule rejects duplicates. | Scope keys per authenticated client and expire them. |
| A13 | **No authentication** in this submission; the API would sit behind a gateway. | The brief asks for the booking capability; identity is a platform concern. | See [SECURITY.md](SECURITY.md). |
| A14 | UTC offsets are **multiples of 15 minutes**. | True of every current time zone, so the UTC grid is also the local grid. | A zone with another offset would need a local-time grid. |

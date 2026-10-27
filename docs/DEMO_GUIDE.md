# Demo guide

A ten-minute path through the system for reviewers. Everything uses the seeded demo data
([DATA_MODEL.md](DATA_MODEL.md#demo-data)).

## 1. Start it (1 minute)

```bash
dotnet run --project src/ServiceScheduler.Api
```

Open <http://localhost:5080/swagger>.

## 2. Requirement 1 and 2 in Swagger (5 minutes)

| Step | Swagger operation | What to show |
|---|---|---|
| 1 | `GET /api/v1/dealerships` | Manchester has two active bays; Chloe is the only `EV_HEALTH` technician; Pune has nobody for `MOT`. |
| 2 | `GET /api/v1/dealerships/{id}/availability` with Manchester and the oil change id | Free starts for tomorrow in local time, with free bay and technician counts. Copy the first `start`. |
| 3 | `POST /api/v1/appointments` with the example body, `startTime` = the copied value, `Idempotency-Key: demo-1` | **201**: bay and technician allocated, times in the dealership's offset, `Location` header. |
| 4 | Execute step 3 again unchanged | **201** with the same id and `Idempotent-Replayed: true`: a retry does not double-book. |
| 5 | Same body, change `notes`, same key | **422** `idempotency_key_reused`. |
| 6 | Same body, no key, Emma's Golf (`customerId` `c0...02`, `vehicleId` `ca...03`) | **201** in Bay 2 with another technician. |
| 7 | Same time, Arjun's Creta (`c0...03`, `ca...04`) | **409** `service_bay_unavailable`: both bays are taken for part of the hour. |
| 8 | EV check (`5e...05`) for Sam's Leaf (`ca...02`) at a free time, then Emma's Tesla (`c0...02`, `ca...05`) 15 minutes later | **409** `technician_unavailable`: the only EV technician is busy, although a bay is free. |
| 9 | `startTime` without an offset, e.g. `2026-11-02T09:00:00` | **400**: the API never guesses a time zone. |
| 10 | Pune (`de...02`), MOT (`5e...02`), Arjun | **422** `no_qualified_technician`. |

## 3. Requirement 3 and lifecycle (1 minute)

- `GET /api/v1/appointments/{id}` returns the stored record with customer, vehicle, bay and technician.
- `POST /api/v1/appointments/{id}/cancel` releases the slots; repeat step 7 and Arjun now gets a bay.

## 4. Proof (3 minutes)

```bash
dotnet test                       # 92 tests, including simultaneous-request races against SQLite
./scripts/smoke-test.sh           # 21 end-to-end checks against the running API
```

Show `ConcurrencyTests.Simultaneous_requests_for_the_last_bays_never_double_book` and the reservation primary key in
`SlotReservationConfiguration`, then the CI run on GitHub (build, format, migration drift, tests, live smoke test).

The same flow without Swagger: [samples/scheduler.http](../samples/scheduler.http).

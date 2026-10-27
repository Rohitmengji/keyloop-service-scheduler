# API contract

Base URL locally: `http://localhost:5080`. The machine-readable contract is the OpenAPI 3.1 document at
`/openapi/v1.json` (browse it with Swagger UI at `/swagger`; both are enabled in development).

## Conventions

- JSON with camelCase property names; enums as strings (`"Confirmed"`).
- **Timestamps in requests must include a UTC offset**: `2026-11-02T09:00:00+00:00` or `2026-11-02T09:00:00Z`.
  A value without an offset is rejected (400), because it would otherwise be read in the server's time zone.
- Timestamps in responses use the dealership's local offset (`+00:00` for London in winter, `+01:00` in summer,
  `+05:30` for Pune); `timeZone` names the IANA zone.
- Start times must be on a 15-minute boundary with zero seconds.
- Errors are RFC 9457 problem documents (`application/problem+json`) with a stable `code` and a `traceId`.
- The version is part of the path: `/api/v1`.

## Endpoints

### `GET /api/v1/service-types`

```json
[{ "id": "5e000000-0000-0000-0000-000000000005", "code": "EV_HEALTH", "name": "EV battery health check", "durationMinutes": 30 }]
```

### `GET /api/v1/dealerships`

Each dealership with its time zone, opening hours, active service bays and active technicians with the service codes
they are qualified for.

### `GET /api/v1/dealerships/{dealershipId}/availability?serviceTypeId={id}&date={yyyy-MM-dd}`

Start times on a local date when at least one bay and one qualified technician are free for the whole service.
`date` is optional and defaults to the next local day at the dealership. Past starts and starts beyond the booking
horizon are not offered. The vehicle's own diary is checked at booking time.

```json
{
  "dealershipId": "de000000-0000-0000-0000-000000000001",
  "timeZone": "Europe/London",
  "date": "2026-11-02",
  "serviceType": { "id": "5e000000-0000-0000-0000-000000000001", "code": "OIL", "name": "Oil and filter change", "durationMinutes": 60 },
  "slots": [
    { "start": "2026-11-02T08:00:00+00:00", "end": "2026-11-02T09:00:00+00:00", "freeServiceBays": 2, "freeTechnicians": 3 }
  ]
}
```

### `POST /api/v1/appointments`

Headers: `Content-Type: application/json`; optional `Idempotency-Key` (1-100 visible ASCII characters, a UUID is ideal).

```json
{
  "dealershipId": "de000000-0000-0000-0000-000000000001",
  "customerId": "c0000000-0000-0000-0000-000000000001",
  "vehicleId": "ca000000-0000-0000-0000-000000000002",
  "serviceTypeId": "5e000000-0000-0000-0000-000000000005",
  "startTime": "2026-11-02T09:00:00Z",
  "notes": "Customer will wait on site"
}
```

`201 Created` with `Location: /api/v1/appointments/{id}`:

```json
{
  "id": "01a17939-7363-7639-a204-716b62a9773f",
  "status": "Confirmed",
  "start": "2026-11-02T09:00:00+00:00",
  "end": "2026-11-02T09:30:00+00:00",
  "timeZone": "Europe/London",
  "dealership": { "id": "de000000-0000-0000-0000-000000000001", "name": "Northgate Motors Manchester" },
  "customer": { "id": "c0000000-0000-0000-0000-000000000001", "name": "Sam Taylor" },
  "vehicle": { "id": "ca000000-0000-0000-0000-000000000002", "registration": "MA22 EVL", "make": "Nissan", "model": "Leaf" },
  "serviceType": { "id": "5e000000-0000-0000-0000-000000000005", "code": "EV_HEALTH", "name": "EV battery health check", "durationMinutes": 30 },
  "serviceBay": { "id": "ba000000-0000-0000-0000-000000000001", "name": "Bay 1" },
  "technician": { "id": "7e000000-0000-0000-0000-000000000003", "name": "Chloe Davies" },
  "notes": "Customer will wait on site",
  "createdAtUtc": "2026-11-01T12:00:00+00:00",
  "cancelledAtUtc": null
}
```

**Idempotency.** Repeating the request with the same key and the same body returns the original appointment with
`201` and `Idempotent-Replayed: true`, without booking again; this also holds when retries arrive simultaneously. The
same instant written with a different offset counts as the same body. Reusing a key for a different body returns `422
idempotency_key_reused`. Only successful bookings consume a key, so a request that failed can be retried with the same
key. Keys are global in this version (production would scope them per client).

### `GET /api/v1/appointments/{id}`

`200` with the appointment (same shape as above) or `404 appointment_not_found`.

### `POST /api/v1/appointments/{id}/cancel`

Cancels the appointment and releases its bay, technician and vehicle slots. Returns `200` with the appointment
(`status: "Cancelled"`, `cancelledAtUtc` set). Cancelling an already-cancelled appointment returns the same response.
An appointment that has started cannot be cancelled (`422 appointment_already_started`). `POST .../cancel` was chosen
over `DELETE` because the record is kept; the state change is the resource being created.

### Health

`GET /health/live` — the process is running. `GET /health/ready` — the database is reachable and has no pending
migrations. Both return `Healthy` as plain text with 200, or 503.

## Error catalogue

Example problem document:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The requested time is not available.",
  "status": 409,
  "detail": "No qualified technician is free for the whole service duration at the requested time.",
  "traceId": "00-2d44b9e3af5a7b8477c95ef57ad3e3a5-fab61bb63d0d4cb2-01",
  "code": "technician_unavailable"
}
```

| Status | `code` | Meaning |
|---|---|---|
| 400 | `validation_failed` | Malformed JSON, missing fields, timestamp without offset, notes over 500 characters, unparseable query values. `errors` lists each field. |
| 400 | `invalid_idempotency_key` | Header is empty, longer than 100 characters, or has non-visible characters. |
| 404 | `appointment_not_found` | No appointment with the id in the URL. |
| 404 | `dealership_not_found` | No dealership with the id in the availability URL. |
| 404 | `not_found` | Unknown route. |
| 409 | `service_bay_unavailable` | Every active bay is busy for part of the requested time. |
| 409 | `technician_unavailable` | Every qualified technician is busy for part of the requested time. |
| 409 | `vehicle_unavailable` | The vehicle already has an overlapping appointment (any dealership). |
| 409 | `allocation_contention` | Lost repeated races to simultaneous bookings; retrying is safe. |
| 422 | `unknown_dealership`, `unknown_customer`, `unknown_vehicle`, `unknown_service_type` | An id in the request body does not exist. |
| 422 | `vehicle_not_owned_by_customer` | The vehicle is registered to another customer. |
| 422 | `start_time_not_aligned` | Start is not on a 15-minute boundary. |
| 422 | `start_time_in_past` | Start has already passed. |
| 422 | `beyond_booking_horizon` | Start is more than 90 days ahead (configurable). |
| 422 | `outside_opening_hours` | The service would start before opening or finish after closing (local time). |
| 422 | `no_service_bays` | The dealership has no active bays. |
| 422 | `no_qualified_technician` | Nobody at the dealership is qualified for the service. |
| 422 | `idempotency_key_reused` | The key was already used for a different booking. |
| 422 | `appointment_already_started` | Cancellation requested after the start time. |
| 413 / 415 / 405 | `payload_too_large` / `unsupported_media_type` / `method_not_allowed` | Framework-level rejections. Request bodies are limited to 32 KB, and booking bodies to 16 KB. |
| 500 | `internal_error` | Unexpected failure. No internal details are returned; use `traceId` to find the logs. |

`notes_too_long` also exists in the application layer as a guard for non-HTTP callers; over HTTP the same condition is
reported earlier as `validation_failed`.

## cURL

```bash
curl -s "http://localhost:5080/api/v1/dealerships/de000000-0000-0000-0000-000000000001/availability?serviceTypeId=5e000000-0000-0000-0000-000000000001"

curl -s -X POST http://localhost:5080/api/v1/appointments \
  -H 'Content-Type: application/json' -H "Idempotency-Key: $(uuidgen)" \
  -d '{"dealershipId":"de000000-0000-0000-0000-000000000001","customerId":"c0000000-0000-0000-0000-000000000001",
       "vehicleId":"ca000000-0000-0000-0000-000000000001","serviceTypeId":"5e000000-0000-0000-0000-000000000001",
       "startTime":"<a start from the availability response>"}'
```

[samples/scheduler.http](../samples/scheduler.http) chains these calls automatically, and
[scripts/smoke-test.sh](../scripts/smoke-test.sh) runs 21 checks against a live instance.

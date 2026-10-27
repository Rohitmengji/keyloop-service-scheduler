# Observability

Built on .NET's native diagnostics (`ILogger`, `ActivitySource`, `Meter`) and exported with OpenTelemetry, so the same
signals work with New Relic, Grafana, Azure Monitor, Datadog or the .NET Aspire dashboard by configuration alone.

## Logs

- Structured logging through source-generated `[LoggerMessage]` methods (no string formatting cost when disabled, stable
  event ids, consistent field names).
- Production format is JSON (`appsettings.json`) with UTC timestamps and scopes; every entry carries `TraceId` and `SpanId`
  so logs join to traces. Development uses single-line text for readability.
- **Privacy rule:** log fields are ids and codes only. No names, emails, VINs, registrations or notes are logged.

| Event id | Level | Event | Fields |
|---|---|---|---|
| 1001 | Information | Appointment booked | AppointmentId, DealershipId, ServiceBayId, TechnicianId, StartUtc |
| 1002 | Information | Booking rejected | DealershipId, ErrorCode |
| 1003 | Warning | Allocation lost a race and is retrying | DealershipId, Attempt |
| 1101 | Information | Appointment cancelled | AppointmentId |
| 2001 | Information | Demo data seeded | — |
| 3001 | Error | Unhandled exception | Method, Path, exception |
| 3002 | Debug | Client aborted the request | Method, Path |
| 3003 | Information | Request rejected by the server (for example 413 body too large) | Method, Path, StatusCode |

EF Core SQL logging is off by default; set `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` to
`Information` to see every query while developing.

## Traces

- ASP.NET Core server spans for every request (health probes excluded) via OpenTelemetry instrumentation; incoming
  W3C `traceparent` headers are honoured, so a gateway or front end can join the trace.
- Business spans from `ActivitySource("ServiceScheduler")`:
  - `BookAppointment` — tags `scheduler.dealership_id`, `scheduler.service_type_id`, `scheduler.outcome`
    (`booked`, `replayed` or the error code) and `scheduler.allocation_attempts`.
  - `CancelAppointment` — tag `scheduler.appointment_id`.
- Problem responses include the `traceId`, so a support request can be traced from the client's error message.

## Metrics

Meter `ServiceScheduler`, plus ASP.NET Core's built-in HTTP server metrics (`http.server.request.duration` and friends).

| Instrument | Type | Tags | Use |
|---|---|---|---|
| `scheduler.appointments.booked` | Counter | `service_type` | Booking volume by service |
| `scheduler.bookings.rejected` | Counter | `reason` (error code) | Why bookings fail; spikes in `service_bay_unavailable` mean demand exceeds capacity |
| `scheduler.bookings.allocation_conflicts` | Counter | — | Contention between simultaneous bookings |
| `scheduler.bookings.idempotent_replays` | Counter | — | Client retries (timeouts upstream) |
| `scheduler.appointments.cancelled` | Counter | — | Cancellation volume |

The rejection counter is covered by `ApiSurfaceTests.Rejected_bookings_are_counted_by_reason`.

Watch metrics locally without any backend:

```bash
dotnet tool install --global dotnet-counters
dotnet-counters monitor --name ServiceScheduler.Api --counters ServiceScheduler,Microsoft.AspNetCore.Hosting
```

## Health

| Endpoint | Checks | Use |
|---|---|---|
| `/health/live` | None (the process responds) | Liveness probe: restart if it fails |
| `/health/ready` | Database reachable **and** no pending migrations | Readiness probe: take out of the load balancer if it fails |

## Exporting to New Relic (or any OTLP backend)

Export is off unless an endpoint is configured, so the API runs locally with no telemetry infrastructure. New Relic
accepts OTLP natively:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=https://otlp.nr-data.net:4317
export OTEL_EXPORTER_OTLP_HEADERS="api-key=<New Relic ingest licence key>"
dotnet run --project src/ServiceScheduler.Api
```

The service reports itself as `service-scheduler`. Logs, traces and metrics are then exported together. For a local UI,
run the .NET Aspire dashboard container and point `OTEL_EXPORTER_OTLP_ENDPOINT` at it.

## Suggested dashboards and alerts

| Signal | Alert when |
|---|---|
| HTTP 5xx rate | Above 1% for 5 minutes |
| p95 `POST /api/v1/appointments` latency | Above 500 ms for 10 minutes |
| `scheduler.bookings.rejected{reason="allocation_contention"}` | Any sustained rate (indicates hot slots or too few retries) |
| `scheduler.bookings.allocation_conflicts` / bookings | Above 5% (contention rising; consider a server database) |
| `/health/ready` failures | Any (database down or migration not applied) |
| `scheduler.bookings.idempotent_replays` | Sudden spike (upstream timeouts) |

Suggested service-level objectives: 99.9% of booking requests answered without a 5xx; p95 booking latency under
300 ms. These are targets for a production deployment; no load test has been run in this submission.

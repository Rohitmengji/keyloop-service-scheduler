# Runbook

## Prerequisites

- **.NET SDK 10.0.100 or later 10.0.x** (developed with 10.0.403). Check with `dotnet --list-sdks`.
  Install from <https://dotnet.microsoft.com/download/dotnet/10.0>, or without admin rights:
  `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0` then add `~/.dotnet` to `PATH`.
- Optional: `jq` and `curl` for `scripts/smoke-test.sh`; the VS Code REST Client extension, Visual Studio or Rider for
  `samples/scheduler.http`.

No database server, Docker or secrets are needed.

## Run locally

```bash
dotnet run --project src/ServiceScheduler.Api      # http://localhost:5080, Swagger UI at /swagger
```

In development the API creates `src/ServiceScheduler.Api/scheduler.db`, applies migrations and seeds demo data.
**Reset the data:** stop the API and delete `scheduler.db*`.

## Configuration

| Key (environment variable form) | Default | Purpose |
|---|---|---|
| `ConnectionStrings__Scheduler` | `Data Source=scheduler.db` | SQLite connection string |
| `Database__MigrateOnStartup` | `false` (`true` in Development) | Apply migrations at start-up |
| `Database__SeedDemoData` | `false` (`true` in Development) | Seed demo data into an empty database |
| `Scheduling__BookingHorizonDays` | `90` | How far ahead bookings may start (1-365, validated at start-up) |
| `Scheduling__MaxAllocationAttempts` | `3` | Retries after losing a booking race (1-10) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables Swagger, readable logs, migration and seeding |
| `ASPNETCORE_URLS` | — | Listening addresses outside `dotnet run` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` / `_HEADERS` | unset | Enables OpenTelemetry export ([OBSERVABILITY.md](OBSERVABILITY.md)) |

## Database migrations

```bash
# Add a migration after changing an entity configuration
dotnet ef migrations add <Name> --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api --output-dir Persistence/Migrations

# Check the model and migrations agree (CI runs this)
dotnet ef migrations has-pending-model-changes --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api

# Production: build a self-contained migration bundle in the pipeline and run it before deploying the new version
dotnet ef migrations bundle --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api -o efbundle
./efbundle --connection "Data Source=/var/lib/scheduler/scheduler.db"
```

`dotnet ef` is pinned in `dotnet-tools.json`; run `dotnet tool restore` once.

## Deploying

- Run with `ASPNETCORE_ENVIRONMENT=Production`, migrations applied by the pipeline, `Database__MigrateOnStartup=false`.
- Liveness probe `/health/live`, readiness probe `/health/ready` (fails until migrations are applied).
- Logs are JSON on stdout; ship them with the platform's log agent or via OTLP.
- Put the API behind a gateway that terminates TLS and provides identity and rate limiting ([SECURITY.md](SECURITY.md)).
- SQLite supports a single node. For more than one instance, move to PostgreSQL or SQL Server first
  ([ADR-0003](adr/0003-double-booking-prevention.md)).

## Backup and restore (SQLite)

```bash
sqlite3 scheduler.db ".backup 'scheduler-$(date +%F).db'"   # online, consistent with WAL
```

Restore by stopping the API and replacing the file. A managed server database would use point-in-time restore instead.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `dotnet: command not found` after a user-local install | Add `export DOTNET_ROOT="$HOME/.dotnet"` and `export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"` to your shell profile. |
| "A compatible .NET SDK was not found" | `global.json` needs SDK 10.0.100+; install a .NET 10 SDK. |
| "Testing with VSTest target is no longer supported" | Run `dotnet test` from the repository root so `global.json` selects Microsoft Testing Platform mode. |
| Port 5080 already in use | Another instance is running: `lsof -i :5080`, or start with `--urls http://localhost:5081` (then set `BASE_URL` for the smoke test). |
| Bookings return 422 `start_time_in_past` in old examples | Example dates go stale; take a start from the availability endpoint (the `.http` file and smoke test do this automatically). |
| `database is locked` | Another process (for example a database browser) holds a write transaction on `scheduler.db`; close it. |
| `/health/ready` returns 503 | Database unreachable or migrations not applied; check logs, run the migration bundle. |

## Responding to alerts

| Alert | First steps |
|---|---|
| 5xx rate | Find the `traceId` from a failing response or the error log (event 3001); follow the trace; roll back if introduced by a deployment. |
| `allocation_contention` rejections | Hot slots under simultaneous demand: check `scheduler.bookings.allocation_conflicts`; raise `MaxAllocationAttempts` slightly or move to a server database. |
| Spike in `service_bay_unavailable` / `technician_unavailable` | Demand above capacity, or bays/technicians marked inactive by mistake; check `GET /api/v1/dealerships`. |
| Readiness failing after deploy | A migration was not applied; run the bundle for that release. |

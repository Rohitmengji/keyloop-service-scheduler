# Security

Scope: what this submission implements, how it was checked, and what a production deployment still needs. Nothing
below claims a control that is not in the code.

## Threat review

| Threat | Example | Mitigation in this service | Evidence |
|---|---|---|---|
| Injection | SQL in an id or notes field | Ids are typed GUIDs; all queries are EF Core LINQ with parameters; no raw SQL except a constant `PRAGMA` | A code search for `FromSql`, `ExecuteSql` and `SqlQuery` finds only the constant PRAGMA in `DatabaseInitializer` |
| Malformed or hostile input | Huge body, missing fields, bad timestamps, control characters in headers | Model validation (required ids, notes ≤ 500), strict timestamp parser, idempotency-key character and length rules, 16 KB body limit on booking, typed route constraints (`{id:guid}`) | `BookingTests` validation cases, `IdempotencyTests.Malformed_keys...` |
| Information disclosure | Stack trace or connection string in an error | Generic 500 problem document; exception details only in server logs; no developer exception page in any environment | `ApiSurfaceTests.Unexpected_failures_return_a_generic_problem_without_internal_details` |
| Personal data leakage | Customer names or VINs in logs/metrics | Logs, spans and metric tags carry only ids and error codes; responses include customer name and registration but never email or VIN | Code review of `LoggerMessage` definitions and `SchedulerTelemetry` |
| Tampering / double booking | Two clients racing for one bay | Database primary key on reservations; optimistic concurrency token on appointments | `ConcurrencyTests.*` |
| Replay / duplicate submission | Client retries after a timeout | Idempotency key with request fingerprint; vehicle rule rejects duplicates without a key | `IdempotencyTests.*`, `ResourceAllocationTests.Submitting_the_same_booking_twice...` |
| Resource exhaustion | Booking years ahead to fill the reservations table | 90-day booking horizon; availability limited to one day per request | `BookingTests.Start_times_must_be_in_the_future_and_within_the_booking_horizon` |
| Fingerprinting | Server banner | Kestrel `Server` header disabled | `Program.cs` |
| Supply chain | Vulnerable package, hijacked CI action | NuGet audit (all, including transitive) with warnings as errors; Dependabot for NuGet and Actions; actions pinned to commit SHAs; workflow token `contents: read` | `Directory.Build.props`, `.github/workflows/ci.yml`, `.github/dependabot.yml` |
| Transport | Plain HTTP in production | HSTS and HTTPS redirection outside development; TLS normally terminates at the gateway | `Program.cs` |

## Secrets and configuration

- No secrets exist in this repository. The only connection string points at a local SQLite file.
- Production settings come from environment variables or a secret store (for example Azure Key Vault or AWS Secrets
  Manager) using standard ASP.NET Core configuration keys such as `ConnectionStrings__Scheduler`.
- The OTLP exporter reads its endpoint and headers (for example a New Relic licence key) from `OTEL_EXPORTER_OTLP_*`
  environment variables; nothing is hard-coded.
- Development-only behaviour (Swagger UI, automatic migration and demo seeding) is switched on by `appsettings.Development.json`
  and is off by default in `appsettings.json`.

## Not implemented (required before production)

| Gap | Recommended approach |
|---|---|
| **Authentication** | OAuth 2.0 / OpenID Connect bearer tokens validated by the API (`AddAuthentication().AddJwtBearer`) and the gateway. |
| **Authorisation** | Dealership-scoped policies: staff can act only for their dealership; a customer can only book their own vehicles (customer id taken from the token, not the body); cancellation limited to the owner or staff. |
| **Rate limiting and quotas** | At the API gateway (Azure API Management, Apigee, AWS API Gateway), with ASP.NET Core's rate limiter as a second line. |
| **Idempotency-key scoping** | Unique per (client, key) once clients are authenticated; expire keys after 24 hours. |
| **Audit trail** | Append-only record of who booked or cancelled what and when (an outbox event stream would also feed notifications). |
| **Data protection** | Database encryption at rest, least-privilege database login (DML only; migrations run under a separate deploy identity), retention policy for past appointments and personal data (UK GDPR). |
| **CORS** | Configure only when a browser client on another origin is introduced; none is enabled now. |
| **Security testing in CI** | Add CodeQL (static analysis) and an OWASP ZAP baseline scan against the smoke-test instance. |

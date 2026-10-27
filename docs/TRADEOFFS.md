# Trade-offs and future improvements

## Trade-offs made

| Decision | Benefit | Cost | Mitigation / exit |
|---|---|---|---|
| Per-slot reservation rows with a primary key | The database itself guarantees no overlap, on any provider, across any number of API instances | 3 rows per 15 minutes booked; fixed granularity | Rows are only needed for future slots and can be purged; PostgreSQL exclusion constraints remove the grid if needed |
| SQLite | Zero set-up, real constraints and transactions, fast tests | One writer at a time; file on one node | Same schema and guard on PostgreSQL/SQL Server; only provider registration and the violation detector change |
| Optimistic allocation with retries | No locks held while reading; normal case is one transaction | Under extreme contention a request can lose 3 races and get a retryable 409 | Retry count is configurable; contention is measured by a metric |
| First-fit allocation | Predictable, testable, explainable | Work is not balanced across technicians | Swap the selection step for least-loaded or skill-ranked choice |
| Application layer uses EF Core directly | Explicit, efficient queries; no repository boilerplate | Application depends on EF Core abstractions | Provider-specific logic is isolated; repositories can be introduced if a non-relational store appears |
| Idempotency stored on the appointment | Atomic with the booking; no extra table | Only successful requests are remembered; keys never expire | Separate idempotency table with expiry once clients are authenticated |
| Request validation with data annotations | Built in, no extra package | Less expressive than FluentValidation | Business rules are in handlers and the domain, where they belong |
| Controllers instead of minimal APIs | Familiar to ASP.NET MVC/Web API teams, automatic validation, rich OpenAPI metadata | Slightly more ceremony | — |
| No caching | Availability is always exact | Every availability call hits the database | Short-lived cache invalidated on booking/cancellation if needed |
| No load test | Time spent on correctness evidence | No measured latency or throughput figures | Add k6 or NBomber scenario in CI against PostgreSQL |

## Future improvements

**Next for production**
1. Authentication and dealership-scoped authorisation; rate limiting at the gateway ([SECURITY.md](SECURITY.md)).
2. PostgreSQL (or SQL Server) with a migration bundle in the deployment pipeline.
3. Transactional outbox publishing `AppointmentBooked` / `AppointmentCancelled` events for customer notifications and the DMS.
4. Purge or archive reservations for past slots.

**Product features**
5. Technician shifts, absences and dealership holidays (resource calendars).
6. Bay capabilities (MOT lane, EV charger, alignment rig).
7. Reschedule as one atomic operation (cancel and rebook in one transaction).
8. Search across several days ("next available slot") and preferred technician.
9. Multi-service visits (several services back to back for one vehicle).

**Engineering**
10. Load test with latency SLOs; CodeQL and an OWASP ZAP baseline scan in CI.
11. Contract snapshot test for the OpenAPI document to catch breaking changes.
12. Split into separate services (booking, resources, notifications) only if team or scaling needs appear; the current
    layering and an outbox make that a mechanical change.

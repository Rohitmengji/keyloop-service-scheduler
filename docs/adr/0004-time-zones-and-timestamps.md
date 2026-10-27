# ADR-0004: Time zones and timestamps

- **Status:** Proposed
- **Decided by:** Agent, within the contract's requirement for "explicit timestamp offset handling" — awaiting developer sign-off.

## Context

Dealerships operate on local wall-clock hours (08:00-18:00 in Manchester means 07:00-17:00 UTC in summer and 08:00-18:00
UTC in winter). Clients, servers and dealerships can be in different zones. A timestamp without an offset is ambiguous,
and on clock-change days some local times occur twice or not at all.

## Decision

1. **Input must carry an explicit offset** (`Z` or `±hh:mm`). A custom JSON converter rejects anything else with 400,
   because System.Text.Json would otherwise interpret a bare time in the server's zone.
2. **Instants are UTC** everywhere in the domain and database (Unix milliseconds in SQLite). The same instant written with
   different offsets is the same booking, including for idempotency fingerprints.
3. **Opening hours are local times** with an **IANA time-zone id** per dealership. A booking is valid only if its start and
   end, converted to local time, fall on the same local date inside opening hours.
4. **Availability scans instants, not local times.** For a local date, every UTC slot from 14 hours before to 36 hours after
   UTC midnight is tested and kept if it belongs to that local date and fits opening hours. This avoids converting
   non-existent or repeated local times on daylight-saving days.
5. **Responses use the dealership's local offset** and include the zone name, so clients can display times without
   their own time-zone database.
6. `TimeProvider` is injected everywhere "now" matters; tests use `FakeTimeProvider`.

## Alternatives considered

- **Accept local times with the dealership's zone implied.** Friendlier for forms, but ambiguous around clock changes and
  silently wrong if a client sends the wrong zone.
- **Store `DateTimeOffset` text.** SQLite cannot compare or order it reliably across offsets.
- **Store `DateTime` with `Kind=Utc`.** Works, but the kind is lost on read from SQLite and must be re-applied everywhere.
- **NodaTime.** Excellent model, but an extra dependency for a problem the base library handles here.

## Consequences

- Unit tests cover London on both 2026 clock-change days and Pune's +05:30 offset; integration tests cover offset
  normalisation and local-offset responses.
- The UTC grid equals the local grid only because every current offset is a multiple of 15 minutes (assumption A14).

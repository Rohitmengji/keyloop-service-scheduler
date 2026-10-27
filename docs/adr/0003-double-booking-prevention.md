# ADR-0003: Prevent double booking with per-slot reservations enforced by the database

- **Status:** Accepted
- **Decided by:** The execution contract asked to investigate a unique key on `(ResourceKind, ResourceId, SlotStartUtc)`.
  The agent proposed the 15-minute grid, treating the vehicle as a third resource, and the retry policy — awaiting
  developer sign-off.

## Context

Two requests for the same bay or technician at overlapping times can arrive simultaneously, possibly on different API
instances. "Check, then insert" in application code has a race window between the check and the insert. The guarantee
must hold regardless of how many instances run.

## Decision

1. Appointments start on a **15-minute grid** and last whole slots.
2. Confirming an appointment inserts one `SlotReservation` row per slot for **its bay, its technician and its vehicle**.
3. The reservation table's **primary key is `(ResourceKind, ResourceId, SlotStartUtc)`**. Overlapping appointments
   always share a slot, so the database rejects the second one; back-to-back appointments share none.
4. The appointment and all its reservations are written in **one transaction**; a violation rolls back everything.
5. The handler checks occupancy first (to return a precise 409 in the normal case). If it loses a race, it clears its
   unit of work, re-reads occupancy and retries with the next free resource, **up to 3 attempts**
   (`Scheduling:MaxAllocationAttempts`), then returns 409 `allocation_contention`.
6. Cancelling deletes the appointment's reservations in the same transaction as the status change.

## Alternatives considered

| Option | Why not |
|---|---|
| In-process lock or `SemaphoreSlim` | Only protects one process; fails as soon as there are two instances. |
| Distributed lock (Redis) | Extra infrastructure and failure modes (expiry, fencing); the database is already the source of truth. |
| Serializable transaction around read-and-insert | Correct on server databases but provider-dependent, causes deadlocks/aborts under load, and SQLite serialises writers anyway. |
| Query for overlapping appointments inside the transaction | Still racy at read-committed isolation without range locks. |
| PostgreSQL `EXCLUDE USING gist` on `tstzrange` | Elegant and grid-free, but PostgreSQL-only; noted as an option when moving to PostgreSQL. |

## Consequences

- Correctness does not depend on application code being bug-free: a test that bypasses the application layer shows the
  database rejecting a clash and writing none of the failed booking's rows.
- Integration tests race real requests; with conflict handling deliberately disabled they fail, proving they exercise the guard.
- Storage cost: 3 rows per booked slot (12 for a one-hour service). Past rows can be purged.
- Granularity is fixed in data; changing it requires a data migration.
- On SQLite, writers queue (WAL mode and a busy timeout). On PostgreSQL/SQL Server the same design runs in parallel with
  only the violation detector changing (`23505`; `2627`/`2601`).

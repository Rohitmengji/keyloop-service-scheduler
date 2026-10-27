# Architecture decisions

Each significant decision is recorded as an Architecture Decision Record (context, decision, alternatives,
consequences, status). **Decided by** shows whether the decision came from the developer's execution contract or was
proposed by the AI agent, and whether the developer has signed it off.

| ADR | Decision | Status | Decided by |
|---|---|---|---|
| [0001](adr/0001-layered-architecture.md) | Four-project layered architecture with inward dependencies | Accepted | Developer (execution contract) |
| [0002](adr/0002-technology-and-database.md) | .NET 10 LTS, ASP.NET Core controllers, EF Core 10, SQLite | Accepted | Developer (contract); versions verified by the agent |
| [0003](adr/0003-double-booking-prevention.md) | Per-slot reservation rows with a database primary key, optimistic allocation with retries | Accepted | Approach from the contract; grid size, vehicle claim and retry policy proposed by the agent — awaiting developer sign-off |
| [0004](adr/0004-time-zones-and-timestamps.md) | Explicit offsets on input, UTC storage, local opening hours, instant-based slot scan | Proposed | Agent — awaiting developer sign-off |
| [0005](adr/0005-api-design-and-errors.md) | REST resources, one status per error kind, problem details with codes, idempotency keys | Accepted | Status rules from the contract; 422-for-unknown-ids, cancel endpoint and idempotency semantics proposed by the agent — awaiting developer sign-off |
| [0006](adr/0006-testing-approach.md) | Real SQLite integration tests per test, fake clock, no persistence mocks | Accepted | Developer (contract) |

Supporting documents: [ASSUMPTIONS.md](ASSUMPTIONS.md) and [TRADEOFFS.md](TRADEOFFS.md).

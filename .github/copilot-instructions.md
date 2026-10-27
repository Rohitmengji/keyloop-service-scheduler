# Copilot instructions

This repository's engineering contract is [AGENTS.md](../AGENTS.md). Read it before making changes; it overrides
general habits. In particular:

- The double-booking guard is the primary key on `SlotReservations`; never weaken it.
- Business rules belong in `ServiceScheduler.Domain` or the application handlers, not controllers or EF configurations.
- Expected failures are `SchedulingErrors` values mapped to problem details; error `code`s are public contract.
- Use the injected `TimeProvider`; instants are UTC; API input needs an explicit offset.
- Verify package versions on NuGet before adding or upgrading; never pin from memory.
- Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` before reporting work as done, and report
  the actual results.
- Never invent test results, reviews or CI runs in documentation.
- Never add AI tools as commit authors or co-authors; never push without the developer's confirmation.

Scoped rules: [.github/instructions](instructions). Specialised agents: [.github/agents](agents).

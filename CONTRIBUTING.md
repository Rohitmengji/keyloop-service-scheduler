# Contributing

The engineering rules are in [AGENTS.md](AGENTS.md); this page is the practical workflow.

## Set up

1. Install the .NET 10 SDK (see [docs/RUNBOOK.md](docs/RUNBOOK.md#prerequisites)).
2. `dotnet tool restore` (installs the pinned `dotnet-ef`).
3. `dotnet test` should pass before you change anything.

## Workflow

1. Branch from `main`.
2. Make the change with its tests. For schema changes, add a migration:
   ```bash
   dotnet ef migrations add <DescriptiveName> --project src/ServiceScheduler.Infrastructure \
     --startup-project src/ServiceScheduler.Api --output-dir Persistence/Migrations
   ```
3. Run the same gates as CI:
   ```bash
   dotnet format ServiceScheduler.slnx --verify-no-changes
   dotnet build ServiceScheduler.slnx -c Release
   dotnet ef migrations has-pending-model-changes --project src/ServiceScheduler.Infrastructure --startup-project src/ServiceScheduler.Api
   dotnet test
   ```
4. If you changed the API, run the API and `./scripts/smoke-test.sh`, and update `docs/API_CONTRACT.md`.
5. Open a pull request; CI must be green.

## Adding things

| Adding... | Steps |
|---|---|
| An endpoint | Handler in `Application` returning `Result<T>`; thin action in a controller; XML comments and `ProducesResponseType` for every status; integration tests for success and each error code |
| A business rule | Domain invariant if it must always hold, handler check returning a new `SchedulingErrors` entry; unit and integration tests |
| A package | Version in `Directory.Packages.props` (check NuGet for the current stable version); justify it in the PR; the NuGet audit must stay clean |

## Commits

- Imperative, present-tense subject lines (for example `Release slots when an appointment is cancelled`).
- Use your own identity. Do not add AI tools as authors or co-authors.
- Never commit secrets, `*.db` files, `bin/`, `obj/` or `TestResults/`.

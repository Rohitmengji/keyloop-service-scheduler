---
description: "Use when editing Markdown: README, system design, ADRs, API contract, runbook, implementation status or the AI collaboration record."
applyTo: "**/*.md"
---
# Documentation rules

- Every claim must be true of the current code and of results actually observed. Re-run a command before quoting its
  output; never write "CI is green" or a test count without having seen it.
- One authoritative place per topic; link instead of copying. `SYSTEM_DESIGN.md` summarises and links to the detailed docs.
- When behaviour changes, update `API_CONTRACT.md` (endpoints, error codes), the affected ADR, `REQUIREMENTS.md` (test
  names) and `IMPLEMENTATION_STATUS.md`.
- ADRs: Status, Decided by (developer, contract or agent, and whether signed off), Context, Decision, Alternatives
  considered, Consequences. Add a row to `DECISIONS.md`.
- `AI_COLLABORATION.md` distinguishes developer decisions from agent proposals and records only real events.
- Mermaid diagrams must match the implemented components and names; keep labels quoted and use `<br/>` for line breaks.
- Plain, direct English; short sentences; tables for comparisons; no marketing language.

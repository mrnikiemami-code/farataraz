# AGENTS.md — FaraTaraz / فراتراز

Concise operating instructions for agents working in this repository. Read
[`docs/architecture/architecture-constitution.md`](docs/architecture/architecture-constitution.md)
before making any architecture-affecting change. The constitution is authoritative.

## What this product is
A **multi-tenant, provider-agnostic Accounting Intelligence Platform**. It is **not**
an AI dashboard for Asan Accounting. Accounting systems are replaceable adapters.

## Repository map
- `src/BuildingBlocks/` — foundation: strong ids, `Tenant`, `AccountingSource`,
  `TenantContext`, provider capability primitives. Depends on nothing platform-wide.
- `src/Core/` — platform domain: capability ports, source model, master data
  (external + canonical identities), application authority marker. Depends only on
  `BuildingBlocks`.
- `src/Adapters/*` — concrete providers (e.g. `Accounting.Mock`). Depend on `Core`.
- `tests/FaraTaraz.ArchitectureTests/` — durable automated guards (see below).
- `docs/architecture/` — constitution + ADRs.

## Allowed dependency direction (enforced by tests)
```
BuildingBlocks  →  (nothing platform-wide)
Core            →  BuildingBlocks
Adapters/*      →  Core, BuildingBlocks
Hosts/*         →  Core, Adapters/*  (added later, when needed)
Tests           →  whatever they assert
```
Never reverse these. `Core` must never reference a concrete adapter.

## Highest-risk invariants (break these and tests fail)
- **Tenant boundary** — every tenant-owned artifact is tenant-bound; tenant authority
  comes only from a trusted execution context, never from client/LLM/MCP input.
- **Provider independence** — no `Asan*`/`Sepidar*`/`Holoo*`/`Mahak*` types, DTOs, or
  field names in `Core` or `BuildingBlocks`. Adding a provider = a new adapter.
- **Source-scoped external identity** — `AccountingSourceId + ExternalCode`. Equal codes
  across sources do NOT prove equal canonical entities.
- **Canonical identity** — FaraTaraz-owned and tenant-bound.
- **Dependency direction** — never reverse the graph above.
- **Sync idempotency** — duplicate source records must not create duplicate sales.
- **Application authority** — REST, MCP, and sync all delegate to Application use cases.
  No `Dashboard → Database`, `MCP → Database`, or `LLM → Database`.
- **MCP restrictions** — MCP has zero independent business calculation authority.
- **LLM restrictions** — the LLM explains deterministic results; it never calculates
  authoritative numbers (demand, totals, stock, reorder, customer metrics).

## Rules when editing
- Keep changes minimal and coherent; do not mix unrelated experimentation.
- Do not implement business features (sales/inventory/customer intelligence, forecasting,
  dashboard, MCP tools, AI, real sync, Asan integration) unless the active task says so.
- Prefer strongly-typed concepts over primitive strings where identity confusion is
  dangerous (ids, tenant/source context). Do not abstract for abstraction's sake.
- Credentials/secrets never enter Domain models.
- When you add or change anything that touches the invariants above, add or update an
  architecture test in `tests/FaraTaraz.ArchitectureTests/`. If a rule cannot be
  automated yet, document exactly why and when it must become executable.

## Planning & source of truth

The repository is the source of truth for execution. Before starting any work, read:

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md` (authoritative)
3. `docs/planning/CURRENT-STATE.md`
4. relevant ADRs (`docs/architecture/adr/`)
5. relevant roadmap wave (`docs/planning/ROADMAP.md`)

SoT hierarchy (highest first): Constitution → accepted ADRs → Current State → Roadmap /
Delivery Plan → current authorized task → implementation. If a task conflicts with the
Constitution or an accepted ADR, STOP; do not silently follow the lower-level instruction.

Waves are not auto-started. P0 does NOT authorize W1; a separate explicit task is required.

## Build & test
```bash
dotnet build FaraTaraz.sln
dotnet test FaraTaraz.sln
```
Both must pass before committing.

## Git
One coherent change per commit. Do not force push. If `origin` is not
`https://github.com/mrnikiemami-code/farataraz`, stop and report before pushing.

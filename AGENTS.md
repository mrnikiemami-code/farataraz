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
- `src/Modules/*` — platform domain, decomposed from the former `Core` into capability
  modules: `MasterData` (external + canonical identities), `AccountingSources`
  (capability ports, provider declaration, ownership oracle), and `Ingestion`
  (`Ingestion.Domain` sync contract + source models; `Ingestion.Application` CQRS use
  cases). Module Domain/Application depend only on `BuildingBlocks`.
- `src/Adapters/*` — concrete providers (e.g. `Accounting.Mock`). Depend on modules and `BuildingBlocks`; never referenced by Host, Domain, or Application.
- `src/Host/FaraTaraz.Host/` — composition-only Host; ZERO business authority, concrete adapter references, or persistence ownership.
- `tests/FaraTaraz.ArchitectureTests/` — durable automated guards (see below).
- `docs/architecture/` — constitution + ADRs.
- `docs/planning/RECOVERY.md` — mandatory fast-resume checkpoint, task handoff, and latest verified evidence.

## Allowed dependency direction (enforced by tests)
```
BuildingBlocks  →  (nothing platform-wide)
Modules/*       →  BuildingBlocks (+ sibling Domain modules where needed)
Adapters/*      →  Modules, BuildingBlocks
Host            →  Modules, BuildingBlocks (composition only; NO Adapters or persistence)
Module Infrastructure / Adapters →  Module contracts/ports, BuildingBlocks
Tests           →  whatever they assert
```
Never reverse these. The platform (`BuildingBlocks` + modules) must never reference a concrete adapter.

## Highest-risk invariants (break these and tests fail)
- **Tenant boundary** — every tenant-owned artifact is tenant-bound; tenant authority
  comes only from a trusted execution context, never from client/LLM/MCP input.
- **Provider independence** — no `Asan*`/`Sepidar*`/`Holoo*`/`Mahak*` types, DTOs, or
  field names in the platform modules or `BuildingBlocks`. Adding a provider = a new adapter.
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
3. `docs/planning/RECOVERY.md` (latest task checkpoint; update on every task handoff)
4. `docs/planning/CURRENT-STATE.md`
5. relevant ADRs (`docs/architecture/adr/`)
6. relevant roadmap wave (`docs/planning/ROADMAP.md`)

SoT hierarchy (highest first): Constitution → accepted ADRs → Current State → Roadmap /
Delivery Plan → current authorized task → implementation. If a task conflicts with the
Constitution or an accepted ADR, STOP; do not silently follow the lower-level instruction.

Waves are not auto-started. P0 does NOT authorize W1; a separate explicit task is required.

## Mandatory recovery discipline
- At task start: fetch origin/master, read `docs/planning/RECOVERY.md`, validate its SHA against Git history, then reconcile it with `CURRENT-STATE.md` and the roadmap. Never assume the recovery SHA equals current HEAD: documentation commits may follow it.
- At every meaningful wave boundary and before the final push: update `RECOVERY.md` with task ID, last verified commit/baseline, completed/pending work, affected files, test evidence, next exact step, blockers, and handoff instructions.
- Recovery is a checkpoint, not a substitute for builds, tests, architecture guards, or accepted ADRs. Never mark an unreviewed wave CERTIFIED.
- New modules/layers must extend architecture guards and demonstrate a failing negative test before acceptance. Do not weaken the existing Host zero-authority rule to wire persistence; use module-owned composition and inward-facing ports.

## Build & test
```bash
dotnet build FaraTaraz.sln
dotnet test FaraTaraz.sln
```
Both must pass before committing.

## Git
One coherent change per commit. Do not force push. If `origin` is not
`https://github.com/mrnikiemami-code/farataraz`, stop and report before pushing.

## Bounded agent execution (owner policy, 2026-10-09)
- Each implementation task targets 20–30 minutes and has a **hard 45-minute budget**. Do not begin another phase if the budget is nearly exhausted. Report PARTIAL with an exact handoff rather than claiming completion.
- One task = one narrow, testable responsibility. W3 must be decomposed into independently authorized slices; no monolithic W3 implementation.
- At 30 minutes or when context becomes constrained, stop feature expansion, run focused tests, save a truthful recovery checkpoint and report the next exact action. Never wait for context exhaustion.
- At task end update `docs/planning/RECOVERY.md` with last verified commit, local/remote status, changed files, tests, unfinished work and next task. Do not mark unverified work PASS.
- Full Debug/Release verification is mandatory for accepted code, but avoid repeatedly running full suites after every small edit; run focused tests during development.
- **Composition exception review:** an Application project reference to its own Infrastructure for DI composition must not permit business handlers, domain services or Application feature code to use Infrastructure types. Add precise source-level and graph guards; do not broadly relax dependency rules. If the accepted ADR prohibits the reference itself, reconcile by a separate ADR/structural repair before feature development.

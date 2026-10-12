# AGENTS.md — FaraTaraz / فراتراز

## Mandatory architecture lock — architect directive (2026-10-12)

This section applies to **every implementation agent, coding assistant and future task**, including parallel or resumed sessions. It is a prospective architecture policy, NOT a claim that all executable guards are implemented. Existing Constitution and accepted ADRs remain higher authority; on conflict STOP and report, never silently override. Current `docs/architecture/structure.md` sections 1–9 contain W1-era descriptions and must be reconciled with actual W2/W3 projects before treating them as a complete physical inventory.

### Clean modular structure — mandatory
- Keep a modular monolith. Within each real business module, locate Domain (business model and invariants), Application (use cases, commands, queries, handlers, inward ports), Infrastructure (EF, migrations, repositories, external integrations), and module-owned Composition where those responsibilities exist. Introduce Contracts or Endpoints **only when an actual cross-module/public contract or delivery responsibility exists**; no empty placeholder projects, speculative layers or generic frameworks.
- One feature owner, one authoritative execution path. No duplicate orchestration in Infrastructure, Host, adapters, controllers or workers. Place each file in its owning layer and use a matching namespace; update the canonical physical map and tests whenever adding/moving a project/folder.
- **MediatR belongs to Application**: request/response definitions and IRequestHandler implementations reside in Application feature folders (e.g. `SynchronizeCustomers/Commands`); Domain, BuildingBlocks, Infrastructure repositories and provider adapters never own handlers or MediatR-driven business policy. Delivery dispatches through `ISender`; it must not bypass the Application use case. Pin MediatR to the version/license decision in ADR-008 until separately approved.
- Dependency graph: Domain must not reference Application, Infrastructure, Host or concrete adapters; Application must not use Infrastructure/EF/Npgsql or concrete repositories; Infrastructure implements inward Application/Domain ports; Host only composes module-owned registrations, with zero business authority. If legacy composition requires a controversial project reference, audit it and isolate composition-only usage; do not create an exception that permits feature handlers to depend outward.
- Inter-module calls must use explicitly owned contracts/ports; no cross-module concrete repository/DbContext or direct data ownership bypass. Extract a Contracts project only when needed to break a real dependency or stabilize a real public seam. BuildingBlocks is for truly shared, provider-neutral primitives only, not a dumping ground for module-specific types.
- Keep provider-specific DTOs and SDKs in adapters; tenant authority originates in a trusted delivery/worker boundary, never from caller input. Assert trusted context, confirm DatabaseTenantScope agreement and ownership **before** provider calls or writes; resolve ProviderId from persisted source authority. Preserve atomic transaction + observed-version checkpoint CAS, explicit completion/restart, and fail-closed changed-record handling. No production delivery until its separate security gate is independently accepted.

### Executable guards and proof required
- A structural rule is not considered locked merely because it is documented. Before certifying architecture stabilization, create or update architecture tests that inspect **real assembly/project references, concrete type dependencies and namespace/path ownership**, with representative **negative fixtures that demonstrably fail**. Test MediatR location, Application→Infrastructure prohibition, Domain purity, Host composition-only authority, provider leakage, cross-module ownership, and BuildingBlocks purity.
- Existing lexical scans (including `OwnershipPathAuthorizationGuard`) are **supplemental**: text presence cannot prove admission ordering or result gating. Back security invariants with behavioral negative integration tests using real PostgreSQL, including unauthorized/mismatched tenant, ownership failure, zero provider calls/writes, stale CAS and rollback. Do not claim a regex or a passing test suite alone proves a production trust boundary.
- No weakening, removing, broad allowlisting or bypassing an existing guard to make tests green. Where a guard conflicts with the accepted architecture, present the concrete counterexample and request an architect ruling before changing it. New folder/module patterns must be covered by guards; no unguarded exemptions.
- Every structural change must keep Debug and Release tests green, real PostgreSQL tests where affected, Linux CI green, and `git diff --check` clean. Show before/after dependency evidence and the negative test proving the guard would catch a regression. Keep files cohesive and comply with existing size rules.
- Optimize for **simple developer workflows**, not maximal abstraction. Do not add extra interfaces, generic engines, DI layers, folders or tests without an actual boundary/invariant. A standard new feature should have an obvious path and fail CI on a real architectural violation.

### Execution and ownership discipline
- At the start of EVERY task read this file, Constitution, relevant accepted ADRs, current structure map, `RECOVERY.md` and architect-owned `OPEN-DEFECTS.md`. Check `git status`, current branch and origin/master; preserve uncommitted work and reconcile remote changes without force-push.
- Executor updates `RECOVERY.md` only; **ChatGPT architect alone** records independent acceptance, defects and architecture journal. Never self-certify or close a defect from implementation reports. Existing task `FT-W3-R3-FINAL-COMPLETION-CONTRACT` remains the only issued coding task until it is reconciled; this policy update is **not permission** to start another task or modify its scope.
- Use a bounded task with explicit stop time. At the hard limit STOP and report PARTIAL with a safe checkpoint; do not spend extra time chasing a green result. User priority is faster **end-to-end architecture stabilization**, not shorter instructions or reduced security coverage.
- **Architecture stabilization exit gate:** current module map matches actual code, MediatR and composition are correctly located, dependency graph and ownership are enforceable, representative forbidden changes fail tests/CI, a normal feature addition follows a straightforward pattern, and architect independently accepts evidence. Until then, do not claim the architecture is permanently locked.

---


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

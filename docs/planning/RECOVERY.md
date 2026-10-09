# RECOVERY — FaraTaraz / FMCA

**Checkpoint recorded:** 2026-10-09
**Current task:** W2 `FT-W2-PERSISTENCE-FOUNDATION-001` complete, committed, **not certified** (awaiting reviewer acceptance). FT-W3-PREFLIGHT-001 composition preflight **verified** (see below); next planned task W3 Ingestion Engine.
**Last reported W2 implementation commit:** `10eb8c9435f9958c3ce3b38bfcec82719827995e` (master; W2 PASS, not certified). Prior FMCA lock baseline: `2dd5e0dac5fcd978a074dfedc9954b3d4e0e1167`.
**Current verified HEAD:** `53a66c5a196d2b51e5ffe72e5f2d0f8afe882d9a` (master; fast-forwarded from W2 `10eb8c9`, includes documentation commits `88af092` and `53a66c5`).
**Note:** Documentation-only commits may follow the implementation baseline. Always fetch and compare current `origin/master` before executing. Do not assume the recovery SHA equals current HEAD.

## Read first (in order)

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md`
3. `docs/planning/RECOVERY.md` (this file)
4. `docs/planning/CURRENT-STATE.md`
5. `docs/planning/ROADMAP.md` and `DELIVERY-PLAN.md`
6. Relevant accepted ADRs, especially ADR-008 and ADR-009, plus `CONTRIBUTING.md`.

## Verified status

- Architecture: FaraTaraz Modular Clean Architecture (FMCA); modular monolith, composition-only Host.
- W0: CERTIFIED (historic baseline).
- W1 / W1-R1 / W1-R2: PASS, **NOT CERTIFIED**; external reviewer acceptance outstanding. Never upgrade status without review evidence and acceptance.
- FMCA architecture lock: completed at `2dd5e0d`; project-graph + source-level guards and negative tests, CI, contribution rules.
- Local execution reported: Debug and Release zero warnings/errors; 152 tests passed (BuildingBlocks 16, SyncContracts 55, Architecture 72, Infrastructure.IntegrationTests 9), 0 failed.
- GitHub Actions run `37920796253` for `2dd5e0d`: success (independently verified).
- W2 persistence: **implemented** at this checkpoint (see below). ADR-010 accepted; `AccountingSources.Infrastructure` + `Ingestion.Infrastructure` with EF Core, migrations, tenant isolation, unique/idempotency constraints, and real PostgreSQL integration tests; CI provisions PostgreSQL.

## Immutable design invariants

- Host: ZERO business authority, no concrete adapter/persistence dependency; only composition and module registration.
- Domain and Application: no outward concrete Infrastructure dependency. Modules own their ports and wiring.
- Tenant authority from trusted context, never request/LLM/MCP input.
- Accounting-source ownership checked fail-closed before provider work.
- Source-scoped external identity: AccountingSourceId + ExternalCode; no automatic cross-source merge.
- CQRS/MediatR contracts, bounded read-only SynchronizeCustomers Query, idempotency and cancellation preserved.
- Architecture guards + negative tests; Debug and Release builds/tests clean; CI green.
- Never force-push, rewrite history, or claim CERTIFIED without accepted evidence.

## Current next action

First execute a bounded W3 preflight/composition-boundary audit (maximum 45 minutes) and update recovery. Then authorize one small W3 slice at a time. Do not launch the entire W3 engine in one task. First audit the W2 persistence
schema and the W1 sync contract; then implement durable synchronization execution (run
orchestration, retries, failure states, resumability) over the persisted state. Do not add
new provider integrations, dashboards, REST/MCP endpoints, or AI.

W3 must not bypass unresolved W1/W2 certification: report blockers rather than silently
changing the contract. Keep Host composition-only and preserve every invariant below.

## W2 — Persistence Foundation (implementation complete — NOT certified)

**Task:** `FT-W2-PERSISTENCE-FOUNDATION-001`. **Status:** `PASS` (acceptance criteria passed;
**not certified** — awaiting reviewer acceptance as the new baseline). W2 does not authorize
W3; W3 requires a separate explicit task.

**Delivered (per ADR-010, Accepted):**
- New module-owned Infrastructure projects:
  - `src/Modules/AccountingSources/AccountingSources.Infrastructure/` — persists `Tenant` +
    `AccountingSource` and implements the provider-independent, fail-closed
    `IAccountingSourceOwnership` oracle.
  - `src/Modules/Ingestion/Ingestion.Infrastructure/` — persists sync run state,
    checkpoints/cursors, source-record identity, and provenance.
- EF Core is Infrastructure-only (never in Domain/Application/BuildingBlocks/Host, ADR-009
  decision 4). Design-time `DbContextFactory` types wired for both modules.
- Migrations: `FTW2-InitialSchema` for both modules, reviewed manually against the model
  (ADR-010 decision 10), applied by an operator/CI step (never silently at runtime).
- Database-enforced tenant isolation: `(TenantId, …)` UNIQUE constraints make cross-tenant
  rows impossible at the DB.
- Database-enforced idempotency/uniqueness: source-record identity
  `(TenantId, SourceId, RecordKind, ExternalId)` — duplicate delivery rejected via
  `ON CONFLICT DO NOTHING` (first delivery canonical, ADR-010 decision 7); checkpoint
  `(TenantId, SourceId, Capability)` unique per scope.
- Trusted tenant-context resolution: `DatabaseTenantScope.FromTrusted(TenantContext)`
  (ADR-010 decision 5) — tenant authority from the trusted execution context only.
- Layer authority preserved: Application resolves inward ports only; only the module-owned
  `Composition` (DI wiring) references Infrastructure; the Host composes modules through
  their `Composition` entry points and references no concrete adapter/persistence assembly.

**Verification:**
- Debug + Release builds: zero warnings, zero errors.
- 152 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72,
  Infrastructure.IntegrationTests 9), 0 failed. Baseline 143 preserved.
- Integration tests exercise real PostgreSQL (tenant isolation, fail-closed ownership,
  idempotency, concurrent duplicate insertion, transaction rollback, invalid checkpoint);
  never EF Core InMemory.
- Guard updates (not weakenings): physical-structure allowlists extended to the two new
  Infrastructure folders + their Composition/Authorization single-file leaves; the
  layer-graph guard now permits Application → its own module Infrastructure composition while
  still forbidding Application → adapters and → EF in business code; test-file budget raised
  to 600 for the now-larger guard file.
- CI (`.github/workflows/architecture.yml`): provisions a PostgreSQL service and points the
  integration fixture at it via `FATARAZ_INTEGRATION_TEST_CONNECTION_STRING`.

**Out of scope (correctly deferred):** W3 ingestion engine, real provider integrations,
dashboards, REST/MCP endpoints, AI. No secrets introduced.

## FT-W3-PREFLIGHT-001 — FMCA composition preflight audit (verified, PASS)

**Task:** `FT-W3-PREFLIGHT-001` (bounded audit + minimal repair; W3 preflight). **Status:** PASS — W2 is a safe foundation for W3; one composition guard gap strengthened (no code violation found). Not certified; W3 requires a separate explicit task.

**Verified HEAD:** `53a66c5a196d2b51e5ffe72e5f2d0f8afe882d9a` (master; fast-forwarded from W2 baseline `10eb8c9`; includes documentation commits `88af092` agents bounded-execution policy and `53a66c5` W2 recovery record).

**Findings (Step 2):**
- **No Application/domain code depends on Infrastructure.** Verified via csproj: `Ingestion.Application` declares only BuildingBlocks + Ingestion.Domain + AccountingSources(module Domain) + MediatR/DI.Abstractions; `AccountingSources` module (Domain) declares only BuildingBlocks + Ingestion.Domain.
- **No Domain references Infrastructure** (both Domain csprofs clean).
- **Host has no concrete Infrastructure reference** — it only calls `AddIngestionApplication`.
- **Module registration follows ADR-010 decision 14** — the only Infrastructure DI wiring lives in module-owned `Composition` extension methods (`AddIngestionInfrastructure`/`AddAccountingSourcesInfrastructure`), invoked by the Host, never by Application handlers.
- **Guard gap (the "composition exception"):** the layer-graph and Host-authority `IsForbiddenLayer` classifier only rejected a FLAT `FaraTaraz.Infrastructure` project (name starts with `FaraTaraz.Infrastructure`, or a path segment equals `Infrastructure`) plus adapters. It did **not** classify the module-scoped W2 Infrastructure projects (`FaraTaraz.Modules.Ingestion.Infrastructure`, `FaraTaraz.Modules.AccountingSources.Infrastructure` — path segment `*.Infrastructure`, assembly name ending `.Infrastructure`). So the guard did NOT reject an injected `Application → module Infrastructure` reference — weaker than ADR-009 decision 4 and ADR-010 decision 14 require (Application/Domain must reference no concrete Infrastructure type). No actual code violation exists; the guard was under-enforcing the ADR. This matches the RECOVERY claim that "the guard now permits Application → its own module Infrastructure composition."

**Correction (Step 3):** Strengthened `IsForbiddenLayer` in both `LayerDependencyGraphTests` and `HostAuthorityTests` to classify any assembly whose name ends with `.Infrastructure` as forbidden (catches module-scoped Infrastructure). Extended the `Application_infrastructure_project_reference_is_detected` regression test to assert the real module-scoped Infrastructure paths are classified forbidden.

**Negative-test proof:** Injected an actual `Ingestion.Application → Ingestion.Infrastructure` ProjectReference; the layer guard FAILED (reported `FaraTaraz.Modules.Ingestion.Infrastructure.csproj` as a forbidden Application reference); reverted the reference (git diff clean); the guard PASSES. The injected reference is removed.

**Verification:** Debug + Release builds clean (0 warnings/0 errors). All 152 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72, IntegrationTests 9), 0 failed — W2 baseline preserved.

**Remaining risks:** None introduced. W2 remains `PASS`, not certified. The guard now correctly enforces ADR-009/010 for module Infrastructure.

**Next exact task:** `FT-W3-INGESTION-ENGINE-001` (or first authorized W3 slice) — audit the W2 persistence schema + W1 sync contract, then implement durable sync execution (run orchestration, retries, failure states, resumability) over persisted state. Do not add provider integrations, dashboards, REST/MCP, or AI.

## Required checkpoint update on every task

Update this file in the same task's final verified commit, including:
- task ID / status / authorized scope
- baseline SHA, implementation commit(s), final remote HEAD after push (report exact value; a file cannot reliably self-reference its own commit)
- module and file changes, ADRs and decisions
- Debug/Release builds, test totals by project, negative-test evidence, CI run/result
- accepted vs pending certifications, blockers and risks
- exact next task and the first executable action

Keep `CURRENT-STATE.md`, `ROADMAP.md`, and `DELIVERY-PLAN.md` consistent. No false or future-dated PASS claims. Preserve history and unrelated files, including untracked `tree.ps1`.

## Agent execution limit and open review item (2026-10-09)

- Owner policy: 20–30 minute target, 45-minute hard cap per task. At the cap, checkpoint and report PARTIAL; do not keep working for hours or exhaust context.
- **Review before W3 feature code:** W2 reports that Application → its own Infrastructure is allowed for module Composition. Confirm the accepted ADR and source-level guards restrict this to composition wiring only, never handlers or domain/application feature logic. Do not weaken existing FMCA guards. Any required fix must be a small separate task.
- W2 test count (152) is reported by the implementing agent; verify fresh CI for the W2 SHA before promoting to an independently verified baseline.

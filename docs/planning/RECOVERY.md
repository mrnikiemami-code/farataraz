# RECOVERY — FaraTaraz / FMCA

**Checkpoint recorded:** 2026-10-09
**Current task:** FT-W2-PERSISTENCE-FOUNDATION-001 — implementation complete, committed, **not certified** (awaiting reviewer acceptance); next planned task W3 Ingestion Engine.
**Last verified implementation baseline:** `2dd5e0dac5fcd978a074dfedc9954b3d4e0e1167` (master, architecture lock).
**Note:** Documentation-only commits may follow this baseline. Always fetch and compare current `origin/master` before executing. Do not assume this checkpoint SHA is current HEAD.

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

Execute a separately authorized W3 Ingestion Engine task. First audit the W2 persistence
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

## Required checkpoint update on every task

Update this file in the same task's final verified commit, including:
- task ID / status / authorized scope
- baseline SHA, implementation commit(s), final remote HEAD after push (report exact value; a file cannot reliably self-reference its own commit)
- module and file changes, ADRs and decisions
- Debug/Release builds, test totals by project, negative-test evidence, CI run/result
- accepted vs pending certifications, blockers and risks
- exact next task and the first executable action

Keep `CURRENT-STATE.md`, `ROADMAP.md`, and `DELIVERY-PLAN.md` consistent. No false or future-dated PASS claims. Preserve history and unrelated files, including untracked `tree.ps1`.

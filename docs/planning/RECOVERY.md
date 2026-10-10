# RECOVERY — FaraTaraz / FMCA

**Checkpoint recorded:** 2026-10-10
**Current task (architect override, 2026-10-10):** FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT — READ-ONLY production ownership/DI/persistence-caller audit; target 10–15 minutes, stop by 20. FT-W2-R2-B-SOURCE-OWNERSHIP produced no implementation; prior handler inspection is recorded in ARCHITECT-JOURNAL.md and must not be repeated. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.
**Last reported W2 implementation commit:** `10eb8c9435f9958c3ce3b38bfcec82719827995e` (master; W2 PASS, not certified). Prior FMCA lock baseline: `2dd5e0dac5fcd978a074dfedc9954b3d4e0e1167`.
**Last architect-observed remote HEAD (before this recovery documentation update):** `bfa4c8c89085bdbb9525b28a5dc499bb3c59a206`; only journal/ledger commits followed implementer HEAD `a31aa254`. Always fetch latest master; this is not implementation acceptance.
**Note:** Documentation-only commits may follow the implementation baseline. Always fetch and compare current `origin/master` before executing. Do not assume the recovery SHA equals current HEAD.

**Implementer handoff — FT-W2-R2-B2-OWNERSHIP-GUARD (2026-10-10):** Added a durable architecture guard `tests/FaraTaraz.ArchitectureTests/OwnershipPathAuthorizationGuard.cs`: scans production `src/**/*.cs` (excluding `tests/` and build output) and flags any call to the ownership-write primitives `SyncRunRepository.StartAsync` / `SourceRecordRepository.InsertOrUpdateAsync` made outside the `Ingestion.Infrastructure` layer. Comments/strings are stripped before matching, so a comment cannot satisfy the guard. GREEN on the shipped source (no forbidden caller); RED proven via a synthetic Application-layer caller; comment/string-stripping proof included. All 75 ArchitectureTests pass (72 prior + 3 new); no regressions. csproj reverted to original (no dependency changes). FT-SEC-007 remains **OPEN** (guard is acceptance *evidence*, not certification); W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Pending: independent ChatGPT architect review of the guard.

**Architect continuity journal:** [`docs/planning/ARCHITECT-JOURNAL.md`](ARCHITECT-JOURNAL.md) — ChatGPT-owned durable log of reports received, independent source/CI checks, architectural decisions, exact GitHub commits, blockers and next steps. On every new chat or context recovery, ChatGPT must read this journal together with RECOVERY and OPEN-DEFECTS, independently reconcile GitHub state, and update the journal itself after each substantive review. Implementers do not own or certify this journal.

**Open defects / acceptance ledger:** [`docs/planning/OPEN-DEFECTS.md`](OPEN-DEFECTS.md) — mandatory read before any W2/W3 task. All unresolved security, DI, transaction, CI, guard, and documentation defects remain tracked there; W2 is NOT CERTIFIED and W3 implementation is not authorized until explicit acceptance. Do not interpret older PASS or 'safe foundation' reports below as current architectural acceptance.

**ARCHITECT RECOVERY / OWNERSHIP LOCK:** ChatGPT, acting as the FaraTaraz independent architecture reviewer, is the **accountable owner and maintainer** of `docs/planning/OPEN-DEFECTS.md`. OpenCode/Cursor are implementers and **cannot certify fixes**. For each reported fix, ChatGPT must fetch and inspect actual GitHub files/diffs, relevant negative guard/test evidence and GitHub CI; **ChatGPT itself must update the defect ledger on `master`**, preserving historical findings and marking VERIFIED only with independent evidence. Missing evidence means OPEN or FIXED — UNVERIFIED. Before any W2 acceptance or W3 authorization, review and reconcile every blocking item. On chat recovery, first read this section and the ledger and resume the same responsibility. Updates occur when a review is requested/performed; no autonomous background GitHub monitoring is implied.

**Current acceptance override:** W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Earlier statements in this file describing W2-R1 as “verified”, database-enforced isolation through uniqueness, or W2 as a “safe foundation” are historical implementation reports, **not current independent architectural approval**. Current statuses live in `OPEN-DEFECTS.md`.

## Read first (in order)

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md`
3. `docs/planning/RECOVERY.md` (this file) and `docs/planning/OPEN-DEFECTS.md`
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

Execute `FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT` as specified in the latest ARCHITECT-JOURNAL.md entry: production ownership implementation/DI and all run-start/record-write callers, read-only, no repeated handler audit. CI remains red (run 38001093552: container initialization failed, build/tests skipped). All open defects remain; no W3 authorization.

## Historical next-action report — superseded, NOT authorization

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

## FT-W2-R1-TENANT-ISOLATION — security repair (verified, PASS)

**Task:** `FT-W2-R1-TENANT-ISOLATION` (CRITICAL security repair; W2 DB-security acceptance was SUSPENDED). **Status:** PASS — the three Ingestion repositories now derive tenant authority only from the trusted <c>DatabaseTenantScope</c>, not a caller-supplied <c>TenantId</c>. Not certified; W2 remains not-certified pending external acceptance. **Did not implement W3 or any business feature.**

**Verified HEAD (base):** `53a66c5a196d2b51e5ffe72e5f2d0f8afe882d9a` (master; W2 + W3 preflight).

**Defects repaired (each was trusting a caller-supplied <c>TenantId</c> as authorization):**
- **SyncRunRepository** — <c>StartAsync</c> wrote a run using the caller tenant; <c>FinishAsync</c> located a run by <c>runId</c> alone (no tenant scope). Now: resolves the trusted tenant, requires a valid trusted scope, scopes the run lookup/modification to that tenant, and rejects cross-tenant finishes (a run owned by another tenant is never modified).
- **SourceRecordRepository** — <c>ExistsAsync</c>/<c>InsertOrUpdateAsync</c> used the caller tenant directly. Now: requires the trusted scope, rejects a caller tenant that does not match the trusted tenant, and scopes all SQL to the trusted tenant. The <c>ON CONFLICT DO NOTHING</c> idempotency policy (ADR-010 decision 7) is **unchanged**.
- **SyncCheckpointRepository** — <c>GetAsync</c>/<c>SaveAsync</c> trusted the caller tenant. Now: requires the trusted scope, rejects cross-tenant requests, and fails closed when the scope is absent. Concurrency/cursor progression unchanged.
- **DI (both module Composition files)** — the trusted scope was a hardcoded default that could silently hide a missing context. Replaced with a single context-aware registration: resolves a <c>TenantContext</c> when one is bound (trusted), otherwise fails closed to <c>DatabaseTenantScope.None</c>. No duplicate default registration; a missing scope never becomes authorized. FMCA boundaries preserved.

**Reference:** the repair mirrors the already-correct <c>EfAccountingSourceOwnership</c> oracle (injects <c>DatabaseTenantScope</c>, ignores the caller tenant, scopes to the trusted tenant, fails closed).

**Architecture restrictions honored:** no Host→Infrastructure, no Application→Infrastructure, no EF Core in Domain/Application, no guard removed/weakened, no new endpoints/providers/dashboards/W3 features.

**Mandatory PostgreSQL negative tests** (`tests/.../TenantIsolationNegativeTests.cs`, 6 tests, real PostgreSQL): (1) Tenant A cannot finish Tenant B's run (run stays <c>Running</c>); (2) Tenant A cannot read Tenant B's checkpoint (denied + scoped read returns null); (3) Tenant A cannot overwrite Tenant B's checkpoint (cursor unchanged); (4) Tenant A cannot read/insert source records as Tenant B (no row created); (5) missing trusted scope denies every operation (all six repository operations throw); (6) correct same-tenant operations still succeed. These fail against the original vulnerable code and pass after the repair.

**Verification:**
- Debug build: zero warnings, zero errors.
- Release build: zero warnings, zero errors.
- Full suite: 158 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72, Infrastructure.IntegrationTests 15), 0 failed. Baseline 152 preserved; +6 negative tests.
- `git diff --check`: clean (no whitespace errors).
- Architecture guards unchanged and passing (72).

**Files changed:** `SyncRunRepository.cs`, `SourceRecordRepository.cs`, `SyncCheckpointRepository.cs` (trusted-tenant guard + caller-tenant validation), `IngestionInfrastructureComposition.cs` + `AccountingSourcesInfrastructureComposition.cs` (context-aware trusted scope), `IngestionPersistenceTests.cs` (construct repositories with a trusted scope), `TenantIsolationNegativeTests.cs` (new).

**Next exact task:** `FT-W3-INGESTION-ENGINE-001` (or first authorized W3 slice) — audit the W2 persistence schema + W1 sync contract, then implement durable sync execution over persisted state. Do not add provider integrations, dashboards, REST/MCP, or AI.

## FT-W2-R3-CHECKPOINT-CONCURRENCY — concurrency repair (implemented, PASS locally, NOT certified)

**Task:** `FT-W2-R3-CHECKPOINT-CONCURRENCY` (bounded W3 preflight slice; FT-DATA-001). **Status:** `PASS` locally — **NOT CERTIFIED**; awaiting independent ChatGPT architect review. **Did not implement W3 or any business feature; did not mark FT-DATA-001 VERIFIED.** W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Verified HEAD (base):** `54c03cd1765d580d2db7c1f8c3519a1cc998c760` (master; current remote HEAD).

**Defect (FT-DATA-001):** `SyncCheckpointRepository.SaveAsync` used read-then-write `SaveChanges`: (1) concurrent first writers both `Add` → unique-constraint violation → crash; (2) a concurrent/stale writer could overwrite a newer checkpoint with stale progress (lost update). The defect asks for an atomic/concurrency-safe checkpoint protocol aligned with the accepted ADR.

**Invariant (now enforced, DB-backed):** a checkpoint write is an atomic, DB-enforced optimistic-concurrency conditional upsert. A writer that observed an older version is rejected (never overwrites a newer checkpoint with stale progress). The ordering is **commit order** via a DB-managed monotonic `Version` column — not the opaque cursor token (not lexically ordered) and not `UpdatedAtUtc` (wall-clock, skew-prone).

**Design decision (PENDING architect confirmation / ADR-010 amendment):** added a DB-managed monotonic `Version` column and made `SaveAsync` a single `INSERT ... ON CONFLICT (TenantId,SourceId,Capability) DO UPDATE SET cursor, updated_at, version = new WHERE version = @expected`. Accepted only when the row's current version equals the version the writer last observed; otherwise 0 rows → throw `StaleCheckpointException` (re-read and retry). First write inserts `version = 0`; a concurrent first writer conflicts and is rejected (never crashes, never silently discarded). This EXTENDS ADR-010 decision 8 (which covers the unique-constraint first-writer race) to also cover the lost-update case the ADR did not address — flagging for reviewer acceptance rather than assuming the ordering semantics.

**Files changed:** `SyncCheckpointEntity.cs` (adds `Version`), `SyncCheckpointRepository.cs` (`SaveAsync` → atomic conditional upsert + `StaleCheckpointException`), `IngestionDbContext.cs` (configures `Version`), `IngestionDbContextModelSnapshot.cs`, new migration `20261010035925_FTW2-CheckpointConcurrencyVersion.{cs,Designer.cs}` (adds `SyncCheckpoints.Version bigint NOT NULL DEFAULT 0`), new test `SyncCheckpointConcurrencyTests.cs` (3 tests). `StaleCheckpointException.cs` new. No dependency/DI/adapter/Host changes.

**Verification (red demonstrated, then restored to green):**
- RED (temporary revert of `SaveAsync` to read-then-write `SaveChanges`): `Concurrent_writes_do_not_crash` FAILS with `DbUpdateException: duplicate key value violates unique constraint "UQ_SyncCheckpoints_Tenant_Source_Capability"` (concurrent first writers crash); `Sequential_writes_advance_db_managed_version_monotonically` FAILS (version stays `0`, no guard).
- GREEN (fix restored): all 3 new concurrency tests pass; full `FaraTaraz.Infrastructure.IntegrationTests` = **24/24 pass** (was 21); `FaraTaraz.ArchitectureTests` = **75/75 pass**; full-solution Debug build = 0 warnings / 0 errors; `dotnet ef migrations has-pending-model-changes` = "No changes have been made to the model since the last migration" (migration/snapshot consistent).

**Architecture restrictions honored:** no Host→Infrastructure, no Application→Infrastructure, no EF Core in Domain/Application, no guard removed/weakened, no new endpoints/providers/dashboards/W3 features. `Version` is DB-managed (never trusted from the caller).

**Next exact task:** independent ChatGPT architect review of commit `f373bd45df647c317307224d4e99999e5a9b7efd` (guard + green CI); then reconcile FT-DATA-001 in `OPEN-DEFECTS.md`. Do NOT promote W2 to certified or authorize W3.

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

## Context-exhaustion recovery protocol — mandatory (2026-10-09)

**Incident:** OpenCode local Ornith 1.5 35B-A3B session compacted approximately 130.1K tokens into 1.8K, continued from stale W2/W3 state and then stopped without a task result. The prior task **FT-W2-R2-BOUNDARY-GUARDS** was interrupted; its status is **UNKNOWN / PARTIAL**, never PASS. Any local uncommitted edits or test results must be inspected before resuming. The latest architect-owned defect ledger is `docs/planning/OPEN-DEFECTS.md`.

**Architect-owned recovery rule:** ChatGPT independently reviews code and updates the defect ledger on master; implementing agents must not mark issues VERIFIED. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Do not trust compacted chat summaries as repository truth.

**Task slicing:** target 10–20 minutes for local-context-constrained agents; **maximum 25 minutes per microtask**, with the existing absolute 45-minute emergency cap retained. Each task has one outcome and at most 1–3 related production files. Avoid full-repository scans, verbose shell dumps, repeated builds, and full test suites inside every microtask. During implementation run only the smallest relevant targeted tests and, when needed, one targeted build. Full Debug/Release + complete suite + CI are a **separate final verification gate**, not skipped for acceptance. Do not start the next microtask automatically.

**Before work / new session:** `git status --short`, `git branch --show-current`, `git rev-parse HEAD`, `git fetch origin master`, `git rev-parse origin/master`; inspect the diff and determine whether the previous task left uncommitted changes. Never reset, clean, stash, checkout over, or overwrite user edits without authorization. Read `AGENTS.md`, this section, `OPEN-DEFECTS.md`, current-state and the relevant ADR. Reconcile any SHA mismatch. The first resumed task is **RECOVERY-ONLY**, not implementation.

**Checkpoint BEFORE context pressure:** record active task ID, last verified SHA, exact changed files, current dirty diff, completed steps, failed/unfinished steps, focused test commands and results, open defect IDs, next single command, elapsed time and status `PARTIAL` in `RECOVERY.md` (or an explicit per-task handoff file if working tree cannot safely be committed). Do this no later than 15 minutes into a microtask and again before expensive tool calls. Keep shell output bounded; prefer targeted file slices and brief test summaries. If compaction occurs unexpectedly, STOP writes and re-read checkpoint/Git before continuing.

**Resume safety:** Never infer a successful command from an incomplete terminal response. If a shell is stalled, do not blindly rerun potentially mutating commands; first inspect process status and filesystem/Git state. Recovery task must report what was preserved, what is uncertain, and the next bounded microtask. Implementer reports evidence only; ChatGPT decides defect closure and updates ledger.

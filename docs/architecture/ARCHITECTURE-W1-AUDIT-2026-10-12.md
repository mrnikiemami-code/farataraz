# FaraTaraz — Wave 1 Architecture Truth Audit (G0 evidence + G1 proposal)

**Task:** `FT-REBASELINE-W1-ARCHITECTURE-TRUTH-001` (READ-ONLY ARCHITECTURE AUDIT)
**Status:** Audit complete — submitted for independent architect review. **Not an accepted ADR and not implementation authorization.**
**Baseline:** `7ca9a3c` (origin/master). Implementation baseline under audit: `8d8b75b616bdb10e8708178c16c0f5975e333476` (314/314 Debug + Release, Linux CI 38248596386). Later commits are architect documentation only.
**Scope:** `src/`, `tests/`, constitution, ADR-001–011, `AGENTS.md`, `structure.md`. **No production code, tests, migrations, project moves or deletions were made.**
**Related:** `docs/architecture/ARCHITECTURE-REBASELINE-2026-10-12.md` (architect design baseline), `docs/planning/OPEN-DEFECTS.md`, `docs/planning/ARCHITECT-JOURNAL.md`.

> Method: every claim below was read from the actual files at the baseline SHA. Where a claim is an inference or is not independently provable, it is marked. Line numbers are from the baseline.

---

## 1. Actual architecture inventory (G0)

### 1.1 Production projects (12) — path, assembly, namespaces, layer, declared dependencies, DI

| # | Project file (under `src/`) | Assembly / namespace root | Layer | Declared `ProjectReference` | Packages | DI registration |
|---|---|---|---|---|---|---|
| 1 | `BuildingBlocks/FaraTaraz.BuildingBlocks.csproj` | `FaraTaraz.BuildingBlocks` (`.Accounting`, `.Application`, `.Configuration`, `.Diagnostics`, `.Errors`, `.Identifiers`, `.Tenancy`) | Foundation | *(none)* | `Microsoft.Extensions.Logging.Abstractions` 8.0.0 | none (types only); grants `InternalsVisibleTo` to 3 **test** assemblies |
| 2 | `Modules/MasterData/FaraTaraz.Modules.MasterData.csproj` | `FaraTaraz.Modules.MasterData` | Domain (shared canonical) | BuildingBlocks | none | none |
| 3 | `Modules/AccountingSources/AccountingSources.Domain/FaraTaraz.Modules.AccountingSources.csproj` | `FaraTaraz.Modules.AccountingSources` (`.Providers`, `.Capabilities`, `.Authorization`) | Domain | BuildingBlocks, Ingestion.Contracts | none | none |
| 4 | `Modules/AccountingSources/AccountingSources.Infrastructure/FaraTaraz.Modules.AccountingSources.Infrastructure.csproj` | `…AccountingSources.Infrastructure` (`.Authorization`, `.Composition`, `.Migrations`, `.Persistence`) | Infrastructure | BuildingBlocks, AccountingSources.Domain | EF Core 9.0.1, EF.Design 9.0.1, Npgsql.EFCore.PostgreSQL 9.0.4 | `AccountingSourcesDbContext`, `DatabaseTenantScope` (TryAdd), `IAccountingSourceOwnership`→`EfAccountingSourceOwnership`, `IAccountingSourceProviderResolver`→`EfAccountingSourceProviderResolver` |
| 5 | `Modules/AccountingSources/AccountingSources.Composition/FaraTaraz.Modules.AccountingSources.Composition.csproj` | `…AccountingSources.Composition` | Composition | AccountingSources.Domain, AccountingSources.Infrastructure | DI.Abstractions 9.0.1 | `AddAccountingSourcesModule` |
| 6 | `Modules/Ingestion/Ingestion.Contracts/FaraTaraz.Modules.Ingestion.Contracts.csproj` | `…Ingestion.Contracts` (`.Synchronization`, `.SourceModel`) | Contracts | BuildingBlocks, MasterData | none | none |
| 7 | `Modules/Ingestion/Ingestion.Domain/FaraTaraz.Modules.Ingestion.Domain.csproj` | `…Ingestion.Domain.Synchronization` | Domain | BuildingBlocks, MasterData | none | none |
| 8 | `Modules/Ingestion/Ingestion.Application/FaraTaraz.Modules.Ingestion.Application.csproj` | `…Ingestion.Application` (`.Composition`, `.Ports`, `.SynchronizeCustomers.Commands`, `.SynchronizeCustomers.Queries`) | Application | BuildingBlocks, Ingestion.Domain, Ingestion.Contracts, AccountingSources.Domain | MediatR 12.5.0, MediatR.Contracts 2.0.1, DI.Abstractions 8.0.0 | `AddIngestionApplication` → MediatR handler assembly scan |
| 9 | `Modules/Ingestion/Ingestion.Infrastructure/FaraTaraz.Modules.Ingestion.Infrastructure.csproj` | `…Ingestion.Infrastructure` (`.Composition`, `.Migrations`, `.Persistence`) | Infrastructure | BuildingBlocks, Ingestion.Domain, Ingestion.Contracts, AccountingSources.Domain, Ingestion.Application | EF Core 9.0.1, EF.Design 9.0.1, Npgsql.EFCore.PostgreSQL 9.0.4 | `IngestionDbContext`, `DatabaseTenantScope` (TryAdd), `SourceRecordRepository`, `SyncCheckpointRepository`, `SyncRunRepository`, `IngestionUnitOfWork`, `EfIngestionWriteStore` + `IIngestionWriteStore`/`IIngestionCheckpointStore`/`IIngestionTrustedTenantScope` |
| 10 | `Modules/Ingestion/Ingestion.Composition/FaraTaraz.Modules.Ingestion.Composition.csproj` | `…Ingestion.Composition` | Composition | Ingestion.Application, Ingestion.Infrastructure | DI.Abstractions 9.0.1 | `AddIngestionModule` |
| 11 | `Host/FaraTaraz.Host/FaraTaraz.Host.csproj` | `FaraTaraz.Host.Composition` | Host | BuildingBlocks, MasterData, AccountingSources.Domain, Ingestion.Domain, Ingestion.Contracts, Ingestion.Application, Ingestion.Composition, AccountingSources.Composition | none | `ConfigureFaraTaraz` → calls the two module seams. **`OutputType` is the default library: no `Program.cs`, no executable. Not a runnable host.** |
| 12 | `Adapters/Accounting.Mock/FaraTaraz.Adapters.Accounting.Mock.csproj` | `FaraTaraz.Adapters.Accounting.Mock` | Adapter | BuildingBlocks, MasterData, Ingestion.Domain, Ingestion.Contracts, AccountingSources.Domain | none | **none in production** — `MockAccountingProvider`/`ISyncablePort<SourceCustomer>` is registered only in `tests/…/CqrsTestSupport.cs:46` |

**Observations from the inventory**
- The declared graph is **acyclic** and matches the intent: foundation ← domains/contracts ← application ← infrastructure ← composition ← host; adapters are outermost.
- Only two layers have persistence realizations: `Ingestion.Infrastructure`, `AccountingSources.Infrastructure`. There is **no `Endpoints` and no executable `Host`**.
- `Ingestion.Contracts` is referenced by Domain (`AccountingSources.Domain`), Application, Infrastructure, Host and Adapter — it is a genuine boundary type surface, but it now also owns the failure taxonomy (see D3).
- `AccountingSources.Domain`'s assembly/namespace is **bare** (`FaraTaraz.Modules.AccountingSources`) while its folder is `AccountingSources.Domain` (see D7).

### 1.2 Test inventory (retention candidates)

| Project | Files | Role |
|---|---|---|
| `tests/FaraTaraz.Infrastructure.IntegrationTests` | 13 files, **85 real-PostgreSQL** tests | tenant isolation, fail-closed scope, idempotency, checkpoint CAS/concurrency, UoW rollback, provider resolver, both sync handlers, DB fail-closed |
| `tests/FaraTaraz.SyncContracts.Tests` | 12 files, **55** tests | W1 sync contract conformance against `MockAccountingProvider` (paging, cursors, idempotency, transient/permanent failure, cancellation, interruption) |
| `tests/FaraTaraz.ArchitectureTests` | 32 files, **157** tests | executable project-graph policy, negative on-disk graph fixtures, host authority, tenant boundary source guards, CQRS placement, provider leakage, file-size budget |
| `tests/FaraTaraz.BuildingBlocks.Tests` | 5 files, **17** tests | ids, tenant, diagnostics/error primitives, capability flags |

### 1.3 Migrations

- `AccountingSources.Infrastructure/Migrations/20261009180140_FTW2-InitialSchema` (+ snapshot).
- `Ingestion.Infrastructure/Migrations/`: `20261009180146_FTW2-InitialSchema`, `20261010035925_FTW2-CheckpointConcurrencyVersion`, `20261012103502_FTW2R3-CheckpointState` (+ snapshots).

---

## 2. Decision trace of both customer-synchronization handlers

### 2.1 `SynchronizeCustomersHandler` (Query) — `SynchronizeCustomers/Queries/SynchronizeCustomersHandler.cs`
1. `command.Tenant.AssertTrusted()` (L45) — admission.
2. `_ownership.IsOwnedByAsync(...)` fail-closed → `UnauthorizedSourceException` (L51–60).
3. `_customers.SyncAsync(request)` — **exactly one bounded page** returned to caller (L63).
4. batch-size upper bound → `SyncProviderException(Permanent)` (L69–75).
5. **No persistence, no cursor advance, no run, no checkpoint.** Pure read/preview of one page.

### 2.2 `RunCustomerSyncHandler` (Command) — `SynchronizeCustomers/Commands/RunCustomerSyncHandler.cs`
1. `command.Tenant.AssertTrusted()` (L64).
2. trusted-scope agreement: `_trustedTenant.TrustedTenantId` must equal `command.Tenant.TenantId` else `UnauthorizedTenantException` (L68–74).
3. `_ownership.IsOwnedByAsync` fail-closed (L77–86).
4. `_providerResolver.ResolveProviderAsync` from **persisted source configuration**; absent/blank → fail-closed (L91–100).
5. restart/cursor rule: `Restart` must not carry a cursor (L106–109).
6. checkpoint read (L111–113); terminal rule: `Completed` rejected unless `Restart`; `Active` → resumable; absent → first sync (L117–132).
7. resume reconciliation `ResolveResumeRequest` (L228–252): validates caller cursor via `SyncCursors.ValidateResume`, rejects a caller cursor that conflicts with the persisted one.
8. one bounded page (L136); page-consistency check `IsComplete ⇔ NextCursor==null` (L148–153).
9. one transaction (`_writes.ExecuteInTransactionAsync`, L156): start run → per record probe → classify duplicate vs changed (`UnsupportedChangedRecordException`, L165–178) → insert with provenance (L180–188) → checkpoint `Active`/`Completed` with **observed-version CAS** (L196–212) → finish run (L215–217).

### 2.3 DI resolvability of the write path

| Dependency of `RunCustomerSyncHandler` | Registered in production? | Where |
|---|---|---|
| `IIngestionWriteStore` | yes | `IngestionInfrastructureComposition` |
| `IIngestionCheckpointStore` | yes | `IngestionInfrastructureComposition` |
| `IIngestionTrustedTenantScope` | yes | `IngestionInfrastructureComposition` |
| `IAccountingSourceOwnership` | yes | `AccountingSourcesInfrastructureComposition` |
| `IAccountingSourceProviderResolver` | yes | `AccountingSourcesInfrastructureComposition` |
| `ISyncablePort<SourceCustomer>` | **NO** | only `tests/…/CqrsTestSupport.cs:46` |
| trusted `TenantContext` (input) | **NO producer** | `TenantContext` ctor/factory are `internal`; friends are test-only |

**Conclusion:** in the composed production container `RunCustomerSyncHandler` cannot be resolved (missing capability port) and could not obtain a trusted tenant even if it were. The path is designed and unit/integration-tested, but **not wired.**

---

## 3. D1–D12 evidence matrix (independent verification)

Classification vocabulary: **CONFIRMED** (verified in code), **PARTIAL** (part outstanding/ambiguous), **FALSE** (disproved), **UNVERIFIED** (cannot be proven from source alone).

| ID | Verdict | Independent evidence | Disposition / needed action |
|---|---|---|---|
| **D1** No production delivery/trusted producer | **CONFIRMED** | `FaraTaraz.Host.csproj` is a library, no `Program.cs`; only `Host/Composition/FaraTarazHost.cs` exists; no `Endpoints`/worker project under `src/`; `ISyncablePort<SourceCustomer>` registered only in `CqrsTestSupport.cs:46`; `TenantContext.cs:53,67` internal + `FaraTaraz.BuildingBlocks.csproj:27-29` friends are test-only. | Design real delivery + authority + provider registration before code (aligns with architect). Not architectural corruption — an unimplemented W4 gate. |
| **D2** Thin Domain; handler owns rules | **CONFIRMED (thin Domain) / PARTIAL (which rules are pure)** | `Ingestion.Domain` = 4 files (~76 lines); `RunCustomerSyncHandler` = 220 lines holding restart/cursor/terminality/duplicate-changed/page-consistency decisions (L106–213). | Extract **only** pure invariants (C5, C6, C8, C10, C11 in §4) with tests; leave admission/CAS/transaction where they are. See W2.2. |
| **D3** Failure taxonomy in Contracts | **CONFIRMED (relocation) + ADR ambiguity** | `Ingestion.Contracts/Synchronization/SyncFailure.cs` owns `SyncFailureCategory`, `SyncProviderException`, `SyncTransientException`, `SyncPermanentException`, `SyncModeNotSupportedException`, `InvalidSyncCursorException`, `SyncCursorScopeMismatchException`. ADR-010 §11 (L93–96) says the taxonomy "remains a **Domain** contract that Infrastructure records, not decides". | Semantic preserved, **assembly relocated without an ADR amendment**. Architect must clarify ADR §11 wording (assembly vs semantic) or split enum-only into Contracts. **Do not change until decided (AD-1).** |
| **D4** Raw SQL duplicates EF schema | **CONFIRMED (duplication) / PARTIAL (risk)** | `SyncCheckpointRepository.cs:120-134` and `SourceRecordRepository.cs:123-135` hand-write INSERT/`ON CONFLICT` with literal column names; the same schema is declared in `IngestionDbContext.OnModelCreating` (L43–100) and migrations. | Real duplication; **runtime** drift is caught only by the 85 PG integration tests (they exercise the SQL after migration), not at compile time. Add a schema-assertion/drift test only if a real gap is found. Not a rewrite trigger. |
| **D5** Two handlers / dead `IApplicationUseCase` | **CONFIRMED** | Query handler is **non-persisting** (no writes) → it is a legitimate read-only preview, **not** a duplicate writer. Both handlers repeat the admission sequence (AssertTrusted → ownership → batch bound) — `SynchronizeCustomersHandler.cs:45-75` vs `RunCustomerSyncHandler.cs:64-143`. `IApplicationUseCase` (`BuildingBlocks/Application/IApplicationUseCase.cs`) has **zero** production consumers. | Keep Command as the single write path; decide whether to retire the Query or keep it as a documented preview with a shared admission helper; remove the dead marker (W3). |
| **D6** Multiple capability representations | **CONFIRMED** | Four encodings: `AccountingCapability` flags (`BuildingBlocks/Accounting/AccountingCapability.cs`), `SupportedCapabilities` (`IAccountingProvider.cs:15`), 6 typed ports (`CapabilitySourcePorts.cs`), and a 12-branch `typeof` chain (`ProviderCapabilityExtensions.cs:37-49`). Adding one capability touches ≥5 files in platform core (§4.4). | Prefer a single capability↔port mapping (e.g. each typed port exposes its `AccountingCapability`) only if a real addition proves the cost; do not over-abstract speculatively. |
| **D7** `AccountingSources` bare assembly/namespace | **CONFIRMED** | Folder `AccountingSources.Domain/`, project `FaraTaraz.Modules.AccountingSources.csproj`, namespace `FaraTaraz.Modules.AccountingSources*`; `PhysicalStructureTests` maps the folder to the bare namespace; my inventory-wide `ArchitecturePolicy.Classify` needs a bare-name heuristic because of it. | Known compatibility exception. **No cosmetic rename** without a cost/benefit decision (AD-5). |
| **D8** Dead `Clock`/diagnostics/error primitives | **CONFIRMED** | `ISystemClock`/`SystemClock` (`Ingestion.Domain/Synchronization/Clock.cs`) have **zero** consumers — Application uses `DateTime.UtcNow` directly (handler L181,199,211). `DiagnosticContext`/`DiagnosticsLoggingExtensions`/`ErrorDescriptor`/`ErrorCode`/`ErrorCategory`/`ErrorSeverity` are used **only** by `FaraTaraz.BuildingBlocks.Tests`. | Remove or connect individually (AD-4); inject a clock only where time is an invariant (e.g. provenance/CAS readability). |
| **D9** String run-state | **CONFIRMED** | `SyncRunState` string constants (`SyncRunRepository.cs:134-140`); outcome enum→string mapping (`EfIngestionWriteStore.cs:196-203`); `mode.ToString()` (L140); checkpoint mapped via `SyncCheckpointStates` strings (`EfIngestionWriteStore.cs:107-112`); migrations persist `State` as `varchar(32)`. | Lower-priority; requires a persisted-value mapping before a typed change (W5 optional). |
| **D10** MasterData records only | **PARTIAL — no proven defect** | `MasterData.cs` = 4 records; `CanonicalCustomer`/`CanonicalProduct` used only in `ExternalIdentityTests`. No canonical mapping service, no enforced invariant beyond the record shape. | Forward scaffolding, not a demonstrated violated invariant. Do not invent aggregates (AD/architect agrees). |
| **D11** Stale assembly anchors / fictitious path | **CONFIRMED** | `PhysicalStructureTests.cs` `Ingestion_Domain_does_not_reference_MediatR` / `…_does_not_reference_Ingestion_Application` / `MediatR_handlers_live_only_in_the_Application_layer` all use `typeof(SyncRequest).Assembly`, which is now **Contracts**, so `Ingestion.Domain` is not actually asserted. `LayerDependencyGraphTests.cs:70` uses `src/Infrastructure/FaraTaraz.Infrastructure.csproj`, which **does not exist**. `structure.md` §5 also describes the query handler as depending only on the port + `TenantContext` (it also depends on `IAccountingSourceOwnership`). | Fix anchors to the real Domain assembly and remove the fictitious path (W3.3). |
| **D12** Parallel error models | **CONFIRMED** | Six genuinely distinct boundaries exist (see §5.4): platform/domain errors, provider-failure taxonomy, persisted run outcome, infrastructure CAS signal, domain completion/change invariants, and a **dead** BuildingBlocks error model. | Map boundaries; delete the dead model or wire it. Not a blanket consolidation (AD-4). |

**Summary:** D1, D3(relocation), D4(duplication), D5, D6, D7, D8, D9, D11, D12 **CONFIRMED**; D2 **CONFIRMED-thin / PARTIAL-pure**; D10 **PARTIAL — not proven**. **No finding rises to "irreconcilable ownership/cycles"; no finding is FALSE.**

---

## 4. Business-rule ownership matrix (classification of every decision)

Legend — **DOM** = Domain invariant, **APP** = Application orchestration/admission, **INF** = Infrastructure enforcement, **CON** = Contract shape, **DEL** = Delivery.

### 4.1 Command handler (`RunCustomerSyncHandler`)

| # | Decision | Current | Correct | Note |
|---|---|---|---|---|
| C1 | `AssertTrusted` before any work | APP (L64) | DEL→APP invitation, guarded in APP | correct (Constitution A.4/G.27) |
| C2 | trusted-scope agreement | APP (L68–74) | APP | correct |
| C3 | ownership fail-closed | APP (L77–86) | APP invoking INF port | correct (A.46) |
| C4 | provider provenance from persisted config | APP (L91–100) | APP + INF resolver | correct (M5) |
| C5 | explicit restart must not carry a cursor | APP (L106–109) + adapter | **DOM** | also in `MockCapabilitySync` — duplicate |
| C6 | `Completed` terminal; only `Restart` passes | APP (L117–132) | **DOM** (checkpoint state semantics) | eligibility ⇒ W2.2 |
| C7 | `Active` ⇒ resumable, absent ⇒ first sync | APP (L128–132) | APP + DOM state semantics | eligibility ⇒ W2.2 |
| C8 | caller cursor must match persisted cursor | APP (L244–249) + `SyncCursors.ValidateResume` | **DOM** | duplicated logic |
| C9 | batch-size upper bound | APP (L138–143) | APP boundary | duplicated with Query L69–75 |
| C10 | page consistency `IsComplete ⇔ NextCursor==null` | APP (L148–153) | **CON/DOM** | already enforced by `SyncBatch` by construction — defensive duplicate |
| C11 | duplicate vs changed classification | APP (L165–178) | **DOM** | core idempotency/freshness rule (ADR-005/E.20) |
| C12 | provenance construction | APP (L180–181) | APP (type is DOM `SourceProvenance`) | time via `DateTime.UtcNow`, not a clock |
| C13 | checkpoint state transition + observed-version CAS | APP (L196–212) | APP composes; **INF** enforces CAS; **DOM** owns state meaning | correct split |
| C14 | run start/finish + outcome | APP (L158–217) | APP; INF persists | correct |
| C15 | single transaction boundary | INF (`ExecuteInTransactionAsync`) | INF | correct (FT-DATA-002) |

### 4.2 Query handler (`SynchronizeCustomersHandler`)

| # | Decision | Current | Correct | Note |
|---|---|---|---|---|
| Q1 | `AssertTrusted` | APP (L45) | APP | correct |
| Q2 | ownership fail-closed | APP (L51–60) | APP | correct |
| Q3 | one bounded page, no persistence | APP (L63) | APP read-only | **no duplicate write authority** |
| Q4 | batch-size upper bound | APP (L69–75) | APP | duplicated with C9 |

### 4.3 Infrastructure

| # | Decision | Location | Correct? |
|---|---|---|---|
| I1 | resolve `DatabaseTenantScope` from trusted context | both `*InfrastructureComposition` | correct (fail-closed to `None`) |
| I2 | `RequireTrustedTenant` fail-closed | all 3 repositories + `EfIngestionWriteStore` | correct |
| I3 | `AssertCallerTenant` defense-in-depth | repositories | correct (caller tenant is validated, never trusted) |
| I4 | idempotency via unique key + `ON CONFLICT DO NOTHING` | `SourceRecordRepository.cs:123-144` | correct (E.20) |
| I5 | checkpoint CAS upsert | `SyncCheckpointRepository.cs:120-152` | correct (FT-DATA-001) |
| I6 | outcome enum → string mapping | `EfIngestionWriteStore.cs:196-203` | acceptable; single source of truth preferred (D9) |
| I7 | raw SQL literal columns | both repos | **deviation D4** |
| I8 | resolver bare `catch` (OCE rethrown) | `EfAccountingSourceProviderResolver.cs:56-65` | correct |

### 4.4 Touchpoints to add one capability (D6 evidence)

Adding e.g. "Warehouse": (1) `AccountingCapability` flags; (2) `SourceWarehouseRecord` in Contracts; (3) `IWarehouseSource : ISyncablePort<SourceWarehouseRecord>`; (4) `KnownCapabilities` mask **and** two `typeof` branches in `ProviderCapabilityExtensions`; (5) adapter implementation; (6) tests. **Five core touchpoints**, four of them duplication of the same fact.

---

## 5. Execution and trust-boundary map (with missing edges)

### 5.1 Intended chain (as designed)

```
[Delivery: HTTP/Worker/MCP]
  → establish TenantContext from an authenticated principal / trusted job claim
    → ISender.Send(command/query)
      → Application handler (admission → provider port → inbound ports)
        → Infrastructure (unit of work, tenant scope, persistence)
          → PostgreSQL
```

### 5.2 Actual chain at baseline

```
[Delivery]                  ✗ MISSING — no Endpoints/worker project; FaraTaraz.Host is a class library
   │
IAuthenticatedPrincipal     ✗ MISSING — ADR-011 Proposed; no port, no implementation
   │
Establishment seam          ✗ MISSING — minting is internal to BuildingBlocks; friends are test-only
   │
TenantContext (trusted)     ⚠ type exists; NEVER produced or registered in production
   │                          (compositions only TryAdd DatabaseTenantScope reading GetService<TenantContext>())
   ▼
ISender                     ✗ no caller (no delivery surface)
   │
RunCustomerSyncCommand      ✓ type
   │
RunCustomerSyncHandler      ✓ implemented (220 lines) — but UNRESOLVABLE in the composed container
   ├─ IIngestionTrustedTenantScope     ✓ registered
   ├─ IAccountingSourceOwnership       ✓ registered
   ├─ IAccountingSourceProviderResolver✓ registered
   ├─ IIngestionWriteStore/Checkpoint  ✓ registered
   └─ ISyncablePort<SourceCustomer>    ✗ NOT registered anywhere in production
        └─ IngestionUnitOfWork → IngestionDbContext → PostgreSQL ✓ (migrations exist, 3)
```

### 5.3 Missing / unresolved edges

| Edge | Status | Consequence |
|---|---|---|
| E1 delivery entry point (HTTP/worker/MCP) | missing | no runtime path; `ISender` never called |
| E2 authenticated-principal producer / establishment seam | missing (ADR-011 Proposed) | no trusted `TenantContext` can exist in production |
| E3 `TenantContext` produced/registered in production | missing | `DatabaseTenantScope` is always `None` → every tenant-scoped op fails closed |
| E4 `ISyncablePort<SourceCustomer>` registered in production | missing (tests only) | handler not resolvable even if tenant existed |
| E5 provider adapter registered in production | missing | no provider available |
| E6 background scheduler/worker | missing | W3 background execution (Constitution A.5) absent |
| E7 DB role/RLS least privilege | missing (constraints only) | tenant isolation relies on scoped queries + unique constraints, not DB roles |

### 5.4 Error/failure boundary inventory (D12)

1. **Platform/domain errors:** `UnauthorizedTenantException`, `UnauthorizedSourceException`, `CapabilityNotSupportedException`, `ConnectionConfigurationException`, `InvalidSourceRecordIdException`, `InvalidSyncRequestException`.
2. **Provider-failure taxonomy:** `SyncProviderException` + `SyncTransientException`/`SyncPermanentException`/`SyncModeNotSupportedException`/`InvalidSyncCursorException`/`SyncCursorScopeMismatchException`.
3. **Persisted run outcome:** `IngestionRunOutcome` ↔ `SyncRunState` strings.
4. **Infrastructure CAS signal:** `StaleCheckpointException`.
5. **Domain completion/change invariants:** `SyncAlreadyCompletedException`, `UnsupportedChangedRecordException`.
6. **Dead BuildingBlocks model:** `ErrorDescriptor`/`ErrorCode`/`ErrorCategory`/`ErrorSeverity` (no production consumer).

---

## 6. Constitution / ADR reconciliation (contradictions flagged, not amended)

| # | Document | Says | Code says | Conflict |
|---|---|---|---|---|
| R1 | `structure.md` §1 layout | 6-project tree (no `Ingestion.Contracts`, no `*.Infrastructure`, no `*.Composition`) | 12 projects incl. Contracts + 2 Infrastructure + 2 Composition | **stale** — doc §1–§3 describe the W1 baseline |
| R2 | `structure.md` §2 ownership | `Ingestion.Domain` owns "the sync contract: `SyncRequest`, `SyncBatch`, `SyncCursor`, …" | those types are in `Ingestion.Contracts` | **stale** |
| R3 | `structure.md` §2 | `BuildingBlocks` owns "the `IApplicationUseCase` marker" | marker exists but is **dead** | stale + dead abstraction (D5/D8) |
| R4 | `structure.md` §5 | query handler depends "only on the capability port + `TenantContext`" | it also depends on `IAccountingSourceOwnership` | **stale** |
| R5 | `structure.md` §4 dependency list | omits `Ingestion.Composition`, `AccountingSources.Composition`, and all `*.Infrastructure` edges | real graph has them | **stale** |
| R6 | ADR-010 §11 | "failure taxonomy remains a **Domain** contract" | taxonomy is in `Ingestion.Contracts` | **assembly relocated**; needs wording decision (D3) |
| R7 | `structure.md` §2 | `Ingestion.Domain` "never owns" MediatR/use cases/persistence; and §3 maps `Ingestion.Domain/Synchronization` | consistent | no conflict |
| R8 | ADR-009 status | "`Contracts`/`Endpoints`/`Infrastructure` waves remain pending explicit authorization" | a `Contracts` project and two `Infrastructure` projects now exist | ADR-009 historical status is **behind reality**; needs a documentation pass, not a silent ADR change |
| R9 | Constitution L.44 | "handler depends only on the inward capability port and the trusted tenant context — never on `ISender`" | consistent (handlers do not take `ISender`) | no conflict |
| R10 | Constitution A.4/A.5 | tenant authority only from trusted context; no static/global | consistent; production producer absent | consistent, gate open (D1) |

**Only R1–R8 are documentation/decision drifts. None requires changing a normative accepted ADR without an explicit architect decision (AD-1, AD-6, AD-8).**

---

## 7. Reuse vs full rewrite decision

### 7.1 Decision: **REUSE the repository; perform a bounded controlled reconstruction.** Do not rewrite from scratch.

### 7.2 Comparison

| Dimension | Controlled reconstruction (recommended) | Full rewrite |
|---|---|---|
| Change scope | Move pure domain policy (≈3 files), fix 2 guards + 1 doc section, retire 1 dead marker, optionally connect a clock; add delivery/auth/provider in W4 | Re-author every module, contracts, persistence, composition and test |
| Reusable, independently verified assets | 85 real-PG integration tests; 157 architecture tests incl. real negative fixtures; idempotency/CAS/tenant-isolation DB semantics; module composition seams; migration schema | **Discarded** — must be re-earned |
| Compatibility risk | Low: no public API rename required; `AccountingSources` bare-name exception retained | High: re-derives identity/provenance/CAS contracts already accepted |
| Regression risk | Low–medium (domain-policy moves must be parity-tested with PG) | High (all invariants re-implemented; no parity baseline) |
| Effort | 11.5–21 h over W2–W5 (W4 conditional) | Weeks; no cost advantage measurable |
| Meets rewrite triggers (rebaseline §45) | Not needed | Requires (a) irreconcilable ownership/cycles, (b) untestable invariants, (c) measured lower cost, (d) migration/parity plan. **None established.** |

### 7.3 Rewrite triggers — explicitly evaluated and **not met**

- (a) Irreconcilable ownership/dependency cycles → **no**: the declared graph is acyclic and machine-enforced.
- (b) Systemic invariants untestable without replacing foundations → **no**: tenant/idempotency/CAS/rollback are proven against real PostgreSQL.
- (c) Measured rewrite cost lower than repair → **no evidence**; repair is local.
- (d) A migration/parity plan exists → only repair has one (existing green suite as parity baseline).

### 7.4 Conditions that would flip the decision (documented for the architect)

- If W2 domain extraction reveals that the ingestion invariants are **inseparable from orchestration** (no pure policy exists), reconsider.
- If W4 authentication requires abandoning the current tenant-scope model (e.g. DB-per-tenant) such that migrations/constraints cannot be preserved.
- If a genuine cross-module cycle is discovered that the current graph policy cannot express.

---

## 8. Proposed target architecture (G1)

### 8.1 Target project graph (keep the shape; grow Domain, defer Endpoints)

```
src/
├── BuildingBlocks/                 foundation (targets: remove dead primitives; add ADR-011 principal port + establishment seam)
├── Modules/
│   ├── MasterData/                 external + canonical identity (unchanged)
│   ├── AccountingSources/
│   │   ├── AccountingSources.Domain/          provider declaration, capability ports, ownership ports
│   │   ├── AccountingSources.Infrastructure/  ownership + provider resolver + persistence
│   │   └── AccountingSources.Composition/     module seam
│   └── Ingestion/
│       ├── Ingestion.Contracts/    transport-neutral sync surface + source models (possible taxonomy split — AD-1)
│       ├── Ingestion.Domain/       sync contract state semantics + pure ingestion invariants (+tests)
│       ├── Ingestion.Application/  one authoritative write use case + read-only preview + inbound ports
│       ├── Ingestion.Infrastructure/ persistence, UoW, CAS, migrations
│       ├── Ingestion.Composition/  module seam
│       └── Ingestion.Endpoints/    ⏳ W4 only, and only with a real executable host + authentication
├── Adapters/<Provider>/            concrete adapters (outermost)
└── Host/                           composition root; becomes an executable delivery host only in W4
```

No new speculative project is proposed. No `TrustedExecution` assembly is proposed (ADR-011 places the seam in `BuildingBlocks`).

### 8.2 Responsibility ownership matrix (target)

| Project | Owns | Never owns |
|---|---|---|
| `BuildingBlocks` | ids, `Tenant`/`AccountingSource`/`TenantContext`, capability flags, (ADR-011) `IAuthenticatedPrincipal` port + establishment seam | any domain rule, capability port, provider concept, dead primitives |
| `MasterData` | external + canonical identity records | sync execution, provider ports, persistence |
| `AccountingSources.Domain` | `IAccountingProvider`, `ISyncablePort<T>` + typed capability ports, ownership/provider-resolver ports | concrete providers, persistence, use cases |
| `AccountingSources.Infrastructure` | ownership oracle, provider resolver, tenant source persistence | business decisions, provider SDKs |
| `Ingestion.Contracts` | transport-neutral sync types + source models (+ taxonomy per AD-1) | business policy, MediatR, EF, persistence |
| `Ingestion.Domain` | checkpoint/sync **state semantics** and the pure ingestion invariants (terminality, restart/cursor conflict, duplicate/changed classification) | MediatR, ports, persistence, tenant authority, time/transport |
| `Ingestion.Application` | admission sequencing, one write use case, read-only preview, inbound port definitions, MediatR | EF/Npgsql, concrete repos, adapters, `ISender` self-dispatch |
| `Ingestion.Infrastructure` | persistence, UoW, CAS, migrations, port implementations | business policy |
| `Ingestion.Composition` | inward DI wiring | business rules |
| `Host` | composition + process start (when executable) | business handlers, domain rules, repositories, tenant decisions |
| Adapters | provider DTO→source-model mapping + port implementation | anything outside the adapter boundary |

---

## 9. Finite W2–W5 reconstruction plan (dependency-ordered)

Estimates are engineering hours; they are **not** deadlines and **not** authorization to auto-start. Each wave stops at its gate for architect review.

### W2 — Domain & Contracts (2–4 h)
- **W2.0 (gate, architect):** resolve AD-1 (ADR-010 §11 wording; keep vs split taxonomy). Blocks W2.1.
- **W2.1:** extract the pure ingestion invariants from `RunCustomerSyncHandler` into `Ingestion.Domain` (C5, C6, C8, C10, C11) as explicit domain types/services with **unit tests**; the handler becomes orchestration that *invokes* them. No behavior change.
- **W2.2:** add focused tests for duplicate/changed classification incl. the `null`-fingerprint case.
- **Exit:** Domain owns the invariants; handler delegates; Debug+Release green; PG parity unchanged (85 integration tests still pass **unmodified**).
- **Rollback:** revert W2 commits (no schema change; no migration).
- **Risk:** behavior drift — mitigated by keeping the existing PG tests unchanged as the parity baseline.

### W3 — Application & simplification (2–3 h)
- **W3.1:** one authoritative **write** path (Command). Decide the Query: keep as a documented read-only preview OR retire it; if kept, extract a single shared admission helper so the sequence exists once.
- **W3.2:** remove `IApplicationUseCase` (verified zero production consumers) or wire it; do not leave a dead marker.
- **W3.3 (D11):** re-point the two Domain-labelled tests to the real `Ingestion.Domain` assembly; remove/replace the fictitious `src/Infrastructure/FaraTaraz.Infrastructure.csproj` path in `LayerDependencyGraphTests`; correct `structure.md` §5.
- **Exit:** one write path, no dead abstraction, guards target real assemblies, green.
- **Rollback:** revert; tests + small Application refactor only.

### W4 — Trusted production delivery (4–7 h, **conditional**)
- **W4.1:** implement the ADR-011 seam (`IAuthenticatedPrincipal` port + `BuildingBlocks.Tenancy` establishment API reusing the internal factory + scoped `TenantContext` composition), with extended `TenantBoundaryGuards` negative proofs (fake trusted principal ⇒ trusted; missing/unauthenticated ⇒ fail-closed).
- **W4.2:** a **real** executable delivery (`Host` executable or `src/Host/FaraTaraz.Api` / Worker) dispatching via `ISender`, with a **real** authentication adapter — only if the user supplies an actual mechanism.
- **W4.3:** register the provider adapter through a module/adapter composition seam.
- **Exit:** end-to-end fail-closed: untrusted / not-owned / unknown-source ⇒ **zero provider calls and zero writes**; owned ⇒ exactly one bounded step — verified against real PostgreSQL.
- **Rollback:** feature-flag the delivery; the auth/provider registrations are additive; remove the executable without touching the domain/persistence layers.
- **External dependencies (must be supplied, never invented):** real authentication/identity mechanism or an approved background-job authority; a real provider (or an explicit "mock-only, non-production" decision).

### W5 — End-to-end hardening & acceptance (2–4 h)
- Behavioral negative suite against the real delivery: cross-tenant, unowned, stale CAS, rollback mid-step, changed record, restart/completion, cancellation.
- Cross-platform Debug+Release CI green at the exact final SHA.
- Documentation consistency pass (`structure.md` §§1–9; ADR-009 historical status) — **documentation-only**, architect-owned.
- Independent architect acceptance at the exact SHA.
- **Exit:** exact-SHA CI green; ADR-011 accepted; every production gate closed **or** explicitly recorded as accepted-risk.

### 9.1 Terminal exit condition (explicit)

Reconstruction is **complete** when either:
- **(A) Full:** W2 and W3 merged with green CI and no behavior change; W4 implemented against a real approved authority + provider and passing end-to-end negative tests; W5 accepted by the architect at an exact SHA; **or**
- **(B) Explicitly blocked end state:** W2/W3 merged; W4 formally BLOCKED on missing external authority/provider, and the repository is left in a documented **fail-closed, non-production** state with the blocked work recorded as external-dependency-blocked (no production activation, no fake authority/provider).

Both are reachable and honest. The plan does **not** require inventing authentication or a provider.

---

## 10. Code and tests to retain unchanged

- **Unchanged, must not be weakened:** `tests/FaraTaraz.Infrastructure.IntegrationTests/**` (85 PG tests), `tests/FaraTaraz.SyncContracts.Tests/**` (55), `tests/FaraTaraz.BuildingBlocks.Tests/**` (17).
- **Retained architecture guards (only D11 anchors corrected):** `ArchitecturePolicy.cs`, `ArchitecturePolicyTests.cs`, `ProjectDependencyGraph.cs`, `SourceTreePaths.cs`, `CompositionSeamPolicy.cs`, `HostAuthorityTests.cs`, `HostCompositionSmokeTests.cs`, `TenantBoundaryGuards.cs`, `TenantTrustSourceTests.cs`, `ProviderLeakageTests.cs`, `SyncContractGuards.cs`, `ReferenceGraphTests.cs`, `SourceFileSizeGuardTests.cs`, `PlanningIntegrityTests.cs`.
- **Retained production:** all of `Ingestion.Infrastructure` persistence + 3 migrations, `AccountingSources.Domain/Infrastructure/Composition`, `BuildingBlocks` (except optional dead-primitive removal), `Adapters/Accounting.Mock`, both module composition seams, `FaraTarazHost` seam aggregation.
- **Retained with edit:** `RunCustomerSyncHandler` (becomes orchestration invoking Domain policy), `Ingestion.Domain` (gains the extracted invariants).

---

## 11. Open architect decisions & external dependencies

| AD | Decision needed | Blocks |
|---|---|---|
| **AD-1** | ADR-010 §11: is "Domain contract" the **assembly** or the **semantic role**? Keep taxonomy in Contracts, or split enum (Contracts) / exceptions+policy (Domain)? | W2.0, D3 |
| **AD-2** | ADR-011 trust model: accept the seam-in-`BuildingBlocks` + `IAuthenticatedPrincipal` shape; and which **concrete** authentication (JWT / connector handshake / background job authority) | W4 |
| **AD-3** | Keep the read-only Query as a documented preview, or retire it? | W3.1 |
| **AD-4** | Remove the dead BuildingBlocks primitives (D8/D12 item 6), or connect them? | W3, W5 |
| **AD-5** | Formally accept D7 (no `AccountingSources` rename) as a recorded exception | W2/W3 |
| **AD-6** | Who owns the `structure.md` §§1–9 + ADR-009 status reconciliation (architect-owned normative docs) | W5 |
| **AD-7** | Scope/timing of typed run-state (D9) | W5 (optional) |
| **AD-8** | Is a raw-SQL/schema drift test (D4) required, or is integration coverage sufficient? | W5 (optional) |

**External dependencies:** real authentication/identity source or background-job authority; a real accounting provider or an explicit mock-only decision; PostgreSQL in CI (already provisioned).

---

## 12. Audit risks and limits

- This audit is **source-level**. It does not execute the suite; totals (314/314) are quoted from the architect-verified baseline CI, not re-run here.
- D2's split between "pure invariant" and "orchestration" is a **judgement**; W2.0/W2.1 must confirm with parity tests before moving code.
- The observed `null`-fingerprint semantics (stored-null vs absent) are covered by `SourceRecordRepository.ProbeAsync` but the **freshness/changed-record policy** (FT-DATA-003) remains an open ADR-level question, unchanged by this audit.
- "No production delivery" is a **gate**, not corruption: it is the expected state before W4.

**No production readiness is claimed. No guard was weakened. No ADR/ledger/journal file was edited.**

---

## 13. Outcome

- **Reuse-vs-rewrite:** **REUSE** (bounded controlled reconstruction). No rewrite trigger established.
- **Architecture verdict:** structurally sound inward layering and verified data-plane behavior; the significant gaps are (i) an unwired delivery/trust boundary (D1), (ii) a thin Domain whose invariants live in the Application handler (D2/D3), (iii) stale documentation/guards (D11/R1–R8), and (iv) dead abstractions (D5/D8/D12-6). All are locally repairable within the existing project graph.
- **Next step:** architect review of this Wave 1 document, then approve **one** target decision set (AD-1…AD-8) and authorize **W2 only**. No code change until then.

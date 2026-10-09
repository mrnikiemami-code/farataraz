# CURRENT STATE — FaraTaraz

**Last updated:** 2026-10-09 — FMCA architecture lock (FT-FMCA-ARCHITECTURE-LOCK-001)
**Operational handoff:** [RECOVERY.md](RECOVERY.md) — mandatory fast-resume checkpoint; implementation baseline `2dd5e0d`
**Certified baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3` (W0 CERTIFIED)

Operational state. Should be readable in under ~2 minutes.

---

## Where are we?

Implementation has continued past W0. **W1 — Synchronization Contracts is `PASS`**
(acceptance criteria passed; not yet certified — no reviewer has accepted it as the new
baseline).

## What is certified?

**W0 — Architecture Foundation.**

Certified baseline: `d499e61730579c2ad9810d306dd2620c986bd9f3`

W0 deliverables:
- architecture constitution
- ADR-001..007
- tenant / source identity
- provider capabilities
- external / canonical identity seam
- architecture tests (all passing)
- Mock provider foundation (partial capabilities)

---

## What is PASS (not yet certified)?

**W1 — Synchronization Contracts.**

Status: `PASS` — acceptance criteria passed. **Not certified**: a reviewer must accept the
W1 evidence before it becomes the baseline.

W1 deliverables:
- provider-independent async sync contract (`ISyncablePort<TRecord>.SyncAsync`)
- bounded, resumable pages (`SyncBatch<TRecord>`), one cursor per page
- `SyncRequest` (source + mode + cursor + batch limit; **no `TenantId`**)
- `SyncCursor` / `SyncCursorScope` + `SyncCursors.ValidateResume`
- `SyncMode` / `SyncModeSupport` (full vs incremental, declared per capability)
- source-record identity (`SourceRecordId` = source + record kind + external id)
- source-record version + deterministic content fingerprint (change signal)
- failure taxonomy (`SyncFailureCategory`: transient / permanent / cancellation)
- cancellation surfaces as `OperationCanceledException`, never a provider failure
- provenance (`SourceProvenance`): retrieval vs provider-modified time
- Mock synchronization scenarios (`MockCapabilitySync`, `MockSources`)
- reusable adapter-agnostic conformance harness + Mock conformance tests
- architecture guards: async ports require `CancellationToken`, source identity shape,
  no persistence packages in BuildingBlocks/modules, `SyncRequest` carries no `TenantId`

W1-R1 (Architecture Structure + CQRS Foundation) — structural refinement of W1 (still
part of W1; W1 remains `PASS`, not certified):
- decomposed the former `Core` project into `BuildingBlocks` (foundation) plus capability
  modules: `MasterData`, `AccountingSources`, `Ingestion.Domain`, `Ingestion.Application`
- capability-first module ownership with an exact path-to-namespace mapping
  (see `../architecture/structure.md`)
- CQRS through MediatR (12.5.0 / Apache-2.0) with `ISender` as the delivery boundary;
  `SynchronizeCustomersQuery` + `SynchronizeCustomersHandler` dispatch over the capability
  port and enforce the trusted `TenantContext` invariant
- structural architecture guards (reference graph, dependency direction, CQRS dispatch,
  no persistence packages, one namespace per folder)

**W1-R2 (Architecture Closure & Certification Readiness)** — closes the remaining W1
architectural defects (still part of W1; W1 remains `PASS`, not certified):
- **Bounded-page CQRS.** `SynchronizeCustomersQuery` now returns a single
  `SyncBatch<SourceCustomer>` page per request; the handler drives exactly one provider
  page and returns it. Pagination is caller-controlled via `SyncBatch.NextCursor`. The
  handler no longer loops pages or accumulates a full in-memory collection (bounded
  Application memory). Cancellation propagates; unsupported modes/capabilities still fail
  explicitly.
- **Tenant-source ownership enforcement.** Provider-independent
  `IAccountingSourceOwnership.IsOwnedByAsync(tenantId, sourceId)` port (fail-closed:
  unknown tenant/source or lookup failure returns `false`, never authorization success).
  The handler asserts the trusted `TenantContext` first, then verifies ownership before any
  provider work; no provider adapter is required and no production always-`true` fake exists.
  Source identity alone never grants authorization (W2/W3 supply the trusted resolution).
- **Physical architecture guards.** Executable guards for exact path↔namespace mapping
  (derived from location, not a file list), capability-first organization (reject generic
  `Commands`/`Queries`/`Handlers`/… top-level folders), single-file leaf allowlist,
  dependency direction (Domain has no MediatR/Application; adapters own no handlers; no
  persistence in W1-R2), and CQRS placement (handlers only in valid Application paths).
- **Sync contract hardening.** `SyncRequest.BatchSize` rejects zero/negative;
  `SourceRecordId` rejects empty/whitespace record kind or external id; `SyncCursor`
  rejects unreasonably large tokens and no longer leaks the raw token via `ToString()`.

Critical acceptance: processing the same source record repeatedly is behaviorally
**idempotent** (stable identity across repeated delivery).

**Test evidence:**
- `FaraTaraz.SyncContracts.Tests` — Mock conformance (paging, cursor continuation,
  incremental declaration, cancellation, batch-size honoring, empty source, unsupported
  capability) + idempotency (same record twice / ten times / repeated batch / changed
  content / different record kind / different source) + interruption & resume + failure
  classification.
- `FaraTaraz.ArchitectureTests` — W1 sync guards (see above); W1-R1 CQRS dispatch +
  untrusted-tenant rejection; W1-R2 bounded-page CQRS (one page per dispatch, cursor
  continuation, no accumulation), tenant-source ownership (all deny/zero-provider-invocation
  cases), and physical structure (path↔namespace, capability-first, single-file leaf,
  dependency direction, CQRS placement).

**Not done in W1 (correctly deferred):** real persistence (W2), ingestion engine (W3),
any real provider integration. No PostgreSQL.

---

## FMCA architecture completion audit (this baseline)

`master` has migrated the FMCA target (`ADR-009-FaraTaraz-Modular-Clean-Architecture.md`,
`structure.md` §10) as far as the baseline responsibilities permit. The table audits every
assembly against the FMCA five-layer target (`Application` / `Contracts` / `Domain` /
`Endpoints` / `Infrastructure`) plus the composition-only `Host`. `COMPLETE` means the layer
owns a real responsibility here; `NOT_APPLICABLE` means no responsibility exists at this
baseline and the layer is deferred **with evidence** — never marked `COMPLETE`. No empty
projects are created (`structure.md` §10.1).

| Module / assembly | Application | Contracts | Domain | Endpoints | Infrastructure |
| --- | --- | --- | --- | --- | --- |
| `BuildingBlocks` (foundation) | — | — | Foundation ids / `Tenant` / `AccountingSource` / `TenantContext` / `AccountingCapability` / `IApplicationUseCase` marker (not a capability module). | — | — |
| `MasterData` | `NOT_APPLICABLE` | `NOT_APPLICABLE` | **COMPLETE** — external (`AccountingSourceId+ExternalCode`) and canonical identity value objects. | — | — |
| `AccountingSources` | `NOT_APPLICABLE` | `NOT_APPLICABLE` | **COMPLETE** — capability ports (`ISyncablePort<T>`), neutral `IAccountingProvider` declaration, ownership oracle. | — | — |
| `Ingestion.Domain` | — | `NOT_APPLICABLE` | **COMPLETE** — sync contract (`SyncRequest`/`SyncBatch`/`SyncCursor`/`SyncMode`), source-record identity/version/fingerprint, source models. | — | — |
| `Ingestion.Application` | **COMPLETE** — `SynchronizeCustomers` in `SynchronizeCustomers/Queries/` (read-only bounded-page sync) + module `Composition`. | `NOT_APPLICABLE` | — | — | — |
| `Accounting.Mock` (adapter) | — | — | — | — | **COMPLETE** — deterministic in-memory provider implementing the capability ports (test/verification only). |
| `Host` | — | — | — | — | Composition root (zero business authority); not an infrastructure layer. |

**Evidence for the `NOT_APPLICABLE` (deferred) entries:**

- **Contracts (separate project) — deferred.** The cross-module contracts (sync contract
  types, capability ports, canonical/external identities) are declared in Domain and are the
  cross-module boundary. No Application request/response contracts or Infrastructure DTOs
  exist to warrant a separate `Contracts` project; extraction is a documented future step.
- **Endpoints — deferred.** No HTTP/MCP delivery surface exists in the baseline; no
  endpoints exist. Implemented when a delivery responsibility is demonstrated.
- **Infrastructure — deferred.** No persistence, external API, or message bus exists in the
  baseline. The only Infrastructure-type assembly is `Accounting.Mock` (a test adapter).
  Persistence is deferred to W2.
- **`MasterData` / `AccountingSources` Application/Endpoints/Infrastructure — deferred.**
  These modules own only Domain-level responsibilities (identity value objects; capability
  ports, provider declaration, ownership oracle). No use cases live here (the one sync use
  case lives in `Ingestion.Application`); there is no delivery or persistence.
- **`Ingestion.Domain` / `Ingestion.Application` Contracts/Endpoints/Infrastructure —
  deferred.** The Domain owns the sync contract and source models (the sync contract types
  also serve as the cross-module contract, see Contracts note). No use cases, delivery, or
  persistence live in either Domain or Application project.

**Verified:** Debug + Release builds zero warnings/errors; **143 tests pass** (16
`BuildingBlocks` + 55 `SyncContracts` + 72 `ArchitectureTests`, the latter including the
Host-zero-authority, feature-first/Command-Query placement, path↔namespace, and — added by
`FT-FMCA-ARCHITECTURE-LOCK-001` — project-graph and layer-boundary guards).

## Architecture lock (FT-FMCA-ARCHITECTURE-LOCK-001)

`master` now enforces the FMCA invariants through **executable guards, CI, and contribution
rules** — so drift fails the build rather than being silently accepted.

### Defense-in-depth guards (`tests/FaraTaraz.ArchitectureTests/`)

Guards assert invariants at **project-reference** *and* **source-code** levels; neither view
alone is sufficient.

- **Host zero authority** — `HostAuthorityTests`. Reflection walks the loaded Host assembly
  and rejects any use-case handler or Application feature type; **project-file guards** read
  the actual `FaraTaraz.Host.csproj` and reject any provider-adapter or infrastructure
  `ProjectReference` and any non-composition `PackageReference`. The declared-project-file
  view is required: the C# compiler emits no metadata reference for an unused-but-declared
  adapter `ProjectReference`, so `Assembly.GetReferencedAssemblies()` cannot see it.
- **Effective dependency graph** — `ProjectDependencyGraph` parses every `.csproj` and builds
  the transitive `ProjectReference` closure; `HostAuthorityTests` and
  `LayerDependencyGraphTests` assert that closure reaches no adapter/infrastructure project
  (Host) or no Application layer (Application).
- **Domain → Application boundary** — `DomainApplicationBoundaryTests`: the Domain declares no
  Application `ProjectReference` and its transitive closure reaches no Application project.
- **CQRS leaves** — `CQRSHandlerGuardTests`: every `IRequestHandler` avoids `ISender` and a
  concrete provider adapter.
- **Physical structure** — `PhysicalStructureTests`, `OrphanSourceFileTests`,
  `SourceFileSizeGuardTests`: path↔namespace, capability-first, single-file-leaf allowlist,
  orphan-file, and 300/500-line budgets (with a regression test that injects an oversized
  file).

**Negative-test evidence** (inject → guard fails for the intended reason → revert):

| Violation | Guard that fires | Result |
| --- | --- | --- |
| Host → concrete adapter | `Host_declares_no_adapter_or_infrastructure_project_reference`, `Host_effective_dependency_graph_has_no_adapter_or_infrastructure` | **DETECTED** (reverted) |
| Host → persistence package | `Host_declares_no_persistence_package` | **DETECTED** (reverted) |
| Domain → Application | `Domain_declares_no_application_project_reference`, `Domain_effective_dependency_graph_has_no_application_project` | **DETECTED** (reverted) |
| Application → concrete Infrastructure | `Application_declares_no_adapter_or_infrastructure_project_reference`, `Application_effective_dependency_graph_has_no_adapter_or_infrastructure` | **DETECTED** (reverted) |
| CQRS handler → ISender | `Application_request_handlers_must_not_receive_ISender` | **DETECTED** (reverted) |
| CQRS handler → concrete adapter | `Application_request_handlers_must_not_depend_on_concrete_providers` | **DETECTED** (reverted) |
| Namespace/path mismatch | `File_declaring_extra_namespace_segment_is_rejected` | **DETECTED** (regression) |
| Oversized source file | `Oversized_source_file_is_detected` | **DETECTED** (regression) |
| Unjustified single-file leaf | `Nested_unjustified_single_file_leaf_is_detected_and_rejected` | **DETECTED** (regression) |
| Orphan production source file | `Orphan_source_file_directly_under_src_is_detected` | **DETECTED** (regression) |

Every listed invariant has a guard that fails on injection; none is weakened to hide
failures.

### CI

[`.github/workflows/architecture.yml`](../.github/workflows/architecture.yml) builds Debug +
Release (zero warnings/errors) and runs all suites on every push/PR to `master`. The
architecture suite **fails the build on any invariant breach**, plus a `git diff --check`
whitespace guard. No force-push; history preserved.

### Contribution rules

[`CONTRIBUTING.md`](../CONTRIBUTING.md) codifies the SoT hierarchy, the invariants to
preserve, the "add a guard when you touch an invariant" rule, the Debug+Release build/test
bar, and the W1 certification governance (`PASS` ≠ `CERTIFIED`; no force-push).

## What is next?

**W2 — Persistence Foundation is NOT authorized by W1.** W1 only establishes contracts and
proves them against the Mock. W2 (PostgreSQL + EF Core) requires a separate explicit task
and an explicit architecture decision to introduce a persistence stack.

## What is blocked?

- Any real Asan analytics work — blocked on the **unverified provider capability**
  discovery gate (see `PRODUCT.md` and `DECISIONS-AND-OPEN-QUESTIONS.md`, OQ-001 / OQ-002).
- W5 (Sales) — blocked on verified read access to historical sales / invoice data.

## What must NOT be started?

- W2 persistence (PostgreSQL) until W1 contracts are accepted AND a separate task
  authorizes it.
- Any real Asan integration until the provider contract is verified.
- AI auto-merge of canonical identities (W4) — deterministic matching only.
- LLM-generated forecasts (W9) — deterministic only.

---

## Quick navigation

- Product: `PRODUCT.md`
- Waves: `ROADMAP.md`
- Execution rules + SoT hierarchy: `DELIVERY-PLAN.md`
- Unresolved questions: `DECISIONS-AND-OPEN-QUESTIONS.md`
- Architecture (authoritative): `../architecture/architecture-constitution.md`

# FaraTaraz — Module Structure

**Status:** Active / Authoritative (supplements `architecture-constitution.md`)
**Applies to:** W1-R1 — Architecture Structure + CQRS Foundation

This document is the canonical description of the physical layout, module ownership,
path-to-namespace mapping, dependency direction, and CQRS layering. It is the reference the
structural architecture guards in
[`../tests/FaraTaraz.ArchitectureTests`](../tests/FaraTaraz.ArchitectureTests) enforce.
Where a file's location or namespace contradicts this document, this document wins — move
the file or rename the namespace to match.

---

## 1. Layout

The repository is a **modular monolith** organized into foundation, module, and adapter
projects. There is no single `Core` project; the former `Core` domain was decomposed into
`BuildingBlocks` plus capability modules.

```
src/
├── BuildingBlocks/          foundation — depends on nothing platform-wide
├── Modules/
│   ├── MasterData/          external + canonical identity
│   ├── AccountingSources/   provider contracts, capability ports, authorization
│   └── Ingestion/
│       ├── Ingestion.Domain/        sync contracts + source models (no MediatR)
│       └── Ingestion.Application/   CQRS use cases (MediatR)
└── Adapters/
    └── Accounting.Mock/     deterministic Mock provider (test/verification only)

tests/
├── FaraTaraz.BuildingBlocks.Tests/
├── FaraTaraz.SyncContracts.Tests/       Mock conformance + CQRS dispatch
└── FaraTaraz.ArchitectureTests/         structural + contract guards
```

---

## 2. Module ownership

| Project | Owns | Never owns |
| --- | --- | --- |
| `BuildingBlocks` | Strong ids, `Tenant`/`AccountingSource`/`TenantContext`, `AccountingCapability`, the `IApplicationUseCase` marker. | Any domain logic, any capability port, any provider concept. |
| `MasterData` | External identities (`AccountingSourceId + ExternalCode`) and FaraTaraz-owned canonical identities. | Sync execution, provider ports, business rules. |
| `Ingestion.Domain` | The sync contract: `SyncRequest`, `SyncBatch`, `SyncCursor`, source-record identity/version/fingerprint, `SyncMode`, source models. | MediatR, use cases, capability ports, persistence. |
| `Ingestion.Application` | Application use cases (CQRS commands/queries) that drive the sync contract to completion over a capability port. | Provider adapters, persistence, tenant authority (takes it in, does not create it). |
| `AccountingSources` | Capability ports (`ICustomerSource`, `IProductSource`, …) and the neutral `IAccountingProvider`. | Concrete providers, sync internals, use cases. |
| `Accounting.Mock` | A deterministic Mock provider that implements the ports for verification. | Anything outside the adapter boundary. |

**Rule:** each module owns exactly one capability domain. A capability port belongs to the
module that declares the contract it synchronizes; the adapter implements that port.

---

## 3. Exact path-to-namespace mapping

Every source folder maps to exactly one fully-qualified namespace. One folder = one
namespace. Do not split a namespace across folders or place two namespaces in one folder.

| Path (under `src/`) | Namespace |
| --- | --- |
| `BuildingBlocks/Accounting/` | `FaraTaraz.BuildingBlocks.Accounting` |
| `BuildingBlocks/Application/` | `FaraTaraz.BuildingBlocks.Application` |
| `BuildingBlocks/Identifiers/` | `FaraTaraz.BuildingBlocks.Identifiers` |
| `BuildingBlocks/Tenancy/` | `FaraTaraz.BuildingBlocks.Tenancy` |
| `BuildingBlocks/Errors/` | `FaraTaraz.BuildingBlocks.Errors` |
| `BuildingBlocks/Diagnostics/` | `FaraTaraz.BuildingBlocks.Diagnostics` |
| `Modules/MasterData/` | `FaraTaraz.Modules.MasterData` |
| `Modules/AccountingSources/Providers/` | `FaraTaraz.Modules.AccountingSources.Providers` |
| `Modules/AccountingSources/Capabilities/` | `FaraTaraz.Modules.AccountingSources.Capabilities` |
| `Modules/AccountingSources/Authorization/` | `FaraTaraz.Modules.AccountingSources.Authorization` |
| `Modules/Ingestion/Ingestion.Domain/SourceModel/` | `FaraTaraz.Modules.Ingestion.Domain.SourceModel` |
| `Modules/Ingestion/Ingestion.Domain/Synchronization/` | `FaraTaraz.Modules.Ingestion.Domain.Synchronization` |
| `Modules/Ingestion/Ingestion.Application/SynchronizeCustomers/` | `FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers` |
| `Adapters/Accounting.Mock/` | `FaraTaraz.Adapters.Accounting.Mock` |

Test namespaces follow the same convention: `FaraTaraz.BuildingBlocks.Tests`,
`FaraTaraz.SyncContracts.Tests`, `FaraTaraz.ArchitectureTests`.

---

## 4. Dependency direction

Dependency direction is **inward**: foundation is at the center, adapters are outermost and
depend inward. No platform module depends on a concrete adapter, and `Core` (now
`BuildingBlocks`) never references an adapter.

```
BuildingBlocks           -> (none)
MasterData               -> BuildingBlocks
Ingestion.Domain         -> BuildingBlocks, MasterData
AccountingSources        -> BuildingBlocks, Ingestion.Domain
Ingestion.Application    -> BuildingBlocks, Ingestion.Domain, AccountingSources
Accounting.Mock (adapter)-> BuildingBlocks, MasterData, Ingestion.Domain, AccountingSources
```

Enforced by `ReferenceGraphTests` (exact per-assembly reference set) and
`DependencyDirectionTests` (no platform module references an adapter).

**Why `Ingestion.Application` depends on `AccountingSources`:** a use case must delegate to
the provider-independent capability port (`ISyncablePort<TRecord>`), which the
`AccountingSources` module declares. **Why `Ingestion.Domain` does not:** the domain layer
declares only the sync contract and source models; it does not know about capability ports.
The split exists precisely so the Application layer can depend on the port without the Domain
layer doing so.

---

## 5. CQRS layering

`Ingestion.Application` is the proof of the W1-R1 CQRS structure. The direction is fixed and
one-way:

```
delivery adapter  →  ISender  →  Application query/command  →  handler  →  capability port
```

- **Query/command** (`SynchronizeCustomersQuery`) is a thin `IRequest<TResponse>` record that
  carries only its inputs: a trusted `TenantContext` and a `SyncRequest`. It contains no
  logic.
- **Handler** (`SynchronizeCustomersHandler`) implements `IRequestHandler<…>`. It depends
  **only** on the inward capability port (`ISyncablePort<SourceCustomer>`) and the inbound
  `TenantContext` — never on a concrete provider, and never on `ISender` itself. It asserts
  the `TenantContext` is trusted (`AssertTrusted()`) before any tenant-scoped work, then
  drives the bounded-page cursor contract to completion.
- **Delivery boundary is `ISender`, not `IMediator`.** Use cases are dispatched through
  `ISender` (resolved from the DI container), never constructed directly and never reached
  through `IMediator`. This keeps the Application layer testable and free of a concrete
  mediator.

**No layer may reach sideways or backwards:** the Domain layer does not reference MediatR or
the Application layer; the capability port does not reference the use case; the adapter
implements the port but never calls the use case.

---

## 6. MediatR baseline (pinned)

`Ingestion.Application` (and `FaraTaraz.ArchitectureTests`) reference **MediatR 12.5.0** and
**MediatR.Contracts 2.0.1**, both **Apache-2.0**. This is a licensed-baseline decision; see
[`adr/ADR-008-MediatR-License.md`](adr/ADR-008-MediatR-License.md). Do not upgrade MediatR
to 13.x/14.x (RPL1.5) without a new explicit architecture and licensing decision.

---

## 7. Structural guards (automated)

`FaraTaraz.ArchitectureTests` enforces this structure:

- `ReferenceGraphTests` — exact per-assembly dependency set for every module and the adapter.
- `DependencyDirectionTests` — no platform module depends on a concrete adapter; foundation
  depends on nothing platform-wide.
- `ProviderLeakageTests` — no provider concept leaks into the platform.
- `SyncContractGuards` — async port shape, source-record identity, no `TenantId` in
  `SyncRequest`, and no persistence packages in `BuildingBlocks` or any module.
- `CQRSProofTests` — the use case is dispatched through `ISender`, delegates to the
  capability port, and rejects an untrusted `TenantContext`.
- `PlanningIntegrityTests` — required docs/ADR files exist.

Any change that alters the reference graph, a namespace, or the CQRS direction must update the
relevant guard.


---

## 8. Source file size budget (automated)

`SourceFileSizeGuardTests` scans all handwritten `.cs` files recursively under
`src/` and `tests/`, including newly added modules. Production files must be
**at most 300 physical lines** and test files **at most 500 physical lines**.
Violations fail the architecture test with the offending path and actual count.
The **250-line review threshold** is advisory, not a passing/failing rule;
reviewers should check cohesion and split independent responsibilities before
a file reaches the hard limit.

Only `bin/`, `obj/`, and C# files carrying an explicit `<auto-generated`
header in the first ten lines are excluded. There is no silent filename-based
allowlist or grandfathering: pre-existing violations must be refactored or
handled through an explicit, separately reviewed architecture decision.

**Line count is a necessary but insufficient guard against God Files.** A file
below the limit can still mix unrelated contracts or responsibilities. Reviews
must separately enforce single responsibility, capability cohesion and
path-to-namespace ownership. Splits must preserve public API behavior and be
verified with full regression tests before merge.

## 9. AccountingSources responsibility folders

`Providers/` owns only `IAccountingProvider`; `Capabilities/` owns the syncable capability abstraction, six typed source ports, and capability resolution; `Authorization/` owns the trusted source ownership oracle and unauthorized-source exception. These are physical disk folders visible inside the SDK-style project in Visual Studio. Every moved type's namespace matches its physical folder. Project-level `Using` items in consumers preserve existing unqualified references without changing the authorization, cancellation, or capability semantics. `Providers/` is a justified single-file leaf because the provider declaration has one responsibility; the architecture allowlist documents this exception.

---

## 10. Target module architecture — Tooba Settlement reference (mandatory for migration)

**Decision (2026-10-09):** FaraTaraz adopts the Tooba Settlement modular architecture as the **target** for all business modules. This section is normative for future implementation and migration; sections 1–9 describe the **current verified baseline** and must not be mistaken for proof that the target already exists. Existing project names and boundaries must be inventoried and migrated deliberately, without breaking behavior.

### 10.1 Physical projects per business module

```text
src/Modules/<Capability>/
  <Capability>.Application/       # use cases and feature-owned Commands/Queries/Models/Ports
  <Capability>.Contracts/         # explicit cross-module and delivery contracts
  <Capability>.Domain/            # business entities, value objects, domain rules
  <Capability>.Endpoints/         # HTTP/MCP delivery, authorization, dispatch only
  <Capability>.Infrastructure/    # persistence, external services, port implementations

tests/<Capability>.Tests/          # module verification (test projects remain under tests/)

src/Host/                         # composition root and wiring ONLY
```

Use consistent FaraTaraz project prefixes, namespaces and solution-folder names; do not blindly copy Tooba assembly names. A module may omit a layer only with an explicit, documented reason. No empty placeholder projects/folders.

### 10.2 Application: capability first, responsibility second

```text
<Capability>.Application/
  Composition/                    # module-local DI registration (no business rules)
  <Feature>/
    Commands/                     # write/side-effect use cases
    Queries/                      # read-only use cases
    Models/                       # feature-owned application models
    Ports/                        # application-owned abstractions
  Validation/                     # genuinely shared module validation
```

Feature-local validation belongs with its feature when not shared. Create only folders with actual contents. Command/query contracts and handlers live under the corresponding feature and operation category. Synchronization that mutates state/cursors is a Command, not a Query, unless a documented behavior audit proves read-only semantics; preserve existing public contracts during migration and use explicit compatibility steps.

### 10.3 Layer authority and dependency direction

- **Domain:** business authority; no dependency on Application, Endpoints, Infrastructure, Host, EF, HTTP or MediatR.
- **Application:** use cases, handlers, validation and inward-facing ports; never references concrete adapters, EF or Host.
- **Contracts:** transport-neutral cross-module/public contracts; no business implementation or infrastructure references.
- **Endpoints:** delivery adapters grouped by audience/capability (e.g. Admin/, Seller/); authenticate/authorize, validate delivery input, translate response, dispatch through `ISender`; no business decisions or persistence.
- **Infrastructure:** implements application/domain ports and owns technical integrations; dependencies point inward.
- **Tests:** validate each layer, module contract and integration boundary.
- **Host:** **ZERO business authority**. May compose module registrations, configure middleware, route endpoints and start the process. Must not contain business handlers, domain rules, repository/provider implementations, business validation or direct tenant-scoped business operations.

A dependency graph must be derived from actual assembly references, then enforced by architecture tests. Do not silently reverse the dependency rule or allow cross-module Application/Infrastructure coupling. Cross-module access uses explicit Contracts/ports. Preserve tenant isolation, source ownership, fail-closed authorization, CQRS and idempotency.

### 10.4 MediatR decision and precision

The currently verified baseline uses MediatR `IRequest<T>`/`IRequestHandler<,>` in Application and `ISender` at delivery. `ISender` is itself a MediatR interface; it is **not** evidence of library independence. Do not silently relocate MediatR to Infrastructure or replace public contracts. Any move to MediatR-independent Application contracts requires a separate ADR, adapter design and tests. Endpoint/Host dispatch must not be confused with handler business logic.

### 10.5 Disk, Visual Studio and migration acceptance

- Real physical directories must match the intended Solution Explorer hierarchy; solution-only virtual folders do not count.
- Use `git mv` for physical moves, update namespaces, project references, solution entries, registrations and tests atomically per module.
- Exact path-to-namespace mapping and dependency-graph guards must be updated to the **actual final** layout, not weakened to hide failures.
- Inventory all `src/` and `tests/` projects and migrate one module at a time with independently reviewable commits.
- Verify solution load, Debug/Release builds (zero warnings/errors), all existing tests and new architecture/Host-authority guards after each wave and at the end.
- Keep `master` unchanged until the migration branch is verified and explicitly approved for merge.
- **Acceptance is not achieved by this documentation commit alone**; physical disk/Visual Studio refactor, regression evidence and module-level migration are required.

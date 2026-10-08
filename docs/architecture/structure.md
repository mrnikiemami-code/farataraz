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
│   ├── MasterData/          five separate external + canonical identity contracts
│   ├── AccountingSources/   provider capability ports
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

**MasterData W1 identity seam:** `ExternalCustomerId.cs`, `ExternalProductId.cs`, `CanonicalId.cs`, `CanonicalCustomer.cs`, and `CanonicalProduct.cs` are separate translation units in the single `MasterData` namespace. There are no empty single-file subfolders. Source-scoped external identities remain distinct from tenant-bound canonical identities. This is a contract-only module at W1; it does not own identity matching, persistence, mapping history, or ID generation. The current record constructors and public equality/deconstruction semantics are intentionally preserved. Default-valued struct IDs and blank inputs remain a pre-W2 validation/design decision and must not be treated as validated IDs at a trust or persistence boundary. The architecture test `MasterDataStructureTests` locks physical cohesion and source/tenant distinctions; it does not claim to prove input validation.\n\n**Rule:** each module owns exactly one capability domain. A capability port belongs to the
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
| `Modules/MasterData/` | `FaraTaraz.Modules.MasterData` |
| `Modules/AccountingSources/` | `FaraTaraz.Modules.AccountingSources` |
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

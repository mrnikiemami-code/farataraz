# ADR-009: Tooba Settlement Modular Architecture (Target)

**Status:** Accepted — target is normative; physical migration wave is *pending explicit wave authorization*
**Date:** 2026-10-09
**Derives from:** `docs/architecture/structure.md` §10 — *"Target module architecture — Tooba Settlement reference (mandatory for migration)"* (adopted on master at `19dab6fc4b877181d0cd0ad385dba9e196f5c886`)
**Depends on:** ADR-001 (Modular Monolith), ADR-007 (Shared Application Authority), ADR-008 (MediatR License Baseline)
**Amends:** ADR-001 (see *Conflicts, compatibility and amendments*)

## Context

FaraTaraz is a multi-tenant, provider-agnostic platform whose code grew into a
capability-first modular monolith (`BuildingBlocks` + capability modules). Section 10 of
`structure.md` makes the **target** explicit: every business module will be organized as a
set of justified, independently owned physical projects following the Tooba Settlement
pattern:

```text
src/Modules/<Capability>/
  <Capability>.Application/       # use cases; feature-owned Commands/Queries/Models/Ports
  <Capability>.Contracts/         # explicit cross-module and delivery contracts
  <Capability>.Domain/            # business entities, value objects, domain rules
  <Capability>.Endpoints/         # HTTP/MCP delivery; authorization, dispatch only
  <Capability>.Infrastructure/    # persistence, external services, port implementations

tests/<Capability>.Tests/          # module verification (test projects stay under tests/)
src/Host/                          # composition root and wiring ONLY
```

Sections 1–9 of `structure.md` describe the **current verified baseline** and must not be
mistaken for proof that the target already exists. This ADR records the accepted target,
states how it interacts with the existing accepted ADRs, and gates the physical migration
behind an explicit, phased wave. **This ADR does not perform the physical migration.**

## Decision

1. **Adopt the Tooba Settlement modular architecture as the target** for all business
   modules, realized as physical projects with the layout above. Project/namespace/
   solution-folder names use consistent FaraTaraz prefixes; Tooba assembly names are not
   copied verbatim (`structure.md` §10.1).
2. **A module may omit a layer only with an explicit, documented reason.** No empty
   placeholder projects or folders are created (`structure.md` §10.1).
3. **Application is capability-first, responsibility-second.** Within
   `<Capability>.Application`, organize by feature, then by `Commands/`, `Queries/`,
   `Models/`, `Ports/`, plus a module-level `Validation/` for genuinely shared validation
   (`structure.md` §10.2). Synchronization that mutates state/cursors is a **Command**,
   not a Query, unless a documented behavior audit proves read-only semantics.
4. **Layer authority and dependency direction are inward.** Domain depends on nothing in
   Application/Endpoints/Infrastructure/Host/EF/HTTP/MediatR; Application references no
   concrete adapter/EF/Host; Contracts are transport-neutral; Endpoints dispatch through
   `ISender` and contain no business decisions or persistence; Infrastructure depends
   inward (`structure.md` §10.3).
5. **Host is the composition root and has ZERO business authority.** It may compose module
   registrations, configure middleware, route endpoints and start the process. It must not
   contain business handlers, domain rules, repository/provider implementations, business
   validation or direct tenant-scoped business operations (`structure.md` §10.3).
6. **Endpoints group delivery surfaces by audience or capability** (e.g. `Admin/`,
   `Seller/`). They authenticate/authorize, validate delivery input, translate responses
   and dispatch through `ISender`; they perform no business calculation (`structure.md`
   §10.3, ADR-007).
7. **MediatR stays in the Application layer.** The verified baseline uses
   `IRequest<T>`/`IRequestHandler<,>` in Application and `ISender` at delivery.
   `ISender` is itself a MediatR interface and is **not** evidence of library
   independence. MediatR is not relocated to Infrastructure and public contracts are not
   replaced. Any move to MediatR-independent Application contracts requires a **separate
   ADR** (`structure.md` §10.4, ADR-008).
8. **The deliverable remains a single deployable monolith.** The 5-layer split is a
   physical/project-level reorganization; it is not a deployment re-architecture. No
   microservices, messaging brokers, or other deployment complexity are introduced without
   demonstrated need (Constitution invariant K.38; ADR-001).
9. **Migrate one module at a time** with independently reviewable commits, preserving
   public contracts and behavior, using `git mv` and updating namespaces, project
   references, solution entries, registrations and tests atomically per module
   (`structure.md` §10.5).
10. **Keep `master` unchanged** until the migration branch is verified and explicitly
    approved for merge (`structure.md` §10.5).

## Conflicts, compatibility and amendments

### ADR-001 (Modular Monolith) — **amended**
- **Stale reference (factual correction).** ADR-001 lists `Core` among the separated
  projects. `Core` was decomposed into `BuildingBlocks` + capability modules
  (`structure.md` §41, on master). This ADR updates the project list to reflect that
  decomposition and the new target.
- **Compatibility (explicit).** The Tooba 5-layer decomposition is compatible with the
  monolith **provided the whole solution still ships as a single process/deployable unit**.
  This ADR does not change the deployment model; it refines the physical project layout.
  Microservices remain rejected (Constitution K.38).
- **Amended text** is applied directly to ADR-001 with a cross-reference to this ADR.

### ADR-007 (Shared Application Authority) — **compatible, cross-reference only**
- No conflict. Tooba `<Capability>.Endpoints/` **is** ADR-007's "thin adapter": it
  delegates to the same Application capabilities, contains no business calculation, and
  REST/MCP/sync continue to share Application authority. This ADR cross-references ADR-007;
  no text change is required.

### ADR-008 (MediatR License Baseline) — **compatible, cross-reference only**
- No conflict. Decision 7 reaffirms ADR-008: MediatR `12.5.0` / `Contracts 2.0.1`
  (Apache-2.0) stays pinned in Application; nothing is relocated to Infrastructure; moving
  off MediatR requires a separate ADR. This ADR cross-references ADR-008; no text change is
  required.

### Behavioral invariants — **compatible; migration constraints (no amendment)**
ADR-002 (tenancy), ADR-003 (provider ports/adapters), ADR-004 (external vs canonical
identity), ADR-005 (sync & idempotency) and ADR-006 (customer connector boundary) are
behavioral invariants. The physical reorganization must **preserve** all of them. The
extended architecture guards (below) must keep enforcing them after each migration wave.

## Consequences

- **Governance gate before code.** No physical migration begins until a phased wave is
  explicitly authorized in `DELIVERY-PLAN.md` (see *Migration gating*). This ADR + the wave
  authorization are the "architecture decision and wave authorization" required before
  migration.
- **Extended architecture guards.** `FaraTaraz.ArchitectureTests` must be updated to
  enforce, for the migrated layout: project/layer ownership; inward dependency direction;
  **Host zero business authority**; **no business logic in Endpoints**; no infrastructure
  leakage into Application/Domain; exact path↔namespace correspondence; feature-first
  Application folders; valid Command/Query placement; source-file size limits; and the
  preserved tenant/CQRS/idempotency invariants. Guards must not be weakened to hide
  failures (`structure.md` §10.5).
- **Regression evidence.** Debug and Release builds (zero warnings/errors) and all existing
  plus new tests must pass after each wave and at the end.
- **Visual Studio fidelity.** Real physical directories must match the Solution Explorer
  hierarchy; solution-only virtual folders do not count (`structure.md` §10.5).

## Migration gating

Per `DELIVERY-PLAN.md`, no agent may automatically start a wave; every wave is reviewed
before continuation. The physical migration is therefore **PLANNED and pending explicit
authorization**, and is described as a phased plan in `DELIVERY-PLAN.md`. It does not begin
until a reviewer authorizes the first wave against baseline `19dab6fc4b877181d0cd0ad385dba9e196f5c886`.

## Rejected Alternatives

- **Flat single project:** rejected — unscalable as domains grow (ADR-001).
- **Microservices upfront:** rejected — no demonstrated need; violates Constitution K.38
  (ADR-001).
- **Relocating MediatR to Infrastructure or dropping it:** rejected without a separate ADR
  — would move a delivery dependency out of the Application layer and risk changing public
  contracts (ADR-008, `structure.md` §10.4).
- **Moving business rules into Endpoints/Infrastructure to thin the Host:** rejected —
  violates Application authority (ADR-007) and the layer-authority rules (`structure.md`
  §10.3).

# ADR-001: Modular Monolith

**Status:** Accepted
**Date:** 2026-10-07

## Context
FaraTaraz will grow into a large platform with many capability domains (tenancy,
identity, ingestion, master data, sales/inventory/customer intelligence, forecasting,
agents). A single massive codebase would become unmanageable, but splitting into
microservices upfront would add operational complexity with no demonstrated need.

## Decision
Build a **modular monolith** organized into clearly separated projects:
`BuildingBlocks` (foundation), capability modules (e.g. `MasterData`, `AccountingSources`,
`Ingestion.Domain`, `Ingestion.Application`), `Adapters/*`, `Hosts/*`, plus test
projects. The former `Core` project was decomposed into `BuildingBlocks` + capability
modules and nothing references it (`docs/architecture/structure.md` §41). Modules
communicate through well-defined boundaries (interfaces, application use cases). Each
module has an explicit dependency direction. The first deployable host can be a single
process; future modularity enables extraction only when justified.

**Target physical layout (accepted target, migration pending explicit wave
authorization).** All business modules will be migrated to a justified, independently owned
5-layer physical decomposition — `<Capability>.Application`, `<Capability>.Contracts`,
`<Capability>.Domain`, `<Capability>.Endpoints`, `<Capability>.Infrastructure` — with a
composition-only `src/Host/` and per-module test projects under `tests/`. This is a
**physical/project-level reorganization only**: the solution remains a **single deployable
monolith** (one process). It is not a deployment re-architecture, and no microservices or
messaging complexity are introduced without demonstrated need. Governed by
[ADR-009](./ADR-009-FaraTaraz-Modular-Clean-Architecture.md) and
`docs/architecture/structure.md` §10.

## Consequences
- Low operational complexity; one deployable unit.
- Clear module boundaries enforce dependency direction and testability.
- Future decomposition is possible but not required until demonstrated.

## Rejected Alternatives
- **Microservices (early):** rejected — no need yet; violates the "no infrastructure
  complexity without demonstrated need" invariant.
- **Single flat project:** rejected — unscalable as domains grow.

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
`BuildingBlocks`, `Core`, `Adapters/*`, `Hosts/*`, plus test projects. Modules communicate
through well-defined boundaries (interfaces, application use cases). Each module has an
explicit dependency direction. The first deployable host can be a single process; future
modularity enables extraction only when justified.

## Consequences
- Low operational complexity; one deployable unit.
- Clear module boundaries enforce dependency direction and testability.
- Future decomposition is possible but not required until demonstrated.

## Rejected Alternatives
- **Microservices (early):** rejected — no need yet; violates the "no infrastructure
  complexity without demonstrated need" invariant.
- **Single flat project:** rejected — unscalable as domains grow.

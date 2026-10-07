# FaraTaraz / فراتراز

A **multi-tenant, provider-agnostic Accounting Intelligence Platform**.

FaraTaraz is **not** an AI dashboard for Asan Accounting. Accounting systems are
replaceable adapters. Source identities are explicitly mapped to FaraTaraz-owned
canonical identities. Deterministic application capabilities own business truth, and AI
is a controlled consumer of those capabilities.

See the authoritative policy in
[`docs/architecture/architecture-constitution.md`](docs/architecture/architecture-constitution.md)
and the operating rules in [`AGENTS.md`](AGENTS.md).

## Dependency direction

```
BuildingBlocks  →  (nothing platform-wide)
Core            →  BuildingBlocks
Adapters/*      →  Core, BuildingBlocks
Tests           →  whatever they assert
```

Core never references a concrete provider adapter. This is enforced by automated
architecture tests.

## Layout

| Path | Purpose |
| --- | --- |
| `src/BuildingBlocks` | Foundation: strong ids, `Tenant`, `AccountingSource`, `TenantContext`, provider capability primitives. |
| `src/Core` | Platform domain: capability ports, source model, master data (external + canonical), application authority marker. |
| `src/Adapters/Accounting.Mock` | Mock provider proving the capability contract (partial capabilities, source-scoped identities). |
| `tests/FaraTaraz.BuildingBlocks.Tests` | Unit tests for foundation concepts. |
| `tests/FaraTaraz.ArchitectureTests` | Durable automated guards (dependency direction, provider leakage, tenant ownership, external identity, capability model, reference graph). |
| `docs/architecture` | Constitution + ADR-001…007. |

## Build & test

```bash
dotnet build FaraTaraz.sln
dotnet test FaraTaraz.sln
```

## Status

W0 establishes the architecture foundation only. No business features, no real sync,
no Asan integration, no AI/forecast/dashboard, no MCP tools.

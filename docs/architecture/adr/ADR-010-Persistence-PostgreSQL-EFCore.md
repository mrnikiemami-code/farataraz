# ADR-010: Persistence — PostgreSQL + EF Core

**Status:** Accepted
**Date:** 2026-10-09
**Author:** W2 Persistence Foundation (`FT-W2-PERSISTENCE-FOUNDATION-001`)
**Depends on:** ADR-001 (Modular Monolith), ADR-002 (Multi-Tenant Model), ADR-003 (Provider
Ports/Adapters), ADR-004 (External vs Canonical Identity), ADR-005 (Sync & Idempotency),
ADR-009 (FMCA)
**Amends:** `structure.md` §10 (adds the deferred `Infrastructure` layer as realized for
`AccountingSources` and `Ingestion`)

## Context

W1 defined and proved the synchronization contract against the deterministic Mock provider,
but the platform had **no persistence**. W2 introduces a real relational store so that:
- tenant-owned accounting sources and their ownership survive process restarts;
- synchronization run state, checkpoints/cursors, source-record identity and provenance are
  durable;
- idempotency and tenant isolation are enforced by the **database**, not only by policy in
  application code (Constitution A.3, E.20; ADR-002 consequences).

The decision must be consistent with the accepted constitution: tenant authority comes only
from a trusted execution context (A.4), source ownership is fail-closed (A.46), provider
concepts never leak into the platform (B.7–B.10), and the physical layout follows FMCA
(ADR-009, `structure.md` §10). This ADR does **not** implement W3 (the ingestion engine),
real provider integrations, dashboards, REST/MCP, or AI.

## Decision

1. **Relational store = PostgreSQL.** PostgreSQL is the production relational database
   (Constitution K.38: no unnecessary complexity; a single well-understood RDBMS is the
   justified choice for a modular monolith).

2. **Persistence implementation = EF Core.** EF Core is the ORM/migration tool. It is an
   **Infrastructure-only** concern: it lives inside each module's `<Capability>.Infrastructure`
   project and never appears in Domain, Application, BuildingBlocks, or the Host
   (ADR-009 decision 4; `structure.md` §10.3).

3. **Module-owned Infrastructure projects.** Real responsibilities get real, justified
   projects (no empty placeholders, `structure.md` §10.1):
   - `src/Modules/AccountingSources/AccountingSources.Infrastructure/`
     (`FaraTaraz.Modules.AccountingSources.Infrastructure`) persists `Tenant` +
     `AccountingSource` and implements the provider-independent ownership oracle
     (`IAccountingSourceOwnership`).
   - `src/Modules/Ingestion/Ingestion.Infrastructure/`
     (`FaraTaraz.Modules.Ingestion.Infrastructure`) persists synchronization run state,
     checkpoints/cursors, source-record identity, and provenance, with durable idempotency
     keys and uniqueness constraints.

4. **Tenant isolation is structural and database-enforced.** Every tenant-owned table carries a
   `TenantId` column. All queries and writes are tenant-scoped by a **trusted** `TenantId`
   resolved from the trusted execution context (ADR-002, Constitution A.4/A.5). There is no
   static/global `TenantId` and no mutable global tenant context. Cross-tenant reads/writes are
   impossible by construction: a scoped query is parameterized by the trusted tenant, and a
   UNIQUE constraint on `(TenantId, …)` rejects cross-tenant rows at the database.

5. **Trusted tenant-context resolution.** The trusted `TenantId` flows from a trusted execution
   context (`TenantContext.FromAuthenticatedPrincipal`) into a per-operation scope. The DbContext
   is created with that trusted tenant bound (via a scoped `TenantContext`/database tenant
   marker), never from client/LLM/MCP input. Background jobs execute with explicit trusted
   `Tenant` + `AccountingSource` context (Constitution A.5).

6. **Source ownership is fail-closed and database-backed.** `IAccountingSourceOwnership` is
   implemented against the `AccountingSources` table, scoped by the trusted `TenantId`. It
   returns `false` (never throws, never authorizes) for an unknown tenant, unknown source, or a
   lookup failure. Source identity alone never grants authorization (Constitution A.46;
   ADR-009 decision 4).

7. **Idempotency and uniqueness are database constraints.** Source-record identity is
   `(TenantId, AccountingSourceId, RecordKind, ExternalId)`. A UNIQUE constraint on that tuple
   makes duplicate delivery provably non-duplicating: a second identical record is rejected (or
   upserted) by the database, so the same source record never creates duplicate downstream
   state (Constitution E.20; ADR-005). Checkpoints carry a UNIQUE constraint on
   `(TenantId, AccountingSourceId, Capability)` so a cursor is stored exactly once per
   source/capability.

8. **Transactional consistency.** Related writes (e.g. a run record + its checkpoint + ingested
   records) are completed within a single `DbContext` transaction committed atomically
   (`SaveChanges` under `BEGIN`/`COMMIT`). Partial failure rolls back the whole unit of work.
   Concurrency is handled by the unique constraints: concurrent duplicate inserts race at the
   constraint, and exactly one wins (the other gets a unique-violation, treated as a duplicate).

9. **Checkpoint durability and validity.** Checkpoints persist the opaque cursor token plus its
   platform scope `(SourceId, Capability)`. Resume validates the scope before honoring a
   cursor (preserving `SyncCursor`/`SyncCursors.ValidateResume` semantics). An invalid or
   mismatched checkpoint is rejected, never silently restarted (Constitution E.21).

10. **Migration ownership.** EF Core migrations live inside each Infrastructure project and are
    owned by that module. They are reviewed manually (not auto-generated-and-committed blindly)
    and applied by an operator/CI step; the platform never migrates the database silently at
    runtime.

11. **Failure and retry semantics.** W2 stores the failure state needed by W3 (run status, last
    attempt/last success, error) but does **not** implement retry loops. The failure taxonomy
    (`SyncFailureCategory`) remains a Domain contract that Infrastructure records, not decides
    (ADR-005, Constitution E.21).

12. **Integration testing strategy.** Real PostgreSQL integration tests exercise actual database
    behavior (tenant isolation, fail-closed ownership, idempotency, concurrent duplicate
    insertion, transaction rollback, invalid checkpoint) — not EF Core InMemory simulations.
    Tests run against a disposable PostgreSQL database/container. CI provisions PostgreSQL for
    these tests. If no real PostgreSQL is available, the integration tests are skipped and this
    is reported (never claimed as passed).

13. **Future database-per-tenant compatibility.** The current model is one database, tenant
    rows scoped by `TenantId` (shared database, tenant-column isolation). The design keeps every
    tenant row `TenantId`-scoped so a future database-per-tenant deployment can pin the
    connection string per tenant without changing domain invariants. This is a documented,
    forward-compatible choice, not implemented now (Constitution K.38: no unproven need).

14. **Layer authority is preserved.** Domain and Application reference **no** EF Core,
    PostgreSQL, or concrete Infrastructure type. The Application layer resolves its inward
    ports (capability port, ownership oracle, trusted context) and never a concrete
    implementation; only the module-owned `Composition` (DI wiring) references the module's
    Infrastructure to bind ports to implementations. The Host composes modules through their
    `Composition` entry points and never references a concrete adapter or persistence assembly
    (ADR-009, `structure.md` §10.3).

## Consequences

- **New Infrastructure projects** under `src/Modules/AccountingSources` and
  `src/Modules/Ingestion`, each with EF Core, migrations, and a `Composition` entry point.
- **Guard updates** (not weakenings): the layer-graph guard now permits Application → its own
  module's Infrastructure **composition** while still forbidding Application → adapters and →
  EF in business code; a new guard proves Application business handlers never depend on EF/
  Infrastructure types. Existing "no persistence in BuildingBlocks/Domain/Application" guards
  remain and still pass (they do not enumerate the new Infrastructure projects).
- **Physical-structure guards** extend their known-project allowlists to the two new folders.
- **No empty placeholder projects.** Infrastructure projects exist only because they own real
  persistence responsibilities here; other layers remain deferred with evidence.
- **CI** provisions PostgreSQL and fails the workflow on invariant breach or test failure.
- **No W3, no real provider integration, no dashboard/REST/MCP/AI** in this wave.

## Rejected Alternatives

- **EF Core InMemory only:** rejected — it cannot prove tenant isolation, unique-constraint
  idempotency, or transactional rollback (Constitution E.20/A.3 require the database to
  participate in correctness).
- **Raw ADO/SQL in Application:** rejected — persistence belongs to Infrastructure, not Application
  (ADR-009, `structure.md` §10.3).
- **Single shared DbContext for all modules:** rejected — module ownership of migrations and
  schema is a core FMCA invariant; cross-module DbContext coupling would violate inward
  dependency direction.
- **Tenant isolation by application filter only:** rejected — filters are policy and can be
  bypassed; unique constraints make cross-tenant rows impossible at the database
  (Constitution A.3).

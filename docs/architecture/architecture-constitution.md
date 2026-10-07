# FaraTaraz Architecture Constitution

**Status:** Active / Authoritative
**Product:** FaraTaraz / فراتراز — a multi-tenant, provider-agnostic Accounting Intelligence Platform
**Repository root namespace:** `FaraTaraz`

This document is the authoritative architecture policy for FaraTaraz. It encodes the
invariants that must hold at every layer. Where a decision conflicts with code, tests,
or future work, this constitution wins. Architectural invariants here are enforced by
automated architecture tests where feasible (see `tests/FaraTaraz.ArchitectureTests`).

---

## Definitions

- **Tenant** — the purchasing organization that consumes FaraTaraz. A Tenant is NOT an
  accounting system, NOT an accounting branch, and NOT a customer stored in an accounting system.
- **AccountingSource** — one configured accounting-system instance belonging to exactly
  one Tenant.
- **Provider** — an accounting product family (Asan, Sepidar, Holoo, Mahak, custom ERP, …).
- **External identity** — a source-scoped identity (`AccountingSourceId + ExternalCode`).
- **Canonical identity** — a FaraTaraz-owned, tenant-bound identity.

---

## Invariants

### A. Tenancy

1. **Tenant is the purchasing organization.** A Tenant is not an accounting system,
   branch, or customer.
2. **AccountingSource belongs to exactly one Tenant.** One Tenant may own many sources;
   one adapter may serve many sources.
3. **Tenant isolation is mandatory.** All tenant-owned data is tenant-bound. No
   cross-tenant access is permitted under any circumstance.
4. **Tenant authority comes only from a trusted execution context.** It never originates
   from client, LLM, or MCP input. `tenantId = arbitrary-value` is forbidden.
5. **Background jobs execute with explicit trusted Tenant + AccountingSource context.**
   No static/global TenantId; no mutable global tenant context.
6. **Tenant-boundary tests are mandatory.** Every change that touches tenant isolation
   must include a test proving tenant-scoped behavior.

### B. Provider independence

7. **Core modules never reference concrete accounting providers.** No `AsanCustomer`,
   `AsanProduct`, `AsanInvoice`, `SepidarInvoice`, etc. may exist in Core/BuildingBlocks.
8. **Provider-specific DTOs, API clients, field names, and mapping logic stay inside the
   adapter boundary.** They never escape into Core/Application/Domain.
9. **Adding a provider should primarily require a new adapter.** The platform core must
   not change to add a provider.
10. **Provider assumptions must not leak into platform invariants.** Equal external codes
    across sources do NOT prove equal canonical entities.

### C. Capability ports

11. **Provider capabilities are independent and explicit.** Capability-oriented ports
    (`ICustomerSource`, `IProductSource`, `ISalesSource`, `IInventorySource`,
    `IPurchaseSource`, `IPaymentSource`, …). No single giant `IAccountingSystem`.
12. **Unsupported capability is an explicit failure, never an empty result.** A provider
    must not return empty collections for unsupported capabilities.
13. **Capability availability is declared, not inferred.** Empty data ≠ capability
    unsupported.

### D. Identity: external vs canonical

14. **External identifiers are source-scoped.** `AccountingSourceId + ExternalCode`
    identifies a source-side identity.
15. **Canonical identities are owned by FaraTaraz** and are tenant-bound.
16. **External and canonical models remain distinct.** Provider records are never mapped
    directly into canonical entities.
17. **Identity mapping supports auditability and remapping.** Mappings are recorded and
    reversible-in-principle.
18. **No fuzzy/AI matching in foundational work.** Equal names or equal codes do not
    prove identity. Human review must be architecturally possible for uncertain matches.

### E. Synchronization

19. **Synchronization is a first-class subsystem**, not a utility method.
20. **Sync must support idempotency.** Receiving the same source record twice must not
    create duplicate sales.
21. **Sync semantics are explicit.** Full/incremental, checkpoints/cursors, retries,
    partial failure, duplicate delivery, provider timeout, changed records, voided
    invoices (where supported), status, last attempt, last success, diagnostics.
22. **Not every provider supports incremental sync.** Assume full sync unless explicit.

### F. Data pipeline & provenance

23. **Source provenance is preserved.** The pipeline preserves stages:
    Provider → Adapter → Source Representation → Ingestion → Identity Resolution →
    Canonical Model → Analytical Model → Application Use Cases → REST/MCP → Dashboard/AI.
24. **Provider data provenance remains traceable.** Reconciliation, debugging,
    reprocessing, remapping, and auditing must be possible.
25. **Analytical projections are conceptually distinct from operational/canonical data.**
    Analytics must not depend directly on provider DTOs/raw payloads.
26. **Raw provider payload retention is policy-gated.** Do not blindly store sensitive
    payloads forever; document the retention decision.

### G. Application authority

27. **Application use cases are the authoritative interface** to platform behavior.
28. **REST and MCP share Application authority.** They delegate to the same capabilities.
29. **Dashboard does not access persistence directly.** `Dashboard → Database` is forbidden.
30. **AI/LLM does not access persistence directly.** `LLM → Database` is forbidden.

### H. AI, forecasting, and policies

31. **MCP has no independent business logic.** It only delegates to Application use cases.
32. **Forecasting is independent from the LLM.** Historical data → Forecast Engine →
    Demand Forecast → Replenishment → Recommendation. The LLM explains; it never
    calculates.
33. **Authoritative numeric results are reproducible without an LLM.** Demand, financial
    totals, stock quantities, reorder quantities, and customer metrics must be
    deterministic.
34. **Business policies (slow/fast-moving, dead stock, VIP, at-risk, safety stock,
    reorder point, replenishment quantity) are deterministic**, live outside prompts,
    and become tenant-configurable later.

### I. Security & credentials

35. **Credentials/secrets never enter Domain models.** Configuration is a reference, not
    a value.
36. **MCP tools must not accept arbitrary TenantId as normal business input.** Tenant
    authority for MCP comes from the trusted execution context, not tool arguments.

### J. Deployment boundary

37. **Customer-side connector boundary is documented.** Accounting APIs may be reachable
    only inside a customer LAN; the connector performs outbound-only secure HTTPS.
    Credentials ideally remain inside the customer environment. No inbound exposure
    required.

### K. Complexity & testability

38. **Infrastructure complexity requires demonstrated need.** No microservices, Kafka,
    RabbitMQ, Kubernetes, event sourcing, Elasticsearch, vector DB, Python services, or
    distributed orchestration without justification.
39. **Adapter contract/conformance tests must be possible.** Each provider adapter is
    testable against a shared contract.
40. **Architecture invariants are enforced by automated guards where feasible.** Rules
    that cannot yet be automated are documented with the reason and a plan to automate.

---

## Source of Truth for Behavior

Application use cases own business truth. REST, MCP, and background sync are thin
adapters that reuse the same capabilities, so the dashboard and AI agents can never
produce conflicting business answers.

FaraTaraz is **not** an AI dashboard for Asan Accounting. FaraTaraz is a
multi-tenant, provider-agnostic platform in which accounting systems are replaceable
adapters, source identities are explicitly mapped to FaraTaraz-owned canonical
identities, deterministic application capabilities own business truth, and AI is a
controlled consumer of those capabilities.

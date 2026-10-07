# CURRENT STATE — FaraTaraz

**Last updated:** W1 — Synchronization Contracts
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
  no persistence packages in Core/BuildingBlocks, `SyncRequest` carries no `TenantId`

Critical acceptance: processing the same source record repeatedly is behaviorally
**idempotent** (stable identity across repeated delivery).

**Test evidence:**
- `FaraTaraz.SyncContracts.Tests` — Mock conformance (paging, cursor continuation,
  incremental declaration, cancellation, batch-size honoring, empty source, unsupported
  capability) + idempotency (same record twice / ten times / repeated batch / changed
  content / different record kind / different source) + interruption & resume + failure
  classification.
- `FaraTaraz.ArchitectureTests` — W1 sync guards (see above).

**Not done in W1 (correctly deferred):** real persistence (W2), ingestion engine (W3),
any real provider integration. No PostgreSQL.

---

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

# ROADMAP — FaraTaraz

**Status:** Approved (P0)
**Baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3` (W0 CERTIFIED)

Dependency-aware roadmap. Waves are **not** auto-started. Each wave is authorized by an
explicit task, not by P0 or by the previous wave completing. See
[`DELIVERY-PLAN.md`](DELIVERY-PLAN.md) for execution rules and the status model.

Architecture rules remain authoritative in
[`../architecture/architecture-constitution.md`](../architecture/architecture-constitution.md).

---

## Phases / Waves

### P0 — Planning & Source of Truth

**Goal:** Repository-owned product / delivery plan.

**Status:** COMPLETE

**Deliverables:** this planning area (`PRODUCT`, `ROADMAP`, `DELIVERY-PLAN`,
`CURRENT-STATE`, `DECISIONS-AND-OPEN-QUESTIONS`).

---

### W0 — Architecture Foundation

**Baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3`

**Status:** COMPLETE — **CERTIFIED**

**Deliverables:**
- architecture constitution
- ADR-001..007
- tenant / source identity
- provider capabilities
- external / canonical identity seam
- architecture tests
- Mock provider foundation

---

### W1 — Synchronization Contracts

**Goal:** Define and prove provider-independent synchronization semantics *before* real
persistence.

**Scope:**
- async provider capability contracts
- cancellation
- pagination / batching where justified
- `SyncRequest`
- `SyncCursor` / checkpoint abstraction
- `SyncBatch`
- source record identity
- sync outcome
- retry classification
- duplicate-delivery semantics
- full vs incremental capability
- provenance
- Mock synchronization scenarios
- adapter conformance tests

**Critical acceptance:** processing the same source record repeatedly must be
behaviorally **idempotent**.

**Test scenarios:** same record twice; same record 10 times; repeated batch;
interruption between batches; restart from checkpoint; provider timeout; cancellation;
empty valid source; unsupported capability.

**No PostgreSQL yet** unless required by an explicit architecture decision.

**Status:** `PLANNED` — **NOT authorized by P0.** A separate explicit task is required.

---

### W2 — Persistence Foundation

**Dependency:** W1 contracts stable.

**Goal:** Introduce PostgreSQL + EF Core after W1 is stable.

**Scope:** tenant-aware persistence; AccountingSource persistence; sync run / checkpoint
persistence; source record / provenance persistence; uniqueness / idempotency constraints;
migrations; transaction boundaries.

**Critical acceptance:** database constraints participate in tenant isolation and
duplicate prevention.

**Status:** `PLANNED`.

---

### W3 — Ingestion Engine

**Dependency:** W2.

**Goal:** Implement durable synchronization execution.

**Scope:** orchestration; retries; failure states; resumability; checkpoints; freshness;
diagnostics; partial failure handling.

Mock provider remains primary verification provider.

**Status:** `PLANNED`.

---

### W4 — Master Data & Identity Resolution

**Dependency:** W3.

**Goal:** External identities → canonical identities.

**Scope:** canonical Product; canonical Customer; external mappings; deterministic
matching; mapping audit; remapping; ambiguous mapping review state.

**NO AI auto-merge.**

**Status:** `PLANNED`.

---

### W5 — Sales Data Foundation

**Dependency:** W3, W4, **verified provider capability to READ historical sales / invoice
data.**

**Scope:** source sales representation; invoice identity; invoice lines;
corrections / voids where supported; canonical sales model; sales projections.

**STOP condition:** if Asan cannot provide required sales history, STOP and redesign
ingestion source strategy rather than fabricating data.

**Status:** `PLANNED`.

---

### W6 — Inventory Intelligence

**Dependency:** W3, W4.

**Scope:** inventory snapshots; stock state; stock movement inputs where available;
velocity inputs; freshness semantics.

**Status:** `PLANNED`.

---

### W7 — Customer Intelligence

**Dependency:** W3, W4, W5.

**Scope:** purchase history; last purchase; purchase interval; customer value;
inactivity; risk; RFM where justified.

All definitions deterministic.

**Status:** `PLANNED`.

---

### W8 — Product & Sales Intelligence

**Dependency:** W5, W6.

**Scope:** fast-moving; slow-moving; dead stock; sales trends; source / branch
comparison; product performance.

**Status:** `PLANNED`.

---

### W9 — Forecasting

**Dependency:** W6, W8.

**Goal:** Start simple and deterministic.

**Candidates:** moving averages; weighted moving averages; exponential smoothing;
seasonality where sufficient data exists. Evaluation required.

**Do not use LLM-generated forecasts.**

**Status:** `PLANNED`.

---

### W10 — Replenishment

**Dependency:** W9, W6.

**Inputs:** forecast; current inventory; safety stock; lead time; incoming stock if
available.

**Outputs:** reorder recommendation; explanation inputs; confidence / data-quality
indicators.

**Status:** `PLANNED`.

---

### W11 — Application API

**Dependency:** W4..W10 (as relevant).

**Scope:** expose approved application use cases. REST remains a thin adapter over
Application authority.

**Status:** `PLANNED`.

---

### W12 — Dashboard

**Dependency:** W11.

**Scope:** React + TypeScript. Executive overview; sales; inventory; purchasing;
customers; forecasts; sync health; data freshness.

**Status:** `PLANNED`.

---

### W13 — Agent / MCP

**Dependency:** W11.

**Scope:** expose controlled tools over existing Application use cases. No independent
calculations; no arbitrary database access; no arbitrary TenantId.

**Status:** `PLANNED`.

---

### W14 — Real Asan Adapter

**Dependency:** provider discovery proves the API contract.

**Scope:** Asan provider adapter. May begin earlier only after contract verification.

**Do not let Asan implementation change platform invariants.** Adapter conformance tests
mandatory.

**Status:** `PLANNED`.

---

### W15 — Customer-Side Connector

**Dependency:** W14 (where APIs are LAN-only).

**Scope:** outbound-only secure communication; credential isolation; reconnect / retry;
source authentication; secure provisioning.

**Status:** `PLANNED`.

---

### W16 — Production Hardening

**Dependency:** W11..W15 (as relevant).

**Scope:** authentication / authorization; tenant isolation verification; audit;
observability; backup / recovery; rate limiting; secret management; deployment; health
checks; failure drills; performance testing.

**Status:** `PLANNED`.

---

### W17 — Pilot & Acceptance

**Dependency:** W16.

**First real Tenant:** 3 Asan sources.

**Verify:** synchronization; mappings; historical accuracy; analytics accuracy; forecast
usefulness; replenishment usefulness; customer-risk usefulness; dashboard; AI answers;
tenant isolation; operational recovery.

**Status:** `PLANNED`.

---

## Roadmap dependencies

```text
P0
 ↓
W0
 ↓
W1 Sync Contracts
 ↓
W2 Persistence
 ↓
W3 Ingestion
 ↓
W4 Identity Resolution
 ↓
Verified Business Data
 ├───────────────┬───────────────┐
 ↓               ↓               ↓
Sales         Inventory       Customer
 ↓               ↓               ↓
 └────────── Analytics ──────────┘
                 ↓
            Forecasting
                 ↓
           Replenishment
                 ↓
         Application API
             ↙       ↘
       Dashboard      MCP/Agent
```

Real provider work may proceed in parallel **only** when the provider contract is
verified.

---

## Status model

- `PLANNED` — scheduled, not yet started.
- `IN_PROGRESS` — authorized and being worked.
- `BLOCKED` — waiting on a decision, discovery gate, or dependency.
- `PARTIAL` — partially done; not accepted.
- `PASS` — task acceptance criteria passed.
- `CERTIFIED` — passed AND its architectural / integration evidence reviewed and accepted
  as the new baseline.

**W0 is `CERTIFIED`.**

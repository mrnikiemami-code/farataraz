# DECISIONS AND OPEN QUESTIONS — FaraTaraz

**Status:** Living document. Review before starting any wave that touches these topics.

Unresolved product / technical questions. **Do NOT invent answers.** Open questions that
block a wave should be resolved at that wave's discovery gate.

---

### OQ-001 — Asan historical sales read capability

**Status:** OPEN / CRITICAL

Can the Asan API read historical **final sales invoices** and **invoice lines**?

**Impact:** sales intelligence, customer behavior, forecasting, replenishment.

**Classification:** `UNVERIFIED_PROVIDER_CAPABILITY`

---

### OQ-002 — Asan historical purchase read capability

**Status:** OPEN

Can purchase history / incoming orders be read?

**Impact:** replenishment quality.

**Classification:** `UNVERIFIED_PROVIDER_CAPABILITY`

---

### OQ-003 — Inventory semantics

**Status:** OPEN

Is inventory current-state only, historical snapshots, or movement-level data?

**Impact:** inventory intelligence, velocity, dead-stock, stock-out detection.

---

### OQ-004 — Customer identifiers

**Status:** OPEN

Which stable identifiers are available: national ID, company ID, phone, tax ID, etc.?

**Impact:** identity resolution, customer intelligence, risk detection.

---

### OQ-005 — Product identifiers

**Status:** OPEN

Which stable identifiers are available: barcode, GTIN, SKU, manufacturer code, etc.?

**Impact:** identity resolution, matching signals, master data.

---

### OQ-006 — Deployment topology

**Status:** OPEN

For the first customer, can the Asan API be reached from the cloud, or only from the local
LAN?

**Impact:** customer-side connector requirement (W15); credential placement (ADR-006).

---

### OQ-007 — Data volume

**Status:** OPEN

Approximate:
- products
- customers
- invoices
- invoice lines
- years of history
- daily transaction volume

**Impact:** synchronization and analytical projection design.

---

### OQ-008 — Equal codes across the first three Asan instances

**Status:** UNDERSTOOD (guard rail)

Known: the first deployment uses identical customer and product codes across three Asan
instances.

**Guard:** this is a property of the first deployment only. It MUST NOT become a platform
invariant. Equal external codes do not prove equal canonical entities (ADR-004).

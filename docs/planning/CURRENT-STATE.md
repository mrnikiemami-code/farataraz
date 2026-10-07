# CURRENT STATE — FaraTaraz

**Last updated:** P0 (this task)
**Baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3`

Operational state. Should be readable in under ~2 minutes.

---

## Where are we?

Planning area (`docs/planning/`) is established. Implementation has **not** continued past
W0.

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

## What is next?

**Next implementation candidate: W1 — Synchronization Contracts.**

**BUT: W1 IS NOT AUTHORIZED BY P0 ITSELF.** A separate explicit task is required before
W1 starts.

## What is blocked?

- Any real Asan analytics work — blocked on the **unverified provider capability**
  discovery gate (see `PRODUCT.md` and `DECISIONS-AND-OPEN-QUESTIONS.md`, OQ-001 / OQ-002).
- W5 (Sales) — blocked on verified read access to historical sales / invoice data.

## What must NOT be started?

- W1 (until explicitly authorized)
- W2 persistence (PostgreSQL) until W1 contracts are stable
- Any real Asan integration until the provider contract is verified
- AI auto-merge of canonical identities (W4) — deterministic matching only
- LLM-generated forecasts (W9) — deterministic only

---

## Quick navigation

- Product: `PRODUCT.md`
- Waves: `ROADMAP.md`
- Execution rules + SoT hierarchy: `DELIVERY-PLAN.md`
- Unresolved questions: `DECISIONS-AND-OPEN-QUESTIONS.md`
- Architecture (authoritative): `../architecture/architecture-constitution.md`

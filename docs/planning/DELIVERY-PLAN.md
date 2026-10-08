# DELIVERY-PLAN — FaraTaraz

**Status:** Approved (P0)
**Baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3` (W0 CERTIFIED)

Execution rules for implementation waves. Planning lives here; architecture rules remain
authoritative in
[`../architecture/architecture-constitution.md`](../architecture/architecture-constitution.md).

---

## Source-of-Truth (SoT) hierarchy

Order of authority, highest first:

1. **Architecture Constitution** — `docs/architecture/architecture-constitution.md`
2. **Accepted ADRs** — `docs/architecture/adr/`
3. **Current State** — `docs/planning/CURRENT-STATE.md`
4. **Roadmap / Delivery Plan** — `docs/planning/ROADMAP.md`, `DELIVERY-PLAN.md`
5. **Current authorized task**
6. **Implementation**

**Conflict rule:** if an implementation or task conflicts with the Constitution or an
accepted ADR, **STOP**. Do not silently follow the lower-level instruction.

---

## Agent startup requirements

Before modifying code, every agent must read, in order:

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md`
3. `docs/planning/CURRENT-STATE.md`
4. relevant ADRs
5. relevant roadmap wave

---

## Wave template

Every implementation wave should capture:

- **Task ID**
- **Parent / baseline SHA**
- **Goal**
- **In scope**
- **Out of scope**
- **Architecture invariants affected**
- **Acceptance criteria**
- **Required tests**
- **Build verification**
- **Git commit**
- **Push evidence**
- **Completion report**
- **Explicit STOP before next wave**

**No agent may automatically start the next wave.** Every wave is reviewed before
continuation. P0 does not authorize W1; a separate explicit task is required.

---

## Definition of Done (per wave)

A wave is not complete merely because code compiles. Where applicable:

- scope implemented
- architecture invariants preserved
- tests added
- all relevant tests pass
- Release build passes
- zero unexpected warnings
- documentation updated
- migrations reviewed if present
- no secrets
- no unrelated changes
- commit created
- commit pushed
- remote SHA verified
- completion report produced
- reviewer accepts evidence

---

## Current wave status

| Wave | Status | Notes |
| --- | --- | --- |
| P0 — Planning & Source of Truth | CERTIFIED | this planning area |
| W0 — Architecture Foundation | CERTIFIED | baseline `d499e61730579c2ad9810d306dd2620c986bd9f3` |
| W1 — Synchronization Contracts | PASS | acceptance criteria passed; **not certified** — reviewer must accept before it becomes the baseline. W1-R1 added the capability-first modular structure (`BuildingBlocks` + `MasterData` + `AccountingSources` + `Ingestion.Domain` + `Ingestion.Application`), CQRS through MediatR (`ISender` boundary), and structural guards. |
| W1-R1 — Architecture Structure + CQRS Foundation | PASS | still part of W1; **not certified**. Capability-first modular monolith, `ISender` delivery boundary, structural guards. Baseline HEAD: `ae27f28d006614ea6c21cbe504990245ac11342a`. |
| W1-R2 — Architecture Closure & Certification Readiness | PASS (pending external review) | still part of W1; **not certified**. Bounded-page CQRS (one page per request, caller-driven cursor), tenant-source ownership port (`IAccountingSourceOwnership`, fail-closed), physical structure guards, sync contract hardening. NOT certified — awaiting reviewer acceptance. |
| W2..W17 | PLANNED | see `ROADMAP.md` |

**Historical certified baselines (do not overwrite):**
- W0 CERTIFIED: `d499e61730579c2ad9810d306dd2620c986bd9f3`
- P0: `0f1221adada6e10cdd1802bfea62bc88a9142f62`
- W1 contracts: `35bbe46dd0cb0c47126028b1123834c69ce45120`
- W1-R1 HEAD: `ae27f28d006614ea6c21cbe504990245ac11342a`

**W1 does not authorize W2.** W2 (PostgreSQL/EF Core) requires a separate explicit task
and an explicit architecture decision to introduce a persistence stack. W1 (including W1-R1
and W1-R2) is `PASS` pending external reviewer acceptance, not `CERTIFIED`.

---

## Release build & test

```bash
dotnet build FaraTaraz.sln -c Release
dotnet test FaraTaraz.sln
```

Both must pass before committing a wave.

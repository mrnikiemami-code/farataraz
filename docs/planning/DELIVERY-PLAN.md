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
| FMCA-ARCH — Modular Architecture Migration | IN PROGRESS (wave 1 completed) | Target in [ADR-009](../architecture/adr/ADR-009-FaraTaraz-Modular-Clean-Architecture.md) + `structure.md` §10; phased plan above; baseline `19dab6fc4b877181d0cd0ad385dba9e196f5c886`. Wave 1 (composition-only `Host` + `Ingestion.Application` feature structure) completed on `master` under task `FT-FMCA-COMPLETE-ARCHITECTURE-001`; `Contracts` / `Endpoints` / `Infrastructure` waves pending explicit authorization. |

**Historical certified baselines (do not overwrite):**
- W0 CERTIFIED: `d499e61730579c2ad9810d306dd2620c986bd9f3`
- P0: `0f1221adada6e10cdd1802bfea62bc88a9142f62`
- W1 contracts: `35bbe46dd0cb0c47126028b1123834c69ce45120`
- W1-R1 HEAD: `ae27f28d006614ea6c21cbe504990245ac11342a`

**W1 does not authorize W2.** W2 (PostgreSQL/EF Core) requires a separate explicit task
and an explicit architecture decision to introduce a persistence stack. W1 (including W1-R1
and W1-R2) is `PASS` pending external reviewer acceptance, not `CERTIFIED`.

---

## Architecture migration wave (FaraTaraz Modular Clean Architecture)

**Status:** IN PROGRESS — wave 1 completed on `master` under task
`FT-FMCA-COMPLETE-ARCHITECTURE-001` (decision prepared by task
`FT-ARCHITECTURE-BASELINE-RECOVERY-001`; recorded in
[ADR-009](../architecture/adr/ADR-009-FaraTaraz-Modular-Clean-Architecture.md) and
`docs/architecture/structure.md` §10). Wave 1 (composition-only `Host` +
`Ingestion.Application` feature structure) is complete. The `Contracts` / `Endpoints` /
`Infrastructure` waves remain *pending explicit wave authorization*; no agent may start them
until a reviewer authorizes them.

- **Baseline for the migration:** `19dab6fc4b877181d0cd0ad385dba9e196f5c886` (adds
  `structure.md` §10 target; sections 1–9 remain the verified baseline).
- **Target:** per-business-module `<Capability>.Application/.Contracts/.Domain/.Endpoints/.Infrastructure`,
  composition-only `src/Host/`, per-module tests under `tests/` (see ADR-009 / `structure.md`
  §10.1).
- **Invariant preserved:** the solution remains a single deployable monolith (ADR-001,
  Constitution K.38). This is a physical reorganization, not a deployment re-architecture.
- **MediatR:** stays in Application at `12.5.0` / `Contracts 2.0.1` (ADR-008). Moving off
  MediatR needs a separate ADR (`structure.md` §10.4).

### Phases (each its own authorized, reviewable wave)

1. **Inventory & guards baseline.** Inventory every `src/` + `tests/` project, source file,
   namespace, assembly reference and solution entry; capture Debug/Release build + test
   baseline; extend `FaraTaraz.ArchitectureTests` with the post-migration guards *against the
   current layout* so they fail until the migration is done (see guards below).
2. **Host → composition root.** Move all business decisions, application handlers, domain
   rules, business validation and persistence out of the Host; leave registration, routing,
   middleware and startup only. Add **Host-zero-business-authority** guard.
3. **Module waves (one module per wave, independently reviewable).** For each business
   module: separate Domain/Application/Contracts/Endpoints/Infrastructure; organize
   Application by feature then Commands/Queries/Models/Ports (+ shared Validation); update
   namespaces, project references, solution entries, DI/dispatch and tests atomically via
   `git mv`; preserve public contracts, tenant/source authorization, idempotency and
   fail-closed rules; prevent cross-module infrastructure coupling.
4. **Final verification.** Solution load; Debug + Release builds (zero warnings/errors); all
   existing and new tests; architecture/Host-authority guards; Visual Studio hierarchy
   fidelity. Keep `master` untouched until approved for merge.

### Extended architecture guards (added in wave 1, enforced through wave 4)

Correct project/layer ownership; inward dependency direction; **Host ZERO business
authority**; **no business logic in Endpoints**; no infrastructure leakage into Application/
Domain; exact path↔namespace correspondence; feature-first Application folders; valid
Command/Query placement; source-file size limits; and the **preserved** tenant, CQRS,
idempotency and provider-independence invariants. Guards must not be weakened to hide
failures.

---

## Release build & test

```bash
dotnet build FaraTaraz.sln -c Release
dotnet test FaraTaraz.sln
```

Both must pass before committing a wave.

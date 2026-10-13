# FaraTaraz — Open Defects and Architecture Acceptance Ledger

## FT-REBASELINE-W4-WORKER-TRUST-DESIGN-001 — architect design review (2026-10-13)

**DESIGN ACCEPTED WITH BINDING CONDITIONS; IMPLEMENTATION NOT AUTHORIZED.** Independently inspected executor commit `7abc3936170288c61865a0412b862097a2cfc528`, 2 docs-only files, actual design `docs/architecture/ARCHITECTURE-W4-WORKER-TRUST-DESIGN-2026-10-13.md`, GitHub Actions [38344782451](https://github.com/mrnikiemami-code/farataraz/actions/runs/38344782451) exact-SHA SUCCESS; read CI logs 328/328 each Debug and Release (9 BuildingBlocks, 72 SyncContracts, 162 Architecture, 85 PostgreSQL Integration; zero failed/skipped). No new production tests or entrypoint were implemented. Source-grounded audit identified absent production authority minting, absent delivery, absent real provider adapter, disabled-source admission gap, and missing execution lease.

**Binding design corrections before implementation:** (1) A fail-closed stub verifier / `IAuthenticatedPrincipal` port does not constitute a working or legitimate authority issuer. Establish actual customer enrollment, signed or otherwise securely authenticated grant issuance/verification with revocation and tenant/source/capability scope. Preserve internal TenantContext minting; no public unrestricted factories or fake issuers. (2) Lease lifetime/ownership must span provider operation and transaction, including multi-instance central execution and crash cleanup; a session-bound PG advisory lock is insufficient if a different pooled connection holds it or releases it prematurely. Prove this with two-worker PG integration. (3) Disabled-source fail-closed check belongs in a shared Application admission/AccountingSources authorization seam, not merely Worker, so future API cannot bypass; preserve ownership-before-provider order, test negative. (4) Provider API/protocol and WordPress link remain unverified; do not register mock as production. (5) Beware local config/Windows service account/DB row alone cannot prove tenant authority. (6) Do not call W4-A skeleton with nonworking fake verifier a completed Worker.

**Next:** obtain user decision on how trustworthy local installation is enrolled and who provisions its verified tenant/source identity; separately obtain real Asan integration specification/access. A narrow **shared disabled-source admission improvement** could be independently proposed as a bounded non-delivery hardening task if source-grounded impact and compatibility are confirmed, but not an excuse to prematurely implement the entire Worker. **W4 delivery remains BLOCKED; ADR-011 Proposed.** W1–W3 ACCEPTED, W5 pending.

---


## FT-REBASELINE-W3-APPLICATION-GUARD-CLEANUP-001 — architect independent acceptance (2026-10-13)

**ACCEPTED for W3 bounded Application/guard cleanup, NOT production delivery/security.** Independently fetched final implementation `115e7998b52a501297b83d2c2711b71eed03a498`; master matched at review. Reviewed exact commit delta (18 files: 10 removed, 1 added, 7 modified). All source changes are removal of unused BuildingBlocks/Application/Diagnostics/Errors, Domain Clock and its two corresponding API-only test files; one new `QueryPreviewNoWriteTests`; revisions to csproj and architecture tests. No Application handler implementation or provider/persistence changes; no schema/migration. Linux Actions [38311155692](https://github.com/mrnikiemami-code/farataraz/actions/runs/38311155692) **SUCCESS** at exact final SHA, independently verified job logs **328/328 Debug and 328/328 Release** (BuildingBlocks 9, SyncContracts 72, Architecture 162, **85 real PostgreSQL Integration** per configuration), zero failed/skipped. Reduced total 331→328 because 8 tests of deleted, unused public scaffolding were removed and 5 architecture tests added. No security test weakening evidenced in reviewed diff; removal of test-covered but unused public APIs is accepted strictly within the reviewed repository (potential untracked external consumers are not provable).

**Concrete W3 acceptance:** `RunCustomerSyncCommand` remains the only durable write use case; `SynchronizeCustomersQuery` retained read-only preview. New tests check no write/persistence constructor/field dependencies and successful dispatch with no write-store/checkpoint-store/DbContext registrations. This establishes current implementation's no-write dependency behavior, not a universal runtime side-effect proof for unknown future providers. `PhysicalStructureTests` now anchors actual Domain via `typeof(SyncCheckpointState).Assembly` and separately tests Contracts; `ReferenceGraphTests` and `DependencyDirectionTests` similarly disambiguate; fictitious flat Infrastructure example removed; real on-disk Domain→Application negative architecture-policy fixture added. Shared admission helper deliberately not introduced; retaining per-handler explicit checks avoids obscuring admission order and follows current guards.

**Remaining:** `structure.md` §§1–9 and historical ADR status require architect-only reconciliation (AD-6); Domain's declared but unused MasterData csproj reference is minor review debt, not a W3 gate. ADR-011 real tenant authority producer, provider adapter, executable delivery, cross-tenant negative E2E and FT-DATA-003 changed/voided policy all BLOCK production W4. **DO NOT auto-implement W4** on fake identity/provider assumptions; next architect action is a narrow W4 security/entrypoint dependency decision and documentation reconciliation. W3 implementation is accepted without certifying production readiness.

---


## FT-REBASELINE-W2-DOMAIN-POLICY-001 — independent acceptance (2026-10-12)

**W2 ACCEPTED, non-production architectural reconstruction only.** Independently verified implementation `60d40ea8ef901d57166ad9f3709b35646feb4f75` equals GitHub master at review: 5 changed files (one cohesive pure `IngestionSyncPolicy` Domain type, 17 new policy tests, Domain→Contracts inward project reference, Application handler delegated C5/C6/C8/C11, executor RECOVERY). No migrations, schema changes, other production change, weakened guard or edits to architect-owned records. GitHub Actions [38259364212](https://github.com/mrnikiemami-code/farataraz/actions/runs/38259364212) SUCCESS at exact source SHA; independently examined job logs confirming **331/331 Debug and 331/331 Release**: 17 BuildingBlocks + 72 SyncContracts (55 prior + 17 new) + 157 Architecture + 85 real PostgreSQL Integration per config, none failed/skipped. Source inspection confirms Domain no I/O/EF/MediatR/clock/tenant/provider calls, original handler maintains admission order, authorized provider resolution, transaction and observed-version CAS, existing public exceptions and request/response form; negative parity covered by unchanged 85 PG tests. Accepted only for these demonstrated boundaries, not full production authorization.

**Residual/next wave:** bool restart input is deliberate small API, not reason to invent intent framework; C7 orchestration and C10 defensive page check remain Application. Changed/voided policy `FT-DATA-003` remains OPEN. W3 scope: preserve preview Query as no-write; remove only truly dead abstractions; repair misleading D11 architecture test anchors using real Domain and existing project paths; keep guards fail-closed and tests/PG green. Trusted principal/worker authority `FT-SEC-004`, actual delivery/provider `FT-SEC-007`, ADR-011 remain BLOCKED. W3 not accepted or started by this entry.

---


## FT-REBASELINE-W1-ARCHITECTURE-TRUTH-001 — independent acceptance (2026-10-12)

**G0 W1 AUDIT ACCEPTED (documentation and review ONLY).** Independently fetched implementation-audit deliverable commit `ac16b254abbcf62a377ccfcbc916fd3cd11e584e` and confirmed its only changes are `docs/architecture/ARCHITECTURE-W1-AUDIT-2026-10-12.md` + executor-owned `RECOVERY.md`. Linux GitHub Actions [38253194288](https://github.com/mrnikiemami-code/farataraz/actions/runs/38253194288) completed SUCCESS at exact SHA; job logs independently show Debug **314/314** and Release **314/314**, each 17 BuildingBlocks + 55 SyncContracts + 157 Architecture + 85 real PostgreSQL Integration; zero failed/skipped. W1 contains source-level inventory of all 12 production projects, D1–D12 findings, C/Q/I decision taxonomy, seven missing delivery/trust edges, ADR inconsistencies, target project graph and finite W2–W5 plan. No implementation change and no production certification.

**ARCHITECT DECISION:** Retain repo; controlled reconstruction rather than deletion/rewrite. Existing graph is acyclic and bounded foundational PostgreSQL behavior is tested; no irreconcilable ownership, systemic untestability or measured cost superiority of rewrite established. W1 finding statuses are accepted as bounded audit hypotheses where appropriate, NOT every alleged risk as a proven runtime defect. Specifically Query is read-only preview, not second write authority; thin Domain does not justify speculative aggregates; failure-taxonomy relocation is semantic-contract-compatible pending ADR wording clarification. **AD-1…AD-8 architect rulings recorded in** [ARCHITECTURE-W1-ARCHITECT-DECISIONS-2026-10-12.md](../architecture/ARCHITECTURE-W1-ARCHITECT-DECISIONS-2026-10-12.md), commit `fc4aa8f`. W2 authorized only as a separately scoped Domain-purity/behavior-parity task. No fresh implementation SHA after `8d8b75b`. Production trusted identity/worker authority (ADR-011), provider, delivery, end-to-end authorization, changed/voided policy FT-DATA-003 remain OPEN/BLOCKED. Architect G1 design disposition does not waive them.

---


## FT-ARCH-GUARD-LOCK-003 — architect independent acceptance (2026-10-12)

**ACCEPTED (structural/project-dependency scope, NOT production certification).** Independently checked GitHub implementation commit `8d8b75b616bdb10e8708178c16c0f5975e333476`; `master` matched it at review. Linux Actions [38248596386](https://github.com/mrnikiemami-code/farataraz/actions/runs/38248596386) completed SUCCESS at the exact SHA. Job logs showed **314/314 Debug** and **314/314 Release**, each including 17 BuildingBlocks, 55 SyncContracts, 157 Architecture and 85 real-PostgreSQL Integration tests (zero failed/skipped). The diff touches tests and documentation only. `ArchitecturePolicy.cs` reuses the existing project dependency graph and applies layer constraints to every `src/**/*.csproj`. `ArchitecturePolicyTests.cs` runs the same enforcement over real inventory and isolated on-disk forbidden-reference graphs (Domain→Infrastructure, Application→Infrastructure, BuildingBlocks→module, AccountingSources→Ingestion.Domain), as well as an allowed-contract graph. Earlier string-only fixture removed. Full ADR/source-level semantic enforcement is not inferred from passing graph tests.

**Explicit limits / follow-up:** This closes the **foundational cross-module project dependency and negative-fixture architecture-guard gap**, not the full security or production gate. Handler enforcement is source-scanning and structural, not a guarantee of business-operation behavior; cross-tenant ownership-before-provider, provenance, CAS/rollback and page/restart semantics remain subject to real PostgreSQL behavioral tests. Production trusted-context producer FT-SEC-004 / ADR-011, real provider/caller/delivery, source-record freshness/changed-voided policy FT-DATA-003, raw-SQL schema drift, run-state typed behavior, obsolete IApplicationUseCase, and structure.md §1–9 documentation drift remain open or require separate disposition. Old `typeof(SyncRequest).Assembly` used in two structural tests now resolves to Contracts and should eventually be corrected to explicit target assemblies without relaxing guards. No broad claim that all architecture concerns are certified.

**Next safe direction:** Update architect continuity journal and recovery pointer, then prioritize production-trust design gates or one bounded documented data-integrity item according to approved roadmap. No work task is automatically issued. Architect owns this ledger and journal; executor owns RECOVERY.

---


## FT-ARCH-CONTRACT-BOUNDARY-002 — independent conditional acceptance (2026-10-12)

**CONTRACT EXTRACTION ACCEPTED CONDITIONALLY; ARCHITECTURE GUARD CERTIFICATION REMAINS OPEN.** Independently verified master at `1cac293d562a5a471be761a0121feb9c4cd8e727`. New `Ingestion.Contracts` references BuildingBlocks + MasterData only; `AccountingSources.Domain` now references Ingestion.Contracts rather than Ingestion.Domain. Contract-facing sync types, source models and failure taxonomy moved, Domain retains remaining internal domain types. Final-SHA Linux CI [38245865478](https://github.com/mrnikiemami-code/farataraz/actions/runs/38245865478) is SUCCESS, Debug **307/307** and Release **307/307**, including 150 architecture tests and 85 real-PostgreSQL integration tests per configuration; no failures/skips. Accept the cross-module reference correction as a bounded result only.

**OPEN: guard proof and architecture policy gaps.** `ModuleContractsBoundaryTests.Negative_fixture_distinguishes_forbidden_domain_from_allowed_contracts` tests only a string classifier, NOT a mutated actual csproj dependency. `Every_production_project_is_classified` enumerates known filenames but does not by itself prove every new project is covered by dependency-policy assertions. Extend executable graph tests to all current/future modules, prove via temporarily injected prohibited actual project references or equivalent checked-in invalid fixture that tests fail, and ensure real assembly/package references/handler location/Host composition remain guarded. Note `Ingestion.Domain` currently does not reference `Ingestion.Contracts`; future domain behaviors must not duplicate shared contracts. Validate sync taxonomy placement against ADR-010 §11 as a documented architectural disposition, not a silent override. The executor exceeded the 45-minute HARD STOP (~60 minutes), a governance failure; do not claim a compliant PASS. Fix/finish architecture lock before next W3 feature. Production trust-producer, provider adapter/delivery, raw-SQL drift, run-state and data policy remain OPEN.

---


## FT-ARCH-FOUNDATION-REPAIR-001 Phase 2 — independent partial acceptance (2026-10-12)

**PHASE 2 ONLY ACCEPTED; WHOLE TASK PARTIAL.** Independently inspected implementation `d97b3445539dbfadcc063024bce64068e3e21025` and verified master equals that commit, sibling physical directories `AccountingSources.Domain`, `AccountingSources.Infrastructure`, `AccountingSources.Composition`, updated project paths/guard paths, and removal of nested Domain `Compile Remove` exclusions. Linux CI [38244400931](https://github.com/mrnikiemami-code/farataraz/actions/runs/38244400931) matches SHA and passed Debug/Release **303/303** each (17 BuildingBlocks, 55 SyncContracts, 146 Architecture, 85 PostgreSQL integration; zero failures). No new negative architecture fixtures or Contracts assembly in this slice. Existing Domain csproj still directly references `Ingestion.Domain`; assembly names remain unchanged. No certification of complete modular boundaries, MediatR guard completeness, documentation alignment or production readiness.

**Next priority:** bounded extraction of actual shared synchronization Contracts to remove AccountingSources→Ingestion.Domain while keeping real Domain rules in Domain and capability port ownership with AccountingSources, with explicit contract-ownership map and negative graph tests. Do not rename assemblies as a cosmetic prerequisite. Remaining guard coverage, source drift, trust producer, production delivery and data policy items remain OPEN. Executor must not edit this architect-owned ledger.

---


## FT-W3-R3 final completion contract — independent acceptance (2026-10-12)

**ACCEPTED FOR BOUNDED, NON-PRODUCTION W3-R3 ENGINE CORRECTNESS.** Independently inspected implementation commit `eb42e71c4ec289baf45bb00d67ccef2eefc27d88` on master, including `RunCustomerSyncCommand`, Application handler, terminal/restart PostgreSQL tests and provider resolver cancellation handling. CI [38238605815](https://github.com/mrnikiemami-code/farataraz/actions/runs/38238605815) independently verified Debug **303/303**, Release **303/303** (17 BuildingBlocks + 55 SyncContracts + 146 Architecture + 85 PostgreSQL Integration each; zero failures/skips). `CustomerSyncIntent.Continue` rejects Completed checkpoints before provider/write; `Restart` is explicit and rejects caller cursor, starts fresh with originally observed checkpoint-version CAS; Active resumes, absent starts first sync; valid page completion controls terminal state; OCE propagates. This resolves the specific M5/R3 completion contract blockers noted above, not all production gates. No production provider/worker/auth, changed/voided policy, or complete architectural guard certification implied.

**NEXT REQUIRED GOAL (USER DIRECTIVE): ARCHITECTURE FOUNDATION STABILIZATION BEFORE FURTHER W3 FEATURES.** Reconcile current physical project/folder/namespace graph against constitution, ADRs, `structure.md` (whose W1 sections are stale), and `AGENTS.md` architect guard directive commit `a780a8f`. Independently audit MediatR feature location/ISender, module Contracts/BuildingBlocks boundaries, Infrastructure/Application references, Host/module composition, concrete cross-module dependencies, and executable guards with negative fixtures. First produce evidence-based discrepancy map, then smallest cohesive structural repairs and green full CI. Do not add speculative layers/abstractions or silently relax old guards. Remaining production trust and data policy defects stay OPEN. Executor does not edit this ledger.

---


## FT-W3-ARCH-M5-R3 independent conditional review — 2026-10-12

**PARTIAL / NOT FULLY ACCEPTED.** Implementer commit `d553c2edfeb20ec8658f3ba055f11002e6d0110e` merged at `fe7310b9f69ce2665010688f6b2371705a872036`; GitHub CI [38237577911](https://github.com/mrnikiemami-code/farataraz/actions/runs/38237577911) independently checked Debug **297/297** and Release **297/297** with zero failures/skips. ProviderId was removed from command and is now fetched from persisted AccountingSource provider configuration behind a trusted-scoped resolver; checkpoint State Active/Completed was added with migration and optimistic CAS. Security and durability improvements are present, but production activation remains blocked.

**MUST FIX before declaring W3-R3 complete:** current `RunCustomerSyncHandler` treats a persisted Completed checkpoint as null for resume and silently launches a new sync when called without cursor; a caller-provided cursor on Completed is also allowed through to provider. Define explicit restart intent, prohibit implicit restart and stale-cursor replay, and verify denied invocations cause zero provider calls and writes. Terminal transition must validate coherent page shape (IsComplete vs NextCursor); currently `NextCursor == null` alone marks Completed. Stale comments saying terminal checkpoint is retained without write must be removed. PostgreSQL negative tests are required.

**Additional audit:** Provider resolver ignores input tenant (uses trusted scope), with bare catch swallowing all exceptions including cancellation; preserve cancellation propagation and fail-closed trusted-tenant agreement. Consider exact typed error for infrastructure failures. **Governance:** executor reported ~68 minutes > 60-minute hard stop, record violation and require checkpoint-and-stop rather than prolonging next tasks. No defect marked VERIFIED. W2 non-executable accepted; W3 engine non-production only.

---


## FT-ARCH-REMEDIATION M3/M4 independent acceptance — 2026-10-12

**M3/M4 ACCEPTED FOR APPROVED NON-PRODUCTION APPLICATION ORCHESTRATION SCOPE.** Independent GitHub source review of `134d84a09cd5bb2c30274b90a50d92aa2c73e15a` verified `RunCustomerSyncHandler` and command in Ingestion.Application, original Infrastructure coordinator removal, scoped M1/M2 ports and trusted tenant/scope agreement before ownership lookup/provider/write. Adapter session invalidated in `finally` on success and failure. CI [38236380648](https://github.com/mrnikiemami-code/farataraz/actions/runs/38236380648) verified Debug **288/288**, Release **288/288**, zero failed/skipped (17 BuildingBlocks, 55 SyncContracts, 146 Architecture, 70 PostgreSQL Integration per configuration).

**Still OPEN:** W3 terminal-page durable completion (stale cursor retained; no terminal marker); caller-supplied `ProviderId` provenance authority; trusted production delivery boundary ADR-011; actual provider adapter/caller; changed/voided record policy. Existing lexical ownership guard only supplies a necessary structural check. No production authorization implied.

---



## FT-ARCH-REMEDIATION M2 independent review — 2026-10-12

**M2 ADAPTER + GUARD REFACTOR ACCEPTED FOR NON-PRODUCTION ARCHITECTURAL SCOPE, WITH FOLLOW-UP SECURITY FIX REQUIRED.** Independent GitHub review of `2d1c8687a71a2831a65afaf9c84f5759a0fad5e8` confirms new `EfIngestionWriteStore`, scoped DI for both M1 ports, inward Infrastructure → Application reference, and guard redesign. Linux CI [38233817379](https://github.com/mrnikiemami-code/farataraz/actions/runs/38233817379) completed **Debug 285/285, Release 285/285** (17 BuildingBlocks, 55 SyncContracts, 146 Architecture, 67 PostgreSQL Integration each; zero failures/skips). No production execution/delivery gates closed.

**M3/M4 mandatory fixes:** `EfIngestionWriteStore.ExecuteInTransactionAsync` currently calls `session.MarkCompleted()` only after `IngestionUnitOfWork.ExecuteAsync` returns successfully; if work/commit fails, a captured session remains callable. Invalidate in `finally`, and test captured session after exceptions, rollback, cancellation. OwnershipPathAuthorizationGuard's per-file lexical `AssertTrusted(` + `IsOwnedByAsync(` check is necessary but insufficient to prove order and authorization outcome; insist on actual Application use-case ordering and real-PostgreSQL negative tests (untrusted, not-owned, ownership lookup error all yield zero provider calls/writes), and test direct port-bypass risks. Keep provider authority and terminal checkpoint semantics OPEN. No lowering tests/guards.

---



**Canonical path:** `docs/planning/OPEN-DEFECTS.md`  
**Created:** 2026-10-09  
**Scope:** W2 persistence foundation, W2-R1 tenant-isolation repair, W3 preflight, FMCA guards and CI.  
**Status:** W2 persistence foundation **CERTIFIED (non-executable approved scope)**; W3 **CONDITIONALLY AUTHORIZED for bounded engine development only**. Production execution/delivery remains blocked by explicit admission, transaction integration and changed-record gates below.

## Architect W2 acceptance decision — 2026-10-11

**W2 PERSISTENCE FOUNDATION: CERTIFIED FOR ITS APPROVED NON-EXECUTABLE SCOPE. W3 IMPLEMENTATION: CONDITIONALLY AUTHORIZED, LIMITED TO APPROVED INGESTION-ENGINE FOUNDATION, NOT PRODUCTION ACTIVATION.**

Reviewer independently inspected GitHub master `a82501fcea72f05dddcad64fa1e4b9269438ee35`, run [38161542330](https://github.com/mrnikiemami-code/farataraz/actions/runs/38161542330) full job log: Ubuntu/native PostgreSQL 16, Debug **250/250** and Release **250/250** (137 Architecture, 55 SyncContracts, 17 BuildingBlocks, 41 PostgreSQL Integration), **zero failures/skips**. Earlier R13–R18 code, integration tests and guard diffs reviewed in the architect handoff. Detailed per-ID evidence is retained below, including non-closed dependencies.

**Scope of acceptance:** W2 repository/schema/migration/tenant-scoped persistence, checkpoint CAS, Infrastructure-owned transactional unit of work, CI portability/architecture guards. This is *not* a claim that the ingestion engine, real provider, authenticated tenant producer, or runtime delivery path already exists.

**W3 start gate (mandatory, BEFORE production execution):**
1. ADR-011 remains **Proposed**; real authenticated principal/background admission belongs to an explicitly approved trusted delivery boundary. W3 may develop non-operational engine internals but must not mint authority from client input, start tenant jobs or expose delivery endpoints before this boundary is approved and independently tested.
2. The W3 writer must obtain a trusted tenant and verify source ownership **before** entering `IngestionUnitOfWork.ExecuteAsync`. Denied/unknown/cross-tenant/lookup-error cases must do zero provider calls and zero writes.
3. Records, run state and checkpoint progression must use the same scoped `IngestionDbContext` transaction; stale CAS and faults roll back the entire step; no cursor advancement ahead of durable records.
4. `FT-DATA-003`: W2 accepts first-delivery-canonical dedup only. W3 MUST decide changed/voided same-identity records freshness semantics (ADR-005) and add PostgreSQL negative tests before enabling incremental/changed-record ingestion. Fail closed if unsupported.
5. Module/Host composition must preserve `CompositionSeamPolicy`, tenant authority guards, real PostgreSQL tests and green Debug+Release Linux CI. No W3 write/endpoint feature may be declared production-ready without a separate independent review.

**Decision basis:** `FT-SEC-004`, `FT-SEC-007`, and the writer portion of `FT-DATA-002` are explicit deferred **implementation gates**, not silently VERIFIED. `FT-DATA-003` is accepted **only for limited W2 duplicate semantics** and remains a mandatory W3 freshness decision. Deferral is not a waiver for production activation.

---

## W3-R1 architect review — 2026-10-12

**Bounded one-page ingestion engine foundation ACCEPTED at `4457b90cad2d3ae294d4e15a068c4c84ee51fe40`**, subject to explicit R2 correctness review. Linux Actions [38210392126](https://github.com/mrnikiemami-code/farataraz/actions/runs/38210392126) was independently inspected: Debug **260/260**, Release **260/260** (17 BuildingBlocks, 55 SyncContracts, 138 Architecture, 50 real PostgreSQL integration), no failures or skips. This is **not** acceptance of production delivery.

**R2 gates identified in source:** Coordinator observes a checkpoint but passes the original `SyncRequest` to provider without demonstrated persisted-cursor resume/scope validation; terminal page persists no next cursor; source-record identity lookup uses nullable fingerprint as both missing-record and possible null-data sentinel. Require focused negative PostgreSQL tests and smallest bounded repairs before adding worker/endpoints. Security reminder: lexical ownership guard checks interface-name presence, not execution semantics; production admission requires end-to-end proof.

---

## W3-R2 architect ruling — 2026-10-12

**R2 bounded implementation ACCEPTED** at commit `747ba0160dc00287e9c0826866034d04d5068ea4`; independent CI [38221476883](https://github.com/mrnikiemami-code/farataraz/actions/runs/38221476883) **267/267 Debug and 267/267 Release**, no failures/skips. Persisted-cursor resume, W1 scope validation, conflicting cursor rejection, nullable-fingerprint row-existence separation reviewed.

**Terminal-page ruling:** RETAIN of a prior continuation cursor after `IsComplete=true` is **temporary non-production behavior only**, not acceptable as a final resume contract. A future invocation must not silently treat historical continuation as an active resume. **R3 must establish explicit durable completion semantics**, using existing schema/contract where feasible, with CAS and atomic transaction, race/crash/replay tests. Do not invent implicit cursor deletion, reset or restart without an ADR decision; avoid conflating completion of one bounded page/run with completion of the entire synchronization.

Other production gates (`FT-SEC-004` authentic tenant authority, `FT-SEC-007` delivery admission, `FT-DATA-003` changed/voided freshness policy) remain open. W3 limited development only; production execution NOT authorized.

---

## FT-ARCH-REMEDIATION M1 architect acceptance — 2026-10-12

**M1 PORT CONTRACTS ACCEPTED (design scope only).** Independent GitHub source review at `cb3bcdf4bbc29c94bf395cd4ad08a3037484e136` verified Application-owned `IIngestionWriteStore`, `IIngestionWriteSession`, `IIngestionCheckpointStore`, `IngestionRecordProbe` and typed `IngestionRunOutcome`; no runtime adapter, Coordinator replacement, schema or DI modification. Linux CI [38231845438](https://github.com/mrnikiemami-code/farataraz/actions/runs/38231845438) independently checked: Debug **271/271**, Release **271/271** (17 BuildingBlocks, 55 SyncContracts, 57 PostgreSQL Integration, 142 Architecture), zero failures/skips. No existing defect is closed.

**M2 authorization is limited to a thin Infrastructure adapter over existing W2 repositories and IngestionUnitOfWork**; no orchestration move, guard weakening, production caller, schema/migration, authentication, or adapter/provider wiring. Require real PostgreSQL integration equivalence for atomic commit, rollback, stale CAS, cancellation, tenant scope, nullable fingerprint, and idempotency. The port shape alone does NOT establish trusted authority or transaction semantics. Existing runtime path and `OwnershipPathAuthorizationGuard` remain unchanged in M2; migration of admission guard belongs to separately reviewed M3/M4.

FT-ARCH-REMEDIATION broader work and W3-R3 remain gated pending further architect review.

---

## Ownership and acceptance authority (mandatory)

**Accountable reviewer and ledger maintainer: ChatGPT acting as FaraTaraz architecture reviewer in the user's conversation.** OpenCode/Cursor/implementation agents are implementers, **not** independent approvers. The reviewer must personally inspect changed GitHub source, diffs, relevant negative guards/tests and CI evidence; decide whether each issue is VERIFIED, still OPEN, or BLOCKED; and **personally update this file on `master`** with the decision, SHA, evidence and date. An implementer report of PASS, local tests, or a self-declared “verified” status cannot close an issue.

Implementation agents may propose new findings and provide evidence, but **must not mark defects VERIFIED or silently remove them**. If the reviewer is unavailable, leave the item OPEN or FIXED — UNVERIFIED and record that independent review is pending. The reviewer must reconcile this ledger at each architectural review and before any W2 acceptance/W3 authorization. A new chat should read `RECOVERY.md` and this ledger, fetch latest GitHub state, and continue ownership; no claim of autonomous background monitoring or automatic updates between conversations is implied.

## Operating rules (mandatory)

1. Read this ledger together with `AGENTS.md`, `docs/planning/RECOVERY.md`, `docs/planning/CURRENT-STATE.md`, the architecture constitution, accepted ADRs and existing ROADMAP/DELIVERY-PLAN **before every task**. This is a defect tracker, **not a replacement for the approved plan**.
2. Never delete or silently close an item. Change its status to **FIXED — UNVERIFIED** when code is committed; change to **VERIFIED** only after independent source inspection, relevant negative regression tests, and required CI evidence. Record commit SHA, exact file paths, tests, and reviewer evidence. If a fix regresses, reopen the same ID.
3. Implementers must report defect IDs and evidence in their task output and may update `RECOVERY.md`; the **ChatGPT architecture reviewer owns the authoritative updates to this ledger** on `master`, including status transitions and new findings. Preserve historical findings and links; never assert “no unresolved risks” while OPEN or BLOCKED items exist.
4. **No green-test-only acceptance.** Inspect implementation, callers, trust boundaries, migrations, transaction paths, guard coverage, and tests. A passing local suite does not override failing GitHub CI.
5. Tasks target 20–30 minutes, **HARD STOP 45 minutes**; report PARTIAL with a checkpoint when unfinished. No automatic W3 or next task.
6. Guard regressions must be repaired with a negative test proving the guard catches the prohibited change; never weaken or remove guards to achieve PASS.

## Status vocabulary

- **OPEN**: demonstrated problem or missing mandatory assurance.
- **FIXED — UNVERIFIED**: code changed, independent validation still outstanding.
- **BLOCKED**: dependency/evidence prevents closure.
- **VERIFIED**: source + negative test + relevant CI independently checked.
- **ACCEPTED RISK**: requires explicit architect decision, ADR and documented scope; not equivalent to VERIFIED.

## Defect register

| ID | Priority | Status | Evidence / problem | Required closure |
|---|---|---|---|---|
| FT-SEC-001 | CRITICAL | VERIFIED — 2026-10-11 (W2 FOUNDATION) | Tenant-scoped `SyncRunRepository.FinishAsync` fixed at `67750c6`; independently reviewed W2 R19 source/test inventory; real PostgreSQL `TenantIsolationNegativeTests` deny cross-tenant or missing trusted scope. Linux CI `38161542330` at `a82501f` passed all 250 Debug and 250 Release tests. This verifies scoped repository behavior, not the deferred authenticated producer. | Reopen if a production entry point bypasses scoped tenant admission; W3 end-to-end admission gated separately. |
| FT-SEC-002 | CRITICAL | VERIFIED — 2026-10-11 (W2 FOUNDATION) | Tenant-scoped `SourceRecordRepository.ExistsAsync/InsertOrUpdateAsync` fixed `67750c6`; real PostgreSQL negative tenant isolation, including no unauthorized writes; independently reviewed R19 inventory and Linux CI `38161542330`, 250/250 both configurations. | W3 writer must use verified scoped repository within approved admission/UoW; no authorization inferred from caller TenantId. |
| FT-SEC-003 | CRITICAL | VERIFIED — 2026-10-11 (W2 FOUNDATION) | Tenant-scoped `SyncCheckpointRepository.GetAsync/SaveAsync` at `67750c6`; real PostgreSQL missing/cross-tenant negative tests and CAS tests; Linux CI `38161542330`, 250/250 Debug+Release. | W3 orchestration must preserve scope and CAS observed-version; FT-DATA-002 end-to-end remains gated. |
| FT-SEC-004 | CRITICAL | BLOCKED — DELIVERY AUTHORITY REQUIRED | W2 fail-closed trust boundary reviewed R16; no production trusted tenant producer, ADR-011 remains Proposed. Non-executable W2 foundation accepted; not operational authentication. | Before production delivery, accept ADR-011 and implement genuine authenticated principal/background authority with negative tests; cannot be represented as W2 VERIFIED. |
| FT-SEC-005 | HIGH | VERIFIED — 2026-10-11 | Both module composition roots use `TryAddScoped<DatabaseTenantScope>` (`cda5495`/`ab5c688`); `TenantScopeCompositionTests` cover both registration orders and missing/untrusted context; CI `38161542330`, 250/250 both configurations. | New delivery DI must not pre-register an attacker-controlled trusted scope; FT-SEC-004 producer boundary remains BLOCKED. |
| FT-SEC-006 | HIGH | VERIFIED — 2026-10-11 (LEXICAL GUARD ONLY) | `TenantBoundaryGuards` negative scanner tests cover forbidden non-Infrastructure DbContext references, fake infrastructure paths and interpolated strings; reviewed R19 evidence; Linux CI `38161542330` 137 architecture tests each configuration. | Lexical prevention is not PostgreSQL row-level security. Preserve fail-closed tenant scope; audit new W3 entry points. |
| FT-SEC-007 | HIGH | W3-R1 IN-PROCESS ADMISSION IMPLEMENTED — PRODUCTION GATE OPEN | Architect independently reviewed commit `4457b90`: `IngestionRunCoordinator.ExecuteAsync` invokes `tenant.AssertTrusted()` and `IAccountingSourceOwnership.IsOwnedByAsync` before capability/provider calls or Ingestion UoW. PostgreSQL `IngestionRunCoordinatorTests` provide nine scenarios, including admission denials with zero provider calls/writes. Linux CI `38210392126` independently inspected: 260/260 Debug and Release, zero skips/failures. Coordinator has NO production delivery/worker caller, so end-to-end trusted admission remains unverified. `OwnershipPathAuthorizationGuard` is lexical and can be satisfied by simply mentioning the ownership interface, not semantic enforcement. | W3 production activation BLOCKED until real trusted principal/background execution admission and DI/resolved caller path proves ownership denial end-to-end; audit every writer, no false VERIFIED claim. |
| FT-DATA-001 | HIGH | VERIFIED — 2026-10-11 (CHECKPOINT CAS) | `7c9bc6f` carries caller-observed version into public `SaveAsync`, rejects stale updates and racing first writers in real PostgreSQL `SyncCheckpointConcurrencyTests`; checked R19 inventory and Linux CI `38161542330` 250/250 each. | W3 writer must pass original observed version; no silent checkpoint retries. |
| FT-DATA-002 | HIGH | W3-R2 RESUME/IDENTITY VERIFIED — TERMINAL CHECKPOINT GATE OPEN | Independent review of `747ba0160dc00287e9c0826866034d04d5068ea4`: `ResolveResumeRequest` restores persisted cursor with W1 scope validation and denies conflicting caller cursor; `SourceRecordRepository.ProbeAsync` separates record existence from nullable fingerprint. Reviewed real PostgreSQL integration additions; Linux CI run `38221476883` full logs: 267/267 Debug and Release (57 real PostgreSQL integration), zero skipped/failures. Terminal `IsComplete=true` currently RETAINS old continuation checkpoint with no completion marker, risking replay on next invocation; no end-to-end delivery. | R3 gate: choose explicit persisted terminal state that is CAS-protected and atomic with record/run writes, distinguish historical cursor from resumable cursor, provide crash/retry/race negative PostgreSQL tests. Current retain behavior accepted only as non-production interim. Do not activate worker/endpoints. |
| FT-DATA-003 | MEDIUM | W3-R1 CHANGED RECORD FAIL-CLOSED — FRESHNESS DESIGN OPEN | Coordinator `4457b90` compares stored fingerprint against incoming fingerprint and throws UnsupportedChangedRecordException on changed payload, retaining first-delivery-canonical W2 behavior; real PostgreSQL negative test passes in Linux CI `38210392126`. Fingerprint `null` is also used by ReadFingerprintAsync as no-record sentinel; investigate nullable-fingerprint identity collision before claiming sound duplicate/change handling. | W3 freshness/version/void policy requires ADR decision before provider change-support activation; ensure identity existence and nullable fingerprint are distinguished; use negative tests. |
| FT-CONFIG-001 | HIGH | VERIFIED — 2026-10-11 | Connection-string fallback removed (`0bc574b`), Infrastructure composition and design-time factories fail closed; `ConnectionConfigurationFailClosedTests` include absent/whitespace/explicit settings; Linux CI `38161542330` passes. | Review future environment/configuration providers without introducing default credentials. |
| FT-CI-001 | HIGH | VERIFIED — 2026-10-11 | Native PostgreSQL on Ubuntu runner replaces Windows service-container arrangement. GitHub Actions run `38154728752`, head `9f9603e996b29736a77846d21229cf0f4a5325b9`: independently inspected full job logs; Debug 117+55+17+33=222/222; Release 117+55+17+33=222/222, zero skipped or failed. | CI environment/platform issue resolved. https://github.com/mrnikiemami-code/farataraz/actions/runs/38154728752 |
| FT-CI-002 | HIGH | VERIFIED — 2026-10-11 | Previous ECR DNS failure `38152401479` bypassed by native Ubuntu PostgreSQL 16.15 (without services/Docker registry); actual run `38154728752` on SHA `9f9603e` completed all provisioning, Debug build/test, Release build/test, whitespace steps successfully. Independent full logs show 222/222 in each configuration, 0 skipped; xUnit2031 warnings remain nonblocking. | Resolved with real Linux and PostgreSQL integration evidence. https://github.com/mrnikiemami-code/farataraz/actions/runs/38154728752 |
| FT-GUARD-001 | HIGH | VERIFIED — 2026-10-11 | Layer and Host architecture guards with negative injection cases and module-owned `.Infrastructure` restrictions, further hardened by CompositionSeamPolicy `2911b39` and Linux reference normalization `9f9603e`; Linux CI `38161542330` architecture 137/137 both configurations. | Review additional future modules and composition edges before extending allowlists. |
| FT-GUARD-002 | HIGH | VERIFIED — 2026-10-11 (LEXICAL GUARD ONLY) | `TenantBoundaryGuards` synthetic negative proofs cover forbidden trusted-context mint and DbContext references, comment/string handling, interpolations, path bypass; Linux CI `38161542330` 137/137 architecture both configurations. | Not a substitute for a real authenticated principal or data-plane RLS; W3 must supply negative end-to-end auth tests. |
| FT-DOC-001 | MEDIUM | VERIFIED — 2026-10-11 (CURRENT STATE) | R19 `a82501f` puts current acceptance audit at RECOVERY top; historical older HEAD/security claims remain archived under explicit acceptance override, not authoritative current state. Architect acceptance checkpoint supersedes previous header with W2/W3 status. | Keep historical record; do not treat previous optimistic claims as verified security. |

## New model audit findings — architect triage 2026-10-11

Source: user-supplied 16-finding architecture audit at reported HEAD `91db8b3`. These are **implementer/model reports**, not independently verified GitHub source facts. Remote master remains `91db8b3`; local commits `0bf63c9` and `9f7ac3e` are not yet available remotely. D-2 maps to existing FT-SEC-004; D-3 to FT-DATA-002; D-12 to FT-DATA-003. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

| ID | Priority | Status | Evidence / problem | Required closure |
|---|---|---|---|---|
| FT-ARCH-001 | HIGH | VERIFIED — 2026-10-11 (COMPOSITION SEAM, NOT RUNTIME DELIVERY) | Reviewed Host module-owned Composition implementation (`0bf63c9`), strict `CompositionSeamPolicy` module-relative boundary and cross-module negative tests (`2911b39`), `HostCompositionSmokeTests`, and Linux project-reference normalization (`9f9603e`); CI `38161542330` 250/250 Debug+Release. Provider adapter delivery and trusted tenant producer not yet bound in production. | W3/delivery must complete actual provider composition and authenticated execution, then verify end-to-end DI resolution. No claim that current Host runs ingestion. |
| FT-SEC-008 | HIGH | VERIFIED — 2026-10-11 (TRACKING PROTECTION) | `.gitignore` rule from `9f7ac3e`; R19 executor evidence `git check-ignore -v opencode.json` -> `.gitignore:22`; `git ls-files opencode.json` empty. Reviewer verified remote change and absence of this named file in tracked production diffs; this verifies ignore/tracking policy, not proof secrets never existed in any history. | Never stage local credential files; audit outgoing diff on each push and rotate any accidentally disclosed key. |
| FT-CI-003 | HIGH | VERIFIED — 2026-10-11 | Independently inspected pushed cross-platform `SourceTreePaths.IsBuildOutput` repair (`a5b1a9a`) and regression evidence, plus subsequent Linux project-reference path normalization (`9f9603e`) and adapter guard fail-closed discovery (`23aefb60`). GitHub Actions run `38156371471`, head `23aefb60`, full independently retrieved logs: Ubuntu PostgreSQL 16.15, Debug and Release each Architecture 128, SyncContracts 55, BuildingBlocks 17, Integration 33 = **233/233**, **0 failed/0 skipped**. This verifies Linux portability and runnable guard coverage, not tenant trust or transaction correctness. | Closed for confirmed D-6 portability failure. Future regressions must retain negative guard tests and CI. https://github.com/mrnikiemami-code/farataraz/actions/runs/38156371471 |
| FT-ARCH-002 | MEDIUM | ACCEPTED DEFERRAL — 2026-10-11 | Deferred Contracts extraction and module layout are explicit in accepted ADR-009 and structure.md §10; no demonstrated W2 invariant violation in R19 audit. | Revisit only when a real cross-module dependency or change requires Contracts; not W2 closure blocker. |
| FT-ARCH-003 | LOW | TRIAGED — NO VERIFIED W2 BREACH | R19 reviewed D-7/D-9 through D-16: D-7 maps FT-DATA-002, D-12 maps FT-DATA-003, scope registration mapped FT-SEC-005; other design concerns currently lack demonstrated breach and remain improvement candidates. | Create a separate defect only with concrete reproducer/invariant violation; no speculative W2 refactor. |

**Executor reconciliation — FT-W2-R10-SAFE-GIT-RECONCILIATION (2026-10-11):** the local branch had proposed its own `FT-ARCH-001` (D-1) and `FT-ARCH-002` (D-2) rows; the architect triage above **supersedes both IDs** (D-1 → `FT-ARCH-001`, D-2 → `FT-SEC-004`), so the duplicate local rows were removed during the safe merge to prevent ID collisions. Local implementation evidence preserved: D-1 commit `0bf63c9` (module-owned `*.Composition` seams + Host wiring); D-2 recorded as `docs/architecture/adr/ADR-011-Trusted-Tenant-Boundary.md` (Status: **Proposed**, pending architect acceptance; no authentication invented); D-4 commit `9f7ac3e`; D-6 commit `a5b1a9a`. **No defect status was changed by this reconciliation.**

## Verified source evidence and history

- W2 initial implementation: `10eb8c9435f9958c3ce3b38bfcec82719827995e` — **NOT CERTIFIED**.
- W3 composition preflight guard: `cd82c2c4469de69bdec72e7e67d0cff1d35ff482`.
- W2-R1 tenant repository changes + 6 negative tests: `67750c6998e8223fb5246951a1388581342df6b7` (158 local tests **reported**, not independently rerun).
- CI runner correction: `01b93c414bfe5fed2223d740b929e1ba9bd93cd4`.
- DI fixes: `cda5495ca7596dd96a9dc185d426f1320dca57e6`, `ab5c6883f3000558905b5ec866260acd4d774d92`; six DI theory cases added `24597a36bb3bd6d03a5f674ca554d802da7cdb8d`.
- CI fixed port: `5ce2b75d4101dd64595c0eefae429c44d0eaf0ce`. As inspected, its GitHub Actions run failed in Debug tests. Never call CI green without checking current run.
- Independent inspection was of actual GitHub commit patches, relevant complete repository files and workflow job statuses; no local .NET/PostgreSQL tests were executed by the reviewer.

## Next actions (follow the existing approved roadmap)

1. Diagnose and fix **FT-CI-002** with evidence; preserve all FMCA guards.
2. Complete **FT-SEC-004**, **FT-SEC-005**, **FT-GUARD-002** trusted boundary and DI tests; do not authorize W3.
3. Close **FT-SEC-006**, **FT-SEC-007**, **FT-CONFIG-001** and correct misleading architectural claims.
4. Address **FT-DATA-001/002/003** with explicit accepted persistence semantics and crash/concurrency tests.
5. Independently inspect final source and tests, verify CI, then separately decide W2 architectural acceptance. Only after acceptance authorize a bounded W3 slice.

## Architect recovery reconciliation — 2026-10-10

Remote source checkpoint `bfa4c8c89085bdbb9525b28a5dc499bb3c59a206` differs from implementer `a31aa254` only in journal/ledger documentation (two commits). No defect closed or removed. Latest CI run [38001093552](https://github.com/mrnikiemami-code/farataraz/actions/runs/38001093552), job `114059059330`, failed Initialize containers; checkout/build/tests skipped. Latest root cause not independently read from logs. FT-CI-002 remains OPEN, FT-CI-001 FIXED — UNVERIFIED. Next task `FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT` targets FT-SEC-007 production oracle/DI/caller evidence with links to FT-SEC-006/FT-GUARD-002; all statuses unchanged. FT-DOC-001 remains OPEN despite operational RECOVERY top-line reconciliation; historical/ADR/planning claims still require correction. See latest ARCHITECT-JOURNAL entry for ordered priorities and limits. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

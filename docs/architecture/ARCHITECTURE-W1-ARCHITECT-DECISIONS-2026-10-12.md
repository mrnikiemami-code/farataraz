# FaraTaraz — Architect W1 Acceptance and Fixed W2–W5 Decisions

**Date:** 2026-10-12  
**Status:** W1 G0 ACCEPTED; G1 TARGET APPROVED WITH CONDITIONS; W2 IMPLEMENTATION MAY BEGIN ONLY WITH A BOUNDED EXPLICIT TASK. NOT production authorization.  
**Reviewed SHA:** `ac16b254abbcf62a377ccfcbc916fd3cd11e584e` (documentation-only).  
**Reviewed report:** [ARCHITECTURE-W1-AUDIT-2026-10-12.md](ARCHITECTURE-W1-AUDIT-2026-10-12.md) (source inventory 12 projects, C1–C15/Q1–Q4/I1–I8, D1–D12, E1–E7).  
**CI:** [38253194288](https://github.com/mrnikiemami-code/farataraz/actions/runs/38253194288) SUCCESS at exact SHA; independently checked job logs: 314/314 Debug and 314/314 Release (17 BuildingBlocks, 55 SyncContracts, 157 Architecture, 85 PostgreSQL Integration, zero skipped/failed).

## Binding decisions (AD-1 through AD-8)

- **AD-1 ACCEPTED: Retain shared failure taxonomy in `Ingestion.Contracts`.** ADR-010 §11 'Domain contract' means domain-significant, provider-independent **contract semantics**, NOT a requirement to reside in `Ingestion.Domain` assembly. Contracts may describe transient/permanent/cancellation classifications and provider-facing exceptions; no retry policy, business decision or Infrastructure-only transport implementation belongs there. Do not split/move taxonomy in W2 or change public types. Clarify ADR-010 with an explicit prospective amendment rather than silently rewriting historical decision.
- **AD-2 DEFERRED/BLOCKED (W4):** no trust producer, identity API or `TrustedExecution` assembly approved. ADR-011 remains Proposed. Obtain a real authenticated-principal or authenticated background-job authority specification (issuer, verification, lifetime, tenant/source binding, failure handling) before choosing minting seam or delivery runtime. Host has zero authority. Do not use fake/test contexts for production.
- **AD-3 ACCEPTED:** keep `SynchronizeCustomersQuery` as **read-only preview**; preserve two distinct use cases but only one authoritative durable write path. Document and test no persistence side effects. Shared admission may be factored only if it measurably reduces repeated policy without obscuring call ordering.
- **AD-4 ACCEPTED IN PRINCIPLE (W3, after usage reconfirmation):** remove demonstrably unused abstractions such as `IApplicationUseCase`, dead diagnostics/error model and unused clock *only* after checking project, tests and public API dependency. Do not remove common types that downstream consumers require or introduce a clock just to replace `DateTime.UtcNow`.
- **AD-5 ACCEPTED COMPATIBILITY EXCEPTION:** `AccountingSources.Domain` folder retains current bare assembly and namespace until a real functional reason justifies a breaking rename. Keep explicit guard mapping; do not add broader exceptions.
- **AD-6 ARCHITECT OWNED:** ChatGPT architect reconciles `structure.md` §§1–9 and historical ADR-009 implementation status with actual code; executor must not amend normative documents. Avoid claiming Endpoints exist.
- **AD-7 DEFERRED:** retain persisted run-state strings for now. No renaming or enum migration without verified legacy value map and backwards-compatible migration/tests.
- **AD-8 ACCEPTED RISK WITH GATE:** raw SQL and EF schema declaration duplication is temporarily allowed; existing real PostgreSQL integration covers exercised statements. Require source-column/schema parity evidence before W5 certification; add a schema guard only if an untested concrete gap is established.

## W2 domain extraction — exact scope control

The W1 matrix labels C5/C6/C8/C10/C11 as candidate pure rules. **These are candidates, not a mandate to manufacture five services or migrate every validation.** Before moving code, capture same-input/same-exception/same-output behavior. Transfer a pure decision only if it is independent of I/O, tenant trust, provider invocation, time, MediatR and persistence. C5 (restart/cursor validity), C6 (Completed terminal intent), C8 (persisted-cursor conflict), and C11 (identity exists with equal/null fingerprint vs changed) are leading candidates for small cohesive Domain policy. **C10 is already contract-enforced by `SyncBatch`; retain Application defensive check if needed and avoid duplication.** C7 remains Application orchestration informed by Domain checkpoint semantics. Preserve `UnsupportedChangedRecordException`, `SyncAlreadyCompletedException`, `InvalidSyncCursorException`, `SyncCursorScopeMismatchException` public behavior and exception surfaces. Domain may reference `Ingestion.Contracts` for shared record/cursor types if allowed by graph, without introducing a reverse reference. No persistent schema changes, no new packages or speculative aggregates.

## Binding plan and gates

1. **W2 Domain and Contracts:** tiny coherent domain-decision API plus narrow tests and unchanged 85 PostgreSQL parity tests; same errors, ordering, no additional provider/writes. Retain taxonomy in Contracts. Exact-SHA CI and independent acceptance before W3.
2. **W3 Application simplification/guards:** keep Command write vs Query preview; remove only proven dead markers; fix D11 wrong Domain anchors and nonexistent project example; document preview/structure. Preserve behavior.
3. **W4 Delivery/security (conditionally blocked):** require independently accepted ADR-011 concrete real identity/job authority, then real adapter and `ISender` delivery; no fabrication of authentication. This is a distinct **production gate**.
4. **W5 End-to-end acceptance:** verify real delivery against PostgreSQL and all negative paths if W4 unblocked, and schema mapping/typed-state disposition plus documentation. If external authorities remain absent, classify W4 BLOCKED and close only **non-production architecture reconstruction**, NEVER label complete production delivery.

**Goal:** bounded controlled reconstruction, **not deletion/full rewrite**. 11.5–21 hours prior planning estimate was preliminary, not a performance warranty. A wave may need coherent subtask(s) with independently reviewed acceptance, but no uncontrolled refactor loop.

**Architect ownership:** ChatGPT updates `docs/planning/OPEN-DEFECTS.md`, `ARCHITECT-JOURNAL.md`, architecture normative docs. Executor updates `RECOVERY.md`. No automatic next task is granted by this decision document.

# FaraTaraz — Architecture Rebaseline and Rebuild Decision Gate

**Status:** ARCHITECT REVIEW BASELINE — implementation frozen pending evidence-based disposition; not a new accepted ADR.
**Date:** 2026-10-12
**Owner:** Independent ChatGPT architect
**Evidence baseline:** implementation `8d8b75b616bdb10e8708178c16c0f5975e333476` (314/314 Debug and Release; Linux CI run 38248596386). Later architect documentation commits are not production changes.
**Scope:** Existing `src/`, constitution, ADR-001–011, `AGENTS.md`, structure.md, executable guards and PostgreSQL integration tests.

## Why this review exists

The user requested an end to incremental architecture churn and a reasoned choice between **controlled reconstruction** and **full rewrite**. This document freezes the decision process; it does not authorize deleting source code, implementing an endpoint, introducing a new security assembly or moving domain behavior. An accepted graph and passing tests are necessary but do not prove a coherent business domain or executable production delivery.

## Non-negotiable target architecture

1. **Modular monolith:** real business capabilities own their behavior and data. No generic engine, framework, speculative projects or technical-layer proliferation.
2. **Domain:** business invariants and decisions independent of use-case orchestration, tenant authority acquisition, persistence, providers, time and transport. A Domain assembly need not be large; its existence must be justified by actual domain rules.
3. **Contracts:** minimal stable, provider-neutral types crossing module/provider boundaries. Contracts may describe domain-significant outcomes and failure categories without becoming the owner of business policy. Do not duplicate types to make a layer appear populated.
4. **Application:** MediatR commands/queries and handlers; admission sequencing, authorization checks, provider resolution, bounded workflow, transactions via inward ports, cancellation and results. Domain policy is invoked from here where there is genuine reusable policy. One authoritative write execution path; a query must not silently duplicate write authority.
5. **Infrastructure:** persistence/EF/Npgsql, transactional and CAS enforcement, authoritative source configuration lookup, and implementations of inward ports. PostgreSQL constraints remain an independent safety net. Raw SQL is acceptable if tested against migrations/schema; replacing it with EF is not automatically an improvement.
6. **AccountingSources:** capability/provider declarations and ownership ports; adapter implementations remain outside. Adding a new provider must not require changes to business core.
7. **Delivery and trusted execution:** a real authenticated HTTP/worker/MCP entry point must establish authority from verified identity/job claims, bind TenantContext through an explicitly reviewed narrow trust seam, verify ownership before provider or write, then dispatch Application via ISender. Host remains composition-only. No public arbitrary TenantContext minting; **do not assume a new TrustedExecution assembly is necessary**. ADR-011 is Proposed and requires an explicit trust model and independent approval before implementation.
8. **Tests:** real project graph and negative fixtures, behavior-level tenant/ownership/provenance/CAS/rollback tests with PostgreSQL, adapter conformance and end-to-end delivery authorization when a delivery exists. Green tests never substitute for code review.

## Evidence-based disposition of the external 12-finding review

| Finding | Decision / scope | Next evidence or action |
| --- | --- | --- |
| D1 No production delivery/trusted producer | **CONFIRMED as an intentionally unimplemented production gate, not proof of architectural corruption.** | Design real caller, authority source, scoped minting seam, provider registration and end-to-end negative tests before code. ADR-011 remains Proposed. No speculative TrustedExecution project. |
| D2 Thin Domain; handler owns rules | **PARTIALLY CONFIRMED.** Handler contains page/restart/changed-record checks. Thin Domain alone is not a defect. | Inventory each branch: pure invariant vs application sequencing vs database concurrency. Move only stable pure domain policy with tests; do not transplant authorization or orchestration. |
| D3 Failure taxonomy in Contracts | **ARCHITECTURAL DISPOSITION: retain provisionally.** ADR-010 §11 says *Domain contract*, not necessarily the `Ingestion.Domain` assembly. Provider-facing exceptions and categories are legitimately shared. | Clarify ADR wording before any relocation; no duplicate taxonomy, no policy/retry logic inside Contracts. Formal ADR amendment required if semantic behavior changes. |
| D4 Raw SQL / EF schema duplication | **RISK ACCEPTED TEMPORARILY, NOT A REWRITE TRIGGER.** | Verify migration/schema mapping and SQL coverage by integration tests; add drift detection only if a real gap exists. |
| D5 Two handlers / dead IApplicationUseCase | **AUDIT REQUIRED.** | Determine if query is a non-persisting preview vs duplicate writer; remove unused marker if verified unused, without broad refactor. |
| D6 Multiple capability representations | **AUDIT REQUIRED.** | Demonstrate an actual new capability addition and identify necessary touchpoints; prefer one source of truth only if duplication creates defects. |
| D7 AccountingSources bare assembly/namespace | **KNOWN COMPATIBILITY EXCEPTION, NOT A FUNCTIONAL BLOCKER.** | Avoid cosmetic rename without migration cost/benefit. |
| D8 Unused Clock/diagnostics/error primitives | **AUDIT REQUIRED.** | Search actual production consumers and decide remove/use individually. Inject clock only when time is a domain invariant or test nondeterminism is material. |
| D9 String run-state | **LOWER-PRIORITY DATA INTEGRITY RISK.** | Map persisted values, migrations and compatibility before typed change. |
| D10 MasterData records only | **NOT PROVEN DEFECT.** | Identify an actual violated canonical-identity invariant; no speculative aggregate. |
| D11 Stale assembly anchors / fictitious path | **CONFIRMED.** | Fix tests targeting `typeof(SyncRequest).Assembly` (now Contracts) and the nonexistent flat Infrastructure example; test real Domain assembly and real paths. |
| D12 Parallel error models | **AUDIT REQUIRED.** | Map distinct boundaries (platform error vs provider failure vs persisted outcome) before consolidation. |

## Single-pass rebuild vs reuse decision

**Current decision: preserve repository and perform a bounded architecture rebaseline; DO NOT DELETE or rewrite from scratch yet.** There is independently verified PostgreSQL transaction/CAS/tenant behavior and an executable architecture policy. The cited defects do not currently demonstrate that the existing foundation is more expensive or less safe to repair than a rewrite.

A full rewrite can be authorized only after an explicit comparison proves: (a) irreconcilable module ownership or dependency cycles that cannot be repaired locally; (b) systemic business invariants untestable without replacing foundations; (c) measured rewrite scope/cost lower than controlled reconstruction; and (d) a migration and regression-parity plan preserving tenant isolation, data compatibility and idempotency. None has yet been established.

**No piecemeal implementation tasks until this decision gate is complete.** First produce a code-level ownership and behavior matrix for every production project and both sync handlers, including actual method/branch classifications, entry-point DI resolvability, trust authority and all 12 findings. Reconcile stale `structure.md` §§1–9 and ADR-009 historical status in one documentation-only architecture decision pass, without silently changing normative ADRs. Then approve **one coherent, finite reconstruction plan** with acceptance gates and a defined end state. Implementation tasks must be derived from that approved plan, not from the latest reviewer's defect ordering.

## Acceptance gates for the rebaseline

- **G0 — Inventory truth:** actual physical project/namespace/dependency graph, existing delivery and DI registrations, business-policy ownership, and accepted ADR conflicts independently evidenced.
- **G1 — Design lock:** one approved target graph and decision for each D1–D12; explicit no-change decisions are allowed and justified.
- **G2 — Rebuild plan:** bounded dependency-ordered implementation slices, estimated costs, rollback/compatibility and required tests, with a terminal exit criterion.
- **G3 — Production readiness (later, separately):** real trusted producer + ownership admission + provider adapter + delivery + behavioral negative tests. Never infer G3 from structural CI.
- **G4 — Independent acceptance:** architect inspects exact final source SHA, Linux CI, PostgreSQL behavior and changed ADRs. Only architect updates OPEN-DEFECTS and ARCHITECT-JOURNAL.

## Continuity and execution protocol

Read this document first together with `docs/planning/ARCHITECT-JOURNAL.md`, `OPEN-DEFECTS.md`, `RECOVERY.md`, `AGENTS.md`, constitution and ADR-009/010/011. Verify latest GitHub master before continuing. Previous task `FT-ARCH-GUARD-TRUTH-004` was drafted in chat **but superseded by the user's explicit architecture-rebaseline request**; do not execute it unless the user has already started it, in which case reconcile its actual status first. No automatic executor dispatch, no file deletion, no speculative production authority. The architect owns design and acceptance; executor tasks are issued directly in chat after G0/G1.

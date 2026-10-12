# FaraTaraz — Open Defects and Architecture Acceptance Ledger

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
| FT-DATA-002 | HIGH | W3-R1 ATOMIC ONE-PAGE WRITER VERIFIED — PRODUCTION GATE OPEN | Architect independently reviewed `4457b90` coordinator: run start, records, conditional continuation checkpoint save with observed-version CAS, and run finish all within one IngestionUnitOfWork transaction. Real PostgreSQL integration includes fault, stale CAS and cancellation rollback; Linux run `38210392126` 260/260 Debug+Release, 0 skips. Provider I/O occurs outside transaction; no production caller. Resume-from-persisted-checkpoint behavior NOT yet proven. | R2 must validate persisted cursor resume/scope and continuation protocol, including completion/retries and conflicts. Production execution remains blocked pending trusted delivery plus end-to-end verification. |
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

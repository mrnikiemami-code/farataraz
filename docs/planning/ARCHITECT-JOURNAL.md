# FaraTaraz — Architect Continuity Journal

## CURRENT CHECKPOINT — W1 REBASELINE ACCEPTED / W2 BOUNDED NEXT — 2026-10-12

**User's enduring mission:** Finish FaraTaraz modular architecture via **five decision-gated waves**, avoid repeated unplanned fixes. **W1 G0 audit independently ACCEPTED** at `ac16b254abbcf62a377ccfcbc916fd3cd11e584e`, exact-SHA Linux CI [38253194288](https://github.com/mrnikiemami-code/farataraz/actions/runs/38253194288) SUCCESS, **314/314 Debug and 314/314 Release** (85 PostgreSQL + 157 Architecture + 55 SyncContracts + 17 BuildingBlocks per configuration). Executor changed only audit document and RECOVERY. **Architect G1 decisions AD-1…AD-8 saved** in `docs/architecture/ARCHITECTURE-W1-ARCHITECT-DECISIONS-2026-10-12.md` created at `fc4aa8f`. Architect accepted controlled reconstruction/reuse instead of deleting or rewriting repository. 12-project source inventory, D1–D12 decisions, C1–C15 / Q1–Q4 / I1–I8 classifications and 7 missing execution edges in `ARCHITECTURE-W1-AUDIT-2026-10-12.md`.

**Immediate next implementation task to issue directly in chat (NOT MCP):** W2, bounded `Ingestion.Domain` pure policy extraction from `RunCustomerSyncHandler`, preserve exact existing exceptions, provider/write call order and PostgreSQL parity; no speculative types; retain failure taxonomy in `Ingestion.Contracts` (AD-1); C10 defensive page validation can remain where it is. Run Debug+Release+PG+Linux CI, independent acceptance gate before W3. No W2 report yet. **No production delivery allowed:** ADR-011 remains Proposed; actual auth/worker authority and real provider are unsupplied, FT-DATA-003 freshness policy unresolved. W3 Query remains read-only preview; D11 guard repair there. Do not invent identities or start W4 early.

**Context-recovery path:** read latest TOP of this journal, `OPEN-DEFECTS.md`, `RECOVERY.md`, `AGENTS.md`, constitution, `ARCHITECTURE-REBASELINE-2026-10-12.md`, W1 audit and architect decisions, ADR-009/010/011; fetch actual master, compare latest implementation and report, exact CI. Architect owns ledger/journal; executor owns RECOVERY. Do not assume the architect's docs commits are new product implementations.

---


## ARCHITECTURE REBASELINE STARTED — 2026-10-12

**User directive:** Stop repeated piecemeal fixes and evaluate controlled reconstruction vs complete rewrite. Independent architect opened [ARCHITECTURE-REBASELINE-2026-10-12.md](../architecture/ARCHITECTURE-REBASELINE-2026-10-12.md) at `084a2f4e4260ee5589899ad7f6a7718a5231a56f`. This is a **review baseline, not an accepted ADR or implementation authorization**. Preserve all existing source and database artifacts. It records target layer authority, preliminary evidence-based dispositions D1–D12, G0 inventory/G1 design/G2 finite rebuild-plan gates, and explicit G3 production-security gate. Decision at this point: **no destructive rewrite justified; controlled reconstruction provisionally preferred pending complete inventory and cost comparison**.

**Evidence inspected:** live `RunCustomerSyncHandler` contains restart/completion/page/changed-record checks; `PhysicalStructureTests` has two Domain-labeled reflection tests anchored to `typeof(SyncRequest).Assembly` (now Contracts); `LayerDependencyGraphTests` has fictitious flat Infrastructure path; `structure.md` early sections assign moved sync types to Domain; ADR-010 §11 uses the words 'Domain contract' for failure taxonomy; ADR-011 remains Proposed. Not all 12 external-review allegations have been independently verified. **No production code changed.** Supersede proposed FT-ARCH-GUARD-TRUTH-004 task unless already in execution; reconcile status first. Next action: independently finish G0 ownership/behavior/DI/entry-point inventory, then write and approve one G1 target and G2 finite plan, not ad hoc D11/D2 tasks. Architect writes journal and defect ledger; executor RECOVERY only.

---


## CURRENT ARCHITECT RECOVERY CHECKPOINT — 2026-10-12 (after FT-ARCH-GUARD-LOCK-003)

**AUTHORITATIVE HEAD AT REVIEW:** implementation `8d8b75b616bdb10e8708178c16c0f5975e333476`; after architect's OPEN-DEFECTS update, fetch latest `origin/master` before acting. Independent Linux CI [38248596386](https://github.com/mrnikiemami-code/farataraz/actions/runs/38248596386) SUCCESS at exact implementation SHA; **314/314 Debug and 314/314 Release**, including **157 architecture** and **85 real PostgreSQL integration** tests each, zero failed/skipped. Architecture guard lock task FT-ARCH-GUARD-LOCK-003 **ACCEPTED for the bounded structural/project dependency scope**. Implementation diff only `ArchitecturePolicy.cs`, `ArchitecturePolicyTests.cs`, `ModuleContractsBoundaryTests.cs`, `structure.md`, `RECOVERY.md`. Policy inventories 12 production projects, covers BuildingBlocks/Domain/Contracts/Application/Infrastructure/Composition/Host/Adapter and checks actual project reference graph plus on-disk negative fixture graphs; source-only MediatR handler scan remains limited to source-level structural evidence.

**Three-step architecture stabilization trajectory:** 1. `FT-ARCH-FOUNDATION-REPAIR-001`: AccountingSources physical sibling directories, implementation `d97b344`, PARTIAL overall but Phase 2 accepted with 303/303 per configuration. 2. `FT-ARCH-CONTRACT-BOUNDARY-002`: Ingestion.Contracts extracted without eliminating Ingestion.Domain, source `1cac293`, 307/307 per config, final-SHA CI [38245865478](https://github.com/mrnikiemami-code/farataraz/actions/runs/38245865478) SUCCESS; AccountingSources now references Contracts rather than Ingestion.Domain. 3. `FT-ARCH-GUARD-LOCK-003`: implementation `8d8b75b`, source and CI independently checked; architecture project-reference lock ACCEPTED. No provider authority or production execution opened by these repairs.

**ACCEPTANCE SCOPE:** W2 persistence foundation previously certified **only for non-executable foundation**; W3 ingestion implementation and bounded internal tests accepted per relevant prior ledger decisions, but **NOT production-ready**. Remaining gates: trusted tenant authority producer from actual authenticated principal/background worker (`FT-SEC-004`, ADR-011), admission enforcement/authorization in real delivery (`FT-SEC-007`), production provider adapter/caller, changed/voided source-record policy (`FT-DATA-003`), raw SQL vs EF schema drift, run-state semantics, `IApplicationUseCase` cleanup, outdated §1–9 structure documentation, and two old assembly-reflection tests targeting Contracts via `typeof(SyncRequest).Assembly`. Do not call these complete or invent production integration.

**IMMEDIATE HANDOFF / CONTEXT LOSS PROTOCOL:** On the next chat/session, read this top checkpoint, `docs/planning/OPEN-DEFECTS.md`, `docs/planning/RECOVERY.md`, `AGENTS.md`, constitution, architecture `structure.md`, and ADR-008/010/011; then fetch current GitHub `master` and latest exact-SHA CI. Compare to the latest executor result before giving more implementation work. Prefer one coherent bounded task and an explicit work budget based on size, not arbitrary split tasks; no forced overtime/no broken partial push. User wants executor task text directly in chat; **do not dispatch via connector/MCP**. ChatGPT architect alone updates `OPEN-DEFECTS.md` and `ARCHITECT-JOURNAL.md`; executor updates `RECOVERY.md`. Avoid large context dump; point to these canonical repo documents for continuity. No new execution task has been issued after `FT-ARCH-GUARD-LOCK-003` as of this entry.

**Current architect decision on ADR-010 taxonomy:** Moving sync failure taxonomy to Ingestion.Contracts preserves a contract-facing Domain contract meaning, but no new ADR approval is asserted. Review ADR-010 §11 explicitly before changing taxonomy again.

---


## CURRENT ARCHITECT RECOVERY CHECKPOINT — 2026-10-12 (after M3/M4 acceptance)

**Authoritative current handoff.** Independently reviewed M3/M4 implementation at `134d84a09cd5bb2c30274b90a50d92aa2c73e15a`; GitHub Actions [38236380648](https://github.com/mrnikiemami-code/farataraz/actions/runs/38236380648) confirmed Debug **288/288**, Release **288/288**, no failures/skips. Acceptance recorded by ChatGPT in `OPEN-DEFECTS.md` at architect commit `5418d63e9b6dffc31ffef8cb4e55608b762e6095`. Source changes: `RunCustomerSyncHandler` in Application now owns admission and orchestration; previous Infrastructure coordinator deleted; M2 adapter session lifetime corrected with `finally`; trusted TenantContext and DatabaseTenantScope agreement before ownership/provider/writes. No production delivery.

**Architecture remediation history:** M1 persistence contracts `cb3bcdf`, independently accepted (architect ledger `42a5b9c`); M2 guard+adapter `2d1c868`, independently accepted with session lifetime follow-up (architect ledger `3e7b297`); M3/M4 `134d84a`, accepted (architect ledger `5418d63`). W2 non-executable foundation remains CERTIFIED; W3 internal bounded engine conditionally authorized only. Do not confuse this with production certification.

**Last task ISSUED, NOT YET REPORTED OR VERIFIED:** `FT-W3-ARCH-M5-R3-PROVENANCE-AND-COMPLETION`, issued after M3/M4: derive ProviderId from authoritative persisted AccountingSource configuration, not caller input; persist explicit terminal completion/non-resumable checkpoint with observed-version CAS and deterministic explicit restart; real PostgreSQL negative tests, migration compatibility; no provider/worker/endpoint/auth. The task may be running locally: NEVER presume completion or clean worktree from this journal. Check current origin/master, local status if available, and latest executor report before any further instruction.

**Remaining gates:** terminal checkpoint/restart and provider provenance (M5 pending); `FT-SEC-004`/ADR-011 trusted production principal or worker producer; `FT-SEC-007` end-to-end production admission; `FT-DATA-003` changed/voided freshness; actual provider adapter and caller; broader Contracts/BuildingBlocks/schema/module layout and capability-neutral extension review. Lexical ownership guard is necessary but not a proof of call order. No fake authority, no production activation, no weakening guards, no invented completion.

**Resume protocol:** read this top checkpoint plus `RECOVERY.md`, `OPEN-DEFECTS.md`, ADRs and AGENTS.md; fetch GitHub master and CI; reconcile executor M5 status; inspect changed code/tests; independently approve or stop. ChatGPT owns architect journal and defect ledger; executor updates RECOVERY only. User wants short end-to-end turnaround to resume W3, NOT shorter or less complete task instructions. Do not dispatch tasks through MCP; provide task text in chat.

---


## ARCHITECT ACCEPTANCE HANDOFF — W2 CERTIFIED / W3 BOUNDED START — 2026-10-11

**Authoritative decision:** Architect certified the **non-executable W2 Persistence Foundation** at reviewed source HEAD `a82501fcea72f05dddcad64fa1e4b9269438ee35` and actual GitHub Actions run `38161542330` (Ubuntu, real PostgreSQL 16, Debug 250/250 and Release 250/250, none skipped). Architect ledger decision commit `f682a615535d80836344213152f76c41bd2557e5`. **W3 is conditionally authorized only for bounded ingestion-engine development, NOT production execution or delivery.**

**Carry-forward mandatory gates:** `FT-SEC-004` real authenticated-principal/background trust authority (ADR-011 still Proposed), `FT-SEC-007` ownership admission BEFORE W3 writer/UoW, `FT-DATA-002` use `IngestionUnitOfWork` for all records/checkpoint/run atomic writes, `FT-DATA-003` decide changed/voided record freshness (W2 first-delivery-canonical only) before enabling relevant providers. All require negative tests and independent approval. Do not claim any runtime path is already integrated. No fake trusted contexts, no production jobs/endpoints/real provider activation until gates satisfied.

**Architect ledger is canonical:** `docs/planning/OPEN-DEFECTS.md` has per-ID review evidence and decisions. Historical reports below that say W2 NOT CERTIFIED/W3 NOT AUTHORIZED refer to **past checkpoints**, not current restricted authorization. Implementation agent must start by fetching latest master, reading ledger+constitution+ADRs, and must not treat certification as removal of W3 production gates.


## Architect independent checkpoint — 2026-10-11 — model audit triage and W2 closure planning

- User supplied a 16-finding architectural report, reportedly at remote `91db8b3`, and an executor report of **unpushed local** commits `0bf63c9` (D-1 Host wiring, 16 files) and `9f7ac3e` (D-4 ignore opencode.json). Local 196 Debug and 196 Release tests and clean working tree are **executor claims**, not architect-verifiable remote evidence. D-1 change exceeded previous explicit architect recommendation to defer implementation; no acceptance or push authorized.
- Independently fetched GitHub `master=91db8b337223a69cc2de39b2fa063daf033cee70` before documentation updates; Actions run `38076703312` concluded failure. Full failing log not yet examined. Thus W2 NOT CERTIFIED, W3 NOT AUTHORIZED.
- Architect updated ledger with report-derived OPEN triage entries FT-ARCH-001, FT-SEC-008, FT-CI-003, FT-ARCH-002/003, preserving existing D-2/3/12 tracking. **These are hypotheses pending code review, not verified findings.** Agent RECOVERY updated with local/remote divergence and manual handoff; never overwrite local commits or force push.
- Next bounded task: `FT-W2-R9-CROSSPLATFORM-GUARDS-001` (D-6) with cross-platform regression/negative tests, no unrelated changes, no push; user receives prompt directly in chat, not through MCP. Before task, executor checks AGENTS, constitution, latest ledger/RECOVERY, local status, both local commits and remote ahead/behind. Independent reviewer inspects output and updates ledger; subsequent tasks address CI and W2 security/data blockers.


**Owner:** ChatGPT (independent architecture reviewer)  
**Purpose:** Durable, evidence-based memory for the architecture reviewer across chat/context loss.  
**Created:** 2026-10-09.  
**Canonical companion documents:** `docs/planning/RECOVERY.md` (agent handoff), `docs/planning/OPEN-DEFECTS.md` (authoritative defect statuses), `docs/planning/CURRENT-STATE.md`, ROADMAP, DELIVERY-PLAN, architecture constitution and ADRs.

## Recovery instructions for ChatGPT

1. Read this journal **and** `RECOVERY.md` and `OPEN-DEFECTS.md` before evaluating any implementation report or authorizing work.
2. Independently fetch current `origin/master`, changed source files, tests, and relevant CI job steps; compare with the last checkpoint. The journal's SHA is a snapshot, not an assertion that remote has not advanced.
3. Distinguish **implementer claims**, **directly inspected evidence**, **unknowns**, and **independent acceptance decisions**. Do not mark an item VERIFIED from a report alone.
4. After each substantive review or architecture decision, ChatGPT must update this journal itself on `master` with date, SHA, task ID, claims received, source/CI evidence, decision, unresolved issues, and next exact safe action. Update `OPEN-DEFECTS.md` itself when defect status or findings change.
5. If a session stops before a checkpoint is written, **reconcile actual GitHub state and the latest implementer report** rather than assuming an unrecorded step succeeded. No background monitoring is implied.
6. Never store credentials, tokens, private customer records, or full shell logs here. Prefer short paths, commit IDs, test counts and links.

## Architect checkpoint — 2026-10-09, after FT-RECOVERY-AFTER-CONTEXT-001 report

**Last independently observed GitHub master HEAD:** `5de5e63e2c931c2c6f9526b9ce984d04d3e47132` (before this journal commit).  
**Certification:** W2 **NOT CERTIFIED**; W3 **NOT AUTHORIZED**. W0 certified historically; W1 not certified.  
**Current implementation task:** `FT-W2-R2-BOUNDARY-GUARDS` was interrupted/stalled and **has not been accepted**. Recovery report says no local changes and task never started; that claim is **not independently verified against the user's Windows worktree**.  
**Next authorized task:** `FT-RECOVERY-REMOTE-RECONCILE-002` — 10-minute safe fetch/fast-forward and re-read recovery + defects; no code edits, tests or W3.

### Reports received from OpenCode

- `FT-W2-R1-TENANT-ISOLATION`: implementer reported PASS, ~40 min, commit `67750c6998e8223fb5246951a1388581342df6b7`, 158 local passing tests (152 prior + 6 negative PostgreSQL tests). **Architect acceptance withheld**: source review found additional trust-boundary, DI, raw DbContext, ownership and concurrency risks.
- `FT-RECOVERY-AFTER-CONTEXT-001`: implementer reported RECOVERED, clean Windows working tree, local and origin/master both `5ce2b75d4101dd64595c0eefae429c44d0eaf0ce`; claimed no interrupted task changes and that W3 could start. **Architect rejected W3 recommendation**: direct GitHub API showed master already at `5de5e63`, and `OPEN-DEFECTS.md` contains outstanding critical/high findings. Implementer likely had stale remote view; reason unverified.
- OpenCode local model shown as Ornith 1.5 35B-A3B; UI showed `Session compacted · 130.1K in · 1.8K out`, then stalled without task result. Compaction is occurring but stale summary/recovery is unsafe.

### Direct GitHub edits by ChatGPT architect

| Commit | Change |
|---|---|
| `01b93c4` | CI runner Windows → Ubuntu for PostgreSQL service container. |
| `cda5495`, `ab5c688` | Changed duplicate `DatabaseTenantScope` registration to `TryAddScoped` in both Infrastructure compositions. |
| `24597a3` | Added `TenantScopeCompositionTests.cs` for both module registration orders, missing/untrusted contexts. |
| `5ce2b75` | CI PostgreSQL service port `5432:5432`. |
| `3f9344e` | Created `OPEN-DEFECTS.md` with durable defect IDs and closure evidence. |
| `b8e46ae` | Linked defect ledger in `RECOVERY.md`. |
| `9c6daad` | Established ChatGPT as defect-ledger owner and sole independent approver. |
| `3419a6c` | Added architecture ownership lock to `RECOVERY.md`. |
| `5de5e63` | Added context-exhaustion microtask and checkpoint protocol to `RECOVERY.md`. |

### Current CI evidence

- Latest observed GitHub Actions for master `5de5e63`: **FAILED**. Other recent runs also failed. CI is **not green**.
- Previous `01b93c4` run passed container initialization and Debug build but failed Debug tests. Port mapping was then changed in `5ce2b75`, but later runs still failed. **Exact latest failing test/stack trace has not been independently obtained**; do not claim root cause.
- No independent local .NET/PostgreSQL execution by ChatGPT. Reported local 158 PASS is not CI evidence.
- Note: new `TenantScopeCompositionTests.cs` was authored directly via GitHub without compiling locally; compilation and behavior **not yet confirmed**.

### Outstanding blockers and cautions

Consult `OPEN-DEFECTS.md` for all canonical IDs. Highest-risk unresolved: `FT-SEC-004` (trust minting), `FT-SEC-006` (raw DbContext tenant access), `FT-SEC-007` (accounting source ownership), `FT-DATA-001` (checkpoint concurrency), `FT-DATA-002` (transaction boundaries), `FT-CONFIG-001` (hardcoded PostgreSQL credentials), `FT-CI-002` (failing Debug tests), `FT-GUARD-002` (negative trust-boundary guard). W2-R1 fixes `FT-SEC-001/002/003` and DI `FT-SEC-005` remain FIXED — UNVERIFIED, not VERIFIED.

**New process concern:** `RECOVERY.md` still contains historical misleading top-line W3-next/“W2-R1 verified” wording. The later ownership/acceptance override controls, but top-of-file should be reconciled to reduce agent confusion.

### Next exact action

1. Receive the result of `FT-RECOVERY-REMOTE-RECONCILE-002` and verify master is current.
2. Diagnose the **actual** failing GitHub CI test/log, in one short bounded task; don't assume PostgreSQL port is the only problem.
3. Split `FT-W2-R2-BOUNDARY-GUARDS` into microtasks, each 10–20 min, ≤25 min, one concern and focused tests; separate final full-suite/CI gate.
4. ChatGPT independently reviews source and updates `OPEN-DEFECTS.md` plus this journal after each substantive result. No W3 until explicit architectural acceptance.

## Architect review — 2026-10-09 — FT-RECOVERY-REMOTE-RECONCILE-002

**Implementer report received:** RECOVERED; Windows `D:\\FaraTaraz` master clean, fast-forwarded `5ce2b75` → `5de5e63`; `OPEN-DEFECTS.md` and recovery governance present. No implementation/build/test/commit/push. OpenCode enumerated ten OPEN and six FIXED — UNVERIFIED items, and correctly did not authorize W3.

**Independent evidence:** GitHub master observed at `4fa33c0f3e3288c174e86899f858d30f044777a9` before this journal update; architect journal was created at `9689d95` and linked in `RECOVERY.md` at `4fa33c0`, after the implementer's `5de5e63` checkpoint. Thus the implementer's remote SHA was valid for its observation window but is **now behind** current master. Windows working-tree cleanliness/fast-forward is **implementer-reported**, not remotely verifiable. No source changes or tests were claimed.

**Decision:** Recovery report accepted as a truthful read-only handoff, **not** as architectural defect closure. All ledger defect statuses unchanged; W2 NOT CERTIFIED and W3 NOT AUTHORIZED. Do not repeat broad recovery/audit. Next microtask should diagnose the CI Debug-test failure with only relevant log excerpts and a minimal proposed fix. Before any write, fetch and fast-forward the latest master (including journal). The architect owns ledger/journal updates; implementation agents must not certify issues.

**Next task:** `FT-W2-CI-DIAG-001` (10–15 minutes, read-only). Inspect newest failing GitHub Actions job and exact failing test names/exception. No full local suite, no production edits. Report the smallest evidence-backed repair. Stop. Then ChatGPT reviews and issues a separate narrowly scoped fix task.

## Architect CI evidence — 2026-10-09 — FT-W2-CI-DIAG-001

**OpenCode report received:** PARTIAL; claimed exact GitHub Actions job logs unavailable due to 403, reported Docker pull failure and hypothesized test-host abort/fixture issue. No implementation performed. **The test-host hypothesis is refuted by independently obtained full GitHub job logs.**

**Independently fetched authoritative logs via connected GitHub job-log API:**

- Run 37991074354, job 114025072351 (head `4efc2c0`): `docker pull postgres:16` fails three times with **`toomanyrequests: You have reached your unauthenticated pull rate limit`**. Container initialization failed; no code checkout/build/test attempted. This is an observed Docker Hub anonymous pull quota, not proof of generic runner egress failure. Log URL: https://github.com/mrnikiemami-code/farataraz/actions/runs/37991074354
- Run 37986593473, job 114010000534 (head `01b93c4`): containers initialized, Debug build succeeded, **15/15 PostgreSQL integration tests failed**, error `Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:5432`, inner `SocketException: Connection refused`. This was not an unknown test-host abort. The workflow then used dynamic container port `5432/tcp` but fixed `localhost:5432` in the test connection string; later `5ce2b75` switched service mapping to `5432:5432`. The fixed port has **not** been validated by a completed subsequent CI test run. Log URL: https://github.com/mrnikiemami-code/farataraz/actions/runs/37986593473

**Architecture decision:** FT-CI-002 stays OPEN, FT-CI-001 stays FIXED — UNVERIFIED. Do not alter Production code or database fixture based on speculative crash claims. Do not pin the same Docker Hub image by digest as a purported fix for unauthenticated pull quota (digest pinning does not establish quota avoidance). Use a proven/verified image registry or authenticated pull strategy, then obtain full all-green Debug and Release evidence. Do not introduce an unverified third-party container image. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Next narrow task:** Investigate supported official PostgreSQL image distribution or auth approach for GitHub Actions; one workflow-only change after source verification, then inspect fresh CI logs. Reviewer will update defect ledger on evidence, not agent PASS.

## 2026-10-09 — FT-W2-CI-IMAGE-001 external review

OpenCode reported PARTIAL and pushed `233beed0d7e238c0f907bae79f0c9a8d74dd47f5`, switching the PostgreSQL service image to `public.ecr.amazonaws.com/libraries/postgres:16`. GitHub Actions run https://github.com/mrnikiemami-code/farataraz/actions/runs/37992717191 failed at container initialization; no build/test ran. Independent reviewer successfully retrieved job `114030757221` complete logs: three `docker pull` attempts all failed with `dial tcp: lookup public.ecr.amazonaws.com on 127.0.0.53:53: no such host`. **Confirmed DNS resolution failure** (not PostgreSQL code/test, and not evidence that image does not exist). Earlier Docker Hub anonymous quota and dynamic-port test connection errors are separate already-verified causes. Current image choice remains unverified and CI red. FT-CI-002 remains OPEN; W2 NOT CERTIFIED, W3 NOT AUTHORIZED. No production edits or speculative image change authorized. Architect updated OPEN-DEFECTS.md at `44114674`. Next: find verified reliable image distribution or build step, make one limited CI config fix and obtain complete CI verification.

## 2026-10-09 — FT-W2-R2-A-TRUST-BOUNDARY review

OpenCode reports PARTIAL, implementation pushed as `39164cdbe357c23c290fe2b0725ce23f50153f43`, clean local/remote, targeted test results: BuildingBlocks 17/17, architecture filter 24/24, PostgreSQL integration filter 12/12, targeted builds zero warnings/errors. No full suite/CI. Independently retrieved commit and inspected all three patches: `TenantContext` positional public record constructor replaced by internal constructor with get-only properties; `FromAuthenticatedPrincipal` now internal; `BuildingBlocks.csproj` adds InternalsVisibleTo for three named test assemblies; reflection regression test asserts no public constructor or factory. The change removes direct public-API trust minting for other assemblies; reported red/green negative test not independently reproduced. Reflection test checks public API, not real authentication or assembly trust, and friend test assemblies retain access. No authenticated production boundary is demonstrated; implementation reports zero production calls and fail-closed production behavior, which still requires end-to-end validation. **Architect decision: accept narrow mitigation by source inspection, keep FT-SEC-004 OPEN — PARTIALLY MITIGATED; no certification, W2 NOT CERTIFIED, W3 NOT AUTHORIZED.** Defect ledger updated at `f533985`. Next: independent focused audit of production tenant authority creation/wiring and/or next small W2 security slice; don't let CI blockage halt other W2 repairs.

## Context handoff — 2026-10-10 — FT-W2-R2-B-SOURCE-OWNERSHIP

**OpenCode report:** PARTIAL; no source edits or tests; local clean at `a31aa254af17860a0c0a5bf3fd617ca2adf6b0d4`; argued source ownership belongs in Application, not repositories. **Independent GitHub review:** confirmed remote HEAD `a31aa254` at start of review. Inspected `src/Modules/Ingestion/Ingestion.Application/SynchronizeCustomers/Queries/SynchronizeCustomersHandler.cs`: calls `command.Tenant.AssertTrusted()`, then `IAccountingSourceOwnership.IsOwnedByAsync(command.Tenant.TenantId, command.Request.SourceId, ct)` and throws `UnauthorizedSourceException` before provider `SyncAsync` if false. Inspected `tests/FaraTaraz.ArchitectureTests/TenantSourceOwnershipTests.cs`: seven cases use an in-memory fake, assert denial and zero provider calls. Inspected `src/Modules/Ingestion/Ingestion.Infrastructure/Persistence/SyncRunRepository.cs`: StartAsync checks trusted tenant scope and caller tenant equality, not source ownership. **Key uncertainty:** no independent exhaustive audit of all repository callers, actual production ownership oracle/DI, or alternate persistence entry points; tests not rerun. Therefore FT-SEC-007 stays OPEN — ORCHESTRATION CHECK OBSERVED, not VERIFIED; ledger updated at `c3dbdac`. Do not inject a sibling authorization port into repositories without architecture justification. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**User reports context nearing exhaustion. Recovery procedure:** Start next chat with request to read `docs/planning/ARCHITECT-JOURNAL.md`, `docs/planning/OPEN-DEFECTS.md`, `docs/planning/RECOVERY.md`; independently fetch latest master; do not trust stale SHA or agent summary. Next bounded 10–15-minute READ-ONLY task: audit actual production `IAccountingSourceOwnership` implementation/DI and every call to `StartAsync` and `InsertOrUpdateAsync`; report bypass paths and precise evidence. No code change, no full test suite, no W3. After independent review update ledger/journal. In parallel CI still blocked by Docker Hub anonymous quota and ECR DNS failure; don't halt W2 source fixes for CI. No background work implied.


## 2026-10-10 — FT-ARCHITECT-RESUME-003 / next ownership audit

**Independent remote reconciliation:** master was `bfa4c8c89085bdbb9525b28a5dc499bb3c59a206`. GitHub compare from last implementer HEAD `a31aa254af17860a0c0a5bf3fd617ca2adf6b0d4` reports two commits ahead, zero behind; only ARCHITECT-JOURNAL.md and OPEN-DEFECTS.md changed. No new implementation exists to review. Read all three recovery documents, AGENTS, Current State, Roadmap, Delivery Plan, constitution and ADR-008/009/010. Prior handler/repository/test inspection is retained; it was not repeated. No local tests executed or Windows worktree inspected.

**Current CI:** run [38001093552](https://github.com/mrnikiemami-code/farataraz/actions/runs/38001093552), exact head `bfa4c8c`, completed FAILURE. Job `114059059330`: Initialize containers failed; checkout, Debug/Release builds/tests and whitespace guard skipped. Latest failure cause was not fetched from logs; historical Docker Hub quota and ECR DNS evidence must not be presented as newly verified root cause. FT-CI-002 OPEN; no closure of FT-CI-001 or other FIXED — UNVERIFIED items.

**Decision / priority:** preserve every defect and acceptance status. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Next resolve the remaining FT-SEC-007 evidence gap (actual ownership oracle/DI and production persistence callers), linked to FT-SEC-006/FT-GUARD-002. Then handle trusted authority/DI (FT-SEC-004/005), raw DbContext and fail-closed config (FT-SEC-006/FT-CONFIG-001); CI remains a separate blocking repair, without repeating already established diagnosis. Data concurrency/atomicity FT-DATA-001/002 and semantics review FT-DATA-003 follow before final independent acceptance. FT-DOC-001 remains OPEN: operational recovery instructions are reconciled here, but historical claims and other planning/ADR prose still need explicit correction.

**Next authorized task:** `FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT`, READ-ONLY, target 10–15 minutes, stop by 20 minutes. After safe fetch/reconciliation, inspect actual production IAccountingSourceOwnership implementation and all DI bindings; enumerate every production caller of SyncRunRepository.StartAsync / its port and SourceRecordRepository.InsertOrUpdateAsync / its port, including indirect resolution and raw DbContext/SQL alternatives. Report caller → authorization → persistence paths with exact path:line and SHA; distinguish implemented callers from unused foundation APIs and future W3 plans. Determine real bypass / no current caller / unknown; public exposure alone is not proof of an exercised bypass. Do not repeat the already recorded handler/fake-test audit, add repository business authorization, invent auth, edit code/docs, run full suites, commit/push, certify or start W3. Return concise evidence and the smallest separate repair proposal, then STOP. Architect owns authoritative updates.

## 2026-10-10 — FT-W2-R2-B1 ownership-path audit independent review

Implementer read-only audit reports one production `EfAccountingSourceOwnership` implementation, one `AddScoped<IAccountingSourceOwnership, EfAccountingSourceOwnership>` DI binding, zero production callers of `SyncRunRepository.StartAsync` and `SourceRecordRepository.InsertOrUpdateAsync`, and ownership gate in `SynchronizeCustomersHandler`. Independent reviewer fetched master `b64857645e970d43015e000ea60d8889aac29941` and read the actual `EfAccountingSourceOwnership.cs`, `AccountingSourcesInfrastructureComposition.cs`, `SourceRecordRepository.cs`, and previously the handler and ownership tests. Confirmed EF oracle uses trusted `DatabaseTenantScope` rather than supplied tenant, `AnyAsync` checks `(SourceId,TenantId)` and returns false on absent trust/lookup failure; DI registers oracle and shared `TryAddScoped` scope. Repository write checks trusted tenant/caller equality but does not itself verify source ownership. No independent exhaustive production caller search or execution of tests in this review; implementer reports no production repository callers. **Decision: FT-SEC-007 remains OPEN pending durable guard for future write-path authorization and independent caller-inventory confirmation. No speculative Infrastructure business-authorization change.** W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Next short task should verify callers with an automated architectural guard or investigate the next independent security defect. CI still blocked.

## 2026-10-10 — FT-W2-R2-B2 guard review

Reviewed commit `be772f292e6735060c70e0f2a107f14261262e30` independently. Added `OwnershipPathAuthorizationGuard.cs` (335 lines) and RECOVERY handoff; no production changes. Implementer reports 3 focused and 75 architecture tests passing locally; independent CI still blocked. Guard regex matches generic `.StartAsync` or `.InsertOrUpdateAsync` names without semantic symbol resolution; it may flag unrelated methods or miss indirect calls. All `Ingestion.Infrastructure` callers are exempt, including future unauthorized paths. Synthetic red/green tests demonstrate textual detection only. Decision: provisional regression check, NOT sufficient authorization proof. FT-SEC-007 stays OPEN; W2 NOT CERTIFIED; W3 NOT AUTHORIZED. User requests fast resume checkpoint and 15–30 minute related work slices with logical commits, not one commit per slice. Do not start a new task until review handoff is complete.

## 2026-10-10 FT-W2-R3 review

Independently reviewed `f373bd4` and `972e8b9`. Conditional checkpoint upsert and Version mitigate simultaneous writes, but SaveAsync reads expected version immediately before writing, not from the sync operation's original checkpoint observation. A delayed older operation can therefore save an old cursor after a newer operation. The concurrent test accepts either `newer` or `stale`; the forced-version test exercises handwritten SQL instead of SaveAsync. Reported local tests: 24 integration and 75 architecture passed; CI not independently verified. FT-DATA-001 remains OPEN, PARTIALLY MITIGATED, recorded in ledger commit `051fdc5`. Next: define expected-version contract across the sync operation and test delayed stale writer through public API, coordinating with FT-DATA-002. W2 NOT CERTIFIED, W3 NOT AUTHORIZED.

## 2026-10-10 — FT-WAKE-FARATARAZ-E2E-001 verified communication recovery

**Scope:** user-authorized Wake setup/test and durable architect recovery only; no production implementation, test-suite run, defect closure or W3 authorization. Remote master before recording: `601816f2d8998a25ef566076173e72cab51b1cab`.

**Independent evidence:** FaraTaraz persistent event subscription was created in this architect conversation, activation checked through Automation peek and Wake status. Explicit `ornith_bridge/ornith-1.5-35b-a3b-local` is required. User applied the project-only provider configuration copied from bridge-test (including the local credential, never displayed); architect read the resulting config and checked model/baseURL/credential presence. User setup chat restarted the FaraTaraz OpenCode server; no architect restart was performed. A terminal connection error to port 18081 first triggered an automatic architect run, proving error delivery. User then supplied healthy model endpoint and authorized one additional async test.

**Final successful request/reply:** parent `msg_124ab62e0001xlOjzbIRiXBnRi` → assistant `msg_124ab62ec001rrBKHEuP0BTyrS`, exact reply `WAKE_FARATARAZ_OK`, finish `stop`, explicit provider `ornith_bridge`. Event `evt_55def956d04abe3e761d77fcf649c438aa5493e3f1265c68c1045b282d35761b` at `2026-10-10T07:16:29.073Z` automatically invoked this architect conversation. Architect read messages/status and reported `WAKE_RECEIVED` and `WAKE_FARATARAZ_OK`. No user-requested read or polling was used for that success; no active execution was reported.

**Decision:** bounded end-to-end communication test VERIFIED. Accepted async send alone is not success; actual assistant completion plus automatic architect run is required. Persistent subscription retained, no next task sent. Current Automation remains test-only and must not silently become permission to run implementation. Observed expiry `2026-10-11T06:33:01.595Z` must be checked before subsequent work.

**Diagnostics retained:** user local logs confirmed invalid `local` selection and subsequently missing `ornith_bridge` in the running server before restart; independently read later API connection failure to 18081. Repeated `opencode_capabilities → Transport closed` was waived by user for the bounded test; this is not proof the provider is absent. Port-routing discrepancy was observed, not proven causal. Credentials and project config/backups must never enter Git.

**Durable protocol:** recorded at top of RECOVERY.md in commit `944922778965ca3d8b7c81063220eeab92cc0eb5`, superseding historical tool-limit/no-FaraTaraz-subscription claims while retaining history. This journal update changes no defect statuses: W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Next substantive concern remains FT-DATA-001 observed-version contract coordinated with FT-DATA-002, after fresh source/HEAD reconciliation and a separately bounded task.

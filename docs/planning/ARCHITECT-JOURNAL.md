# FaraTaraz — Architect Continuity Journal

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

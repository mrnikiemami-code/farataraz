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

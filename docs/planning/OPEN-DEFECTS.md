# FaraTaraz — Open Defects and Architecture Acceptance Ledger

**Canonical path:** `docs/planning/OPEN-DEFECTS.md`  
**Created:** 2026-10-09  
**Scope:** W2 persistence foundation, W2-R1 tenant-isolation repair, W3 preflight, FMCA guards and CI.  
**Status:** W2 **NOT CERTIFIED**; W3 implementation **NOT AUTHORIZED** until blocking issues are resolved and independently reviewed.

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
| FT-SEC-001 | CRITICAL | FIXED — UNVERIFIED | Original `SyncRunRepository.FinishAsync` updated by run ID without tenant predicate. W2-R1 `67750c6` added trusted scope + tenant-scoped lookup. | Independently verify negative cross-tenant test, actual DI call path, and CI. |
| FT-SEC-002 | CRITICAL | FIXED — UNVERIFIED | Original `SourceRecordRepository.ExistsAsync/InsertOrUpdateAsync` trusted caller TenantId. `67750c6` added trusted-scope enforcement. | Verify negative tests and source ownership/authority checks, integration path and CI. |
| FT-SEC-003 | CRITICAL | FIXED — UNVERIFIED | Original `SyncCheckpointRepository.GetAsync/SaveAsync` trusted caller TenantId. `67750c6` added trusted-scope enforcement. | Verify negative tests, DI path and CI; concurrency tracked separately. |
| FT-SEC-004 | CRITICAL | OPEN | `TenantContext.FromAuthenticatedPrincipal(TenantId)` is publicly callable and `TenantContext` is a public record; origin flag alone is not authentication proof. Actual trusted execution boundary and all call sites are not yet audited. | Trace every production call site and authenticated boundary; prohibit untrusted minting via enforceable guard and tests. Do not invent authentication that is not implemented. |
| FT-SEC-005 | HIGH | FIXED — UNVERIFIED | Both module Infrastructure Composition files registered `DatabaseTenantScope` with `AddScoped` (last registration wins). Changed to `TryAddScoped` in `cda5495` and `ab5c688`; added `TenantScopeCompositionTests.cs` in `24597a3`. | Inspect registrations, verify both module orders, untrusted/missing context and scope lifetime in CI. Confirm externally supplied scope cannot bypass boundary. |
| FT-SEC-006 | HIGH | OPEN | `IngestionDbContext` and `AccountingSourcesDbContext` expose raw tenant-owned DbSets with no global tenant filter or verified PostgreSQL RLS. Composite unique indexes **do not** enforce tenant authorization. ADR-010/RECOVERY database-isolation claims are overstated. | Audit actual production DbContext call paths; enforce trusted-scope access or document an explicitly reviewed compensating boundary; add negative tests/guards and correct docs. |
| FT-SEC-007 | HIGH | OPEN | `SyncRunRepository.StartAsync` and `SourceRecordRepository.InsertOrUpdateAsync` take SourceId but do not themselves verify accounting-source ownership. | Verify mandatory fail-closed `IAccountingSourceOwnership` check at the real orchestration boundary; add negative tests before W3 execution. |
| FT-DATA-001 | HIGH | OPEN | `SyncCheckpointRepository.SaveAsync` uses read-then-write `SaveChanges`; concurrent first writers can violate unique constraint and concurrent writers can regress cursor. | Atomic/concurrency-safe checkpoint protocol and real PostgreSQL race + stale cursor tests; align with accepted ADR. |
| FT-DATA-002 | HIGH | OPEN | Run start/finish, record insert and checkpoint saves commit independently. No proven atomic sync unit-of-work across records, cursor and run state. | Explicit transaction/commit design, crash/rollback tests; no premature checkpoint advance. |
| FT-DATA-003 | MEDIUM | OPEN — REVIEW SEMANTICS | `SourceRecordRepository.InsertOrUpdateAsync` uses `ON CONFLICT DO NOTHING`; same external identity with a changed version is ignored. May be intentional first-delivery policy (ADR-010 d7). | Compare W1 contracts and ADR-010; retain if intentional, otherwise approved version/freshness design and tests. |
| FT-CONFIG-001 | HIGH | OPEN | Both module Infrastructure Composition files retain `DefaultConnectionString` with `Username=postgres;Password=postgres` fallback when config is missing. | Fail closed for missing production connection configuration; keep explicit disposable test config; add guard/test. |
| FT-CI-001 | HIGH | FIXED — UNVERIFIED | Workflow initially used PostgreSQL service on `windows-latest` (container initialization failure). `01b93c4` changed runner to Ubuntu; `5ce2b75` changed port to `5432:5432`. | CI must pass Debug+Release builds and all tests on actual master; review logs if failing. |
| FT-CI-002 | HIGH | OPEN | GitHub Actions for `5ce2b75` **FAILED at Debug tests** (container initialization and Debug build succeeded); Release stages skipped. Local 158 PASS is not independent CI PASS. | Inspect failing job logs, repair actual test/configuration cause, rerun to all-green. Run: https://github.com/mrnikiemami-code/farataraz/actions/runs/37986593473 (older run); check latest run for `5ce2b75`. |
| FT-GUARD-001 | HIGH | FIXED — UNVERIFIED | W3 preflight `cd82c2c` strengthened `IsForbiddenLayer` in `LayerDependencyGraphTests` and `HostAuthorityTests` to recognize module-scoped `.Infrastructure` projects; reported inject/prove/revert. | Independently verify architecture tests and negative injection evidence in green CI. |
| FT-GUARD-002 | HIGH | OPEN | No proven durable guard prevents untrusted code from creating trusted TenantContext or bypassing authorized repository path through raw DbContext. | Add meaningful negative architectural/source guard and targeted regression; review false-positive/false-negative behavior. |
| FT-DOC-001 | MEDIUM | OPEN | `RECOVERY.md` top-level “Current verified HEAD” still points to `53a66c5` and claims DB-enforced tenant isolation via uniqueness; some historical W3 preflight prose says “safe foundation”. | Correct current checkpoint, distinguish historical reports from verified security, record outstanding defects and link this ledger. |

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

# RECOVERY — FaraTaraz / FMCA

**Checkpoint recorded:** 2026-10-09
**Current task:** FT-FMCA-ARCHITECTURE-LOCK-001 — completed; next planned task W2 Persistence Foundation.
**Last verified implementation baseline:** `2dd5e0dac5fcd978a074dfedc9954b3d4e0e1167` (master, architecture lock).
**Note:** Documentation-only commits may follow this baseline. Always fetch and compare current `origin/master` before executing. Do not assume this checkpoint SHA is current HEAD.

## Read first (in order)

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md`
3. `docs/planning/RECOVERY.md` (this file)
4. `docs/planning/CURRENT-STATE.md`
5. `docs/planning/ROADMAP.md` and `DELIVERY-PLAN.md`
6. Relevant accepted ADRs, especially ADR-008 and ADR-009, plus `CONTRIBUTING.md`.

## Verified status

- Architecture: FaraTaraz Modular Clean Architecture (FMCA); modular monolith, composition-only Host.
- W0: CERTIFIED (historic baseline).
- W1 / W1-R1 / W1-R2: PASS, **NOT CERTIFIED**; external reviewer acceptance outstanding. Never upgrade status without review evidence and acceptance.
- FMCA architecture lock: completed at `2dd5e0d`; project-graph + source-level guards and negative tests, CI, contribution rules.
- Local execution reported: Debug and Release zero warnings/errors; 143 tests passed (BuildingBlocks 16, SyncContracts 55, Architecture 72), 0 failed.
- GitHub Actions run `37920796253` for `2dd5e0d`: success (independently verified).
- W2 persistence: **not implemented** at this checkpoint; planned and requires explicit task authorization plus persistence ADR.

## Immutable design invariants

- Host: ZERO business authority, no concrete adapter/persistence dependency; only composition and module registration.
- Domain and Application: no outward concrete Infrastructure dependency. Modules own their ports and wiring.
- Tenant authority from trusted context, never request/LLM/MCP input.
- Accounting-source ownership checked fail-closed before provider work.
- Source-scoped external identity: AccountingSourceId + ExternalCode; no automatic cross-source merge.
- CQRS/MediatR contracts, bounded read-only SynchronizeCustomers Query, idempotency and cancellation preserved.
- Architecture guards + negative tests; Debug and Release builds/tests clean; CI green.
- Never force-push, rewrite history, or claim CERTIFIED without accepted evidence.

## Current next action

Execute a separately authorized W2 Persistence Foundation task. First audit W1 contracts and write/accept a PostgreSQL + EF Core persistence ADR; then implement tenant-aware persistence in the module-owned Infrastructure, migrations, source/checkpoint/provenance uniqueness and idempotency constraints, transactional boundaries and integration tests. Do not add HTTP/MCP endpoints, real accounting-provider integrations, dashboards, or AI.

W2 may not bypass unresolved W1 compatibility issues: report blockers rather than silently changing the contract. Keep Host composition-only and extend guards with negative tests for every new boundary.

## Required checkpoint update on every task

Update this file in the same task's final verified commit, including:
- task ID / status / authorized scope
- baseline SHA, implementation commit(s), final remote HEAD after push (report exact value; a file cannot reliably self-reference its own commit)
- module and file changes, ADRs and decisions
- Debug/Release builds, test totals by project, negative-test evidence, CI run/result
- accepted vs pending certifications, blockers and risks
- exact next task and the first executable action

Keep `CURRENT-STATE.md`, `ROADMAP.md`, and `DELIVERY-PLAN.md` consistent. No false or future-dated PASS claims. Preserve history and unrelated files, including untracked `tree.ps1`.

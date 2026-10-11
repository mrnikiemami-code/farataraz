# RECOVERY — FaraTaraz / FMCA

## W2 FINAL-ACCEPTANCE PREFLIGHT AUDIT — FT-W2-R19 (2026-10-11)

**Task:** `FT-W2-R19-FINAL-ACCEPTANCE-PREFLIGHT` (read-only audit + recovery documentation). **Baseline:** `ceef37a` (origin/master; fast-forwarded from `368b1f7`). W2 NOT CERTIFIED; W3 NOT AUTHORIZED. `OPEN-DEFECTS.md` untouched (architect-owned); no production code changed.

**Audit evidence on `ceef37a`:** full solution Debug **250/250** and Release **250/250** (BuildingBlocks 17, SyncContracts 55, Integration 41, Architecture 137), 0 failed/skipped; Linux CI green at the immediately preceding SHAs (`38154728752`, `38156371471`, `38159980853`, `38160912881`).

### Per-defect evidence & recommendation matrix

| ID | Current ledger status | Audited evidence (source / tests) | Recommendation |
|---|---|---|---|
| FT-SEC-001/002/003 | FIXED — UNVERIFIED | Repositories derive tenant only from `DatabaseTenantScope`; `TenantIsolationNegativeTests` (real PostgreSQL, 6) prove cross-tenant finish/read/write denial + missing-scope fail-closed. | **VERIFY-able now** (source + negative tests + green CI). |
| FT-SEC-005 | FIXED — UNVERIFIED | `TryAddScoped<DatabaseTenantScope>` in both compositions; `TenantScopeCompositionTests` (both module orders, missing/untrusted). | **VERIFY-able now.** |
| FT-SEC-006 / FT-GUARD-002 | FIXED — UNVERIFIED | `TenantBoundaryGuards` (real-tree + synthetic negatives: non-INF DbContext ref, mint outside BuildingBlocks, comment/string, interpolated/raw string). | **VERIFY-able now.** |
| FT-SEC-004 | BLOCKED — deferred authority boundary | ADR-011 trust-source inventory (R16): internal minting only, test-only `InternalsVisibleTo` allowlist guard, no production `TenantContext` producer → always fail-closed. No authenticated-principal authority exists. | **Accepted W3/delivery dependency** (ADR-011). Not a W2 code defect; W2 stays fail-closed. Keep BLOCKED. |
| FT-SEC-007 | OPEN — orchestration check observed | R18 inventory: handler + write primitives + unit of work have **zero production callers**; ownership gate proven fail-closed where reachable; guard now covers the full write surface (`StartAsync|InsertOrUpdateAsync|SaveAsync|ExecuteAsync`). | **Requires explicit W3 admission gate** (writer must gate ownership before `IngestionUnitOfWork`). Keep OPEN. |
| FT-DATA-001 | FIXED — UNVERIFIED | `SyncCheckpointConcurrencyTests` (deterministic public-API CAS: stale rejected, newer retained, racing first writers). | **VERIFY-able now.** |
| FT-DATA-002 | FIXED — infra verified; W3 orchestration open | `IngestionUnitOfWork` (single scoped `IngestionDbContext`, commit-after-success, rollback on error/CAS/cancel) + 6 real-PG tests; no production orchestrator invokes it. | **W2 infra boundary accepted; W3 gate required.** Keep dependency open. |
| FT-DATA-003 | OPEN — review semantics | ADR-010 decision 7 explicitly permits "rejected (**or** upserted)"; ADR-005 requires idempotent de-dup + provenance. `ON CONFLICT DO NOTHING` = **approved first-delivery-canonical for duplicate identity**; **changed-record freshness (same identity, new version/fingerprint) is undecided** — currently silently ignored. | **Needs an explicit architect decision**: accept first-delivery-canonical as W2 semantics (mark ACCEPTED RISK + ADR note) **or** schedule a version/freshness task. Not silently closeable. |
| FT-CONFIG-001 | FIXED — UNVERIFIED | `ConnectionConfigurationFailClosedTests` (null/empty/whitespace rejected, supplied string accepted) + design-time factories fail closed. | **VERIFY-able now.** |
| FT-GUARD-001 | FIXED — UNVERIFIED | `IsForbiddenLayer` recognizes module-scoped `.Infrastructure`; `LayerDependencyGraphTests`/`HostAuthorityTests` + injection negative proofs. | **VERIFY-able now.** |
| FT-ARCH-001 (D-1) | OPEN — local fix reported, unreviewed | Implemented on master (`0bf63c9`): module-owned `*.Composition` seams; Host wires seams, not Infrastructure; transitive Host guard refined to seam-only + `CompositionSeamTests`/`HostCompositionSmokeTests`. | **Architect review** of the seam pattern vs ADR-010; appears coherent with ADR-010 decision 14. |
| FT-ARCH-002 (D-5/D-8) | OPEN — design review | Capability contracts split across `AccountingSources` + `Ingestion.Domain`; Infrastructure nesting/naming. Explicitly a deferred `Contracts` extraction (`structure.md` §10, ADR-009 decision 1/2). | **Deferred design choice, not a violation.** Recommend architect record as accepted deferral. |
| FT-ARCH-003 (D-7/D-9..D-16) | OPEN — triage | Mix of trade-offs; D-7→FT-DATA-002, D-12→FT-DATA-003; rest (raw-SQL/schema drift, BuildingsBlocks logging dep, unused marker, capability switch, silent run finish, duplicated scope registration, future-module guard classification, hardcoded reference graph) are maintainability smells, not demonstrated invariant violations. | **Triage individually**; split only demonstrated problems; no speculative refactor. |
| FT-SEC-008 | OPEN — reported risk | Verified: `git check-ignore -v opencode.json` → `.gitignore:22`; `git ls-files opencode.json` → empty (untracked, ignored, not in history). | **VERIFY-able now.** |
| FT-DOC-001 | OPEN | RECOVERY top-level reconciled (current section is W2 NOT CERTIFIED + this audit); historical sections referencing `53a66c5`/"safe foundation" are explicitly labeled historical by the acceptance override at the "Open defects"/"Current acceptance override" notes. No demonstrably incorrect *current* recovery claim found. | **Recommend close as reconciled**; do not rewrite preserved history. |

### Bottom line
- **VERIFY-able now (architect action):** FT-SEC-001/002/003, FT-SEC-005, FT-SEC-006, FT-GUARD-001/002, FT-DATA-001, FT-CONFIG-001, FT-SEC-008, and (per prior architect verification) FT-CI-001/002/003 already VERIFIED.
- **Accepted W3/delivery dependencies (not W2 code defects):** FT-SEC-004 (auth boundary, ADR-011), FT-SEC-007 (W3 admission gate), FT-DATA-002 (W3 orchestrator wraps the unit of work).
- **Needs an explicit decision, not silent closure:** FT-DATA-003 (changed-record freshness) — either ACCEPTED RISK + ADR note or a scheduled task.
- **Documentation/triage, not certification blockers:** FT-ARCH-002 (accepted deferral), FT-ARCH-003 (triage), FT-DOC-001 (reconciled).
- **Genuine W2 certification blockers:** none in W2 code correctness — certification is gated on the architect accepting the three deferred W3/delivery dependencies and deciding FT-DATA-003, plus reviewing FT-ARCH-001.
- **Required minimal remaining fixes:** **none in code.** Optional: an explicit ADR note (or ledger ACCEPTED RISK) for FT-DATA-003, and an architect ruling on FT-ARCH-002 as an accepted deferral.

**Files:** this `RECOVERY.md` only. No production/source, guard, migration or CI change; `OPEN-DEFECTS.md` untouched; no defect VERIFIED or closed; W3 not authorized.

## SOURCE-OWNERSHIP BOUNDARY CHECKPOINT — FT-W2-R18 / FT-SEC-007 (2026-10-11)

**Task:** `FT-W2-R18-SOURCE-OWNERSHIP-BOUNDARY`. **Status:** `PASS` (inventory + preventive guard + real-PostgreSQL negatives). **FT-SEC-007 CANNOT close for W2 as a runtime guarantee — it requires an explicit W3 admission gate** (documented below). No W3 code, no fake auth, no VERIFIED claim. `OPEN-DEFECTS.md` untouched.

**Baseline / sync:** `e7e3ffb` (origin/master; fast-forwarded from `f842b14`).

**Entry-point / caller inventory (production `src/`).**
- `SynchronizeCustomersHandler` — defined in `Ingestion.Application`; **no production caller** (no endpoint/worker dispatches it; it is dispatched only by tests through `ISender`).
- `IAccountingSourceOwnership` — port in `AccountingSources`; `EfAccountingSourceOwnership` the only implementation; DI registers it exactly once (`AddScoped<IAccountingSourceOwnership, EfAccountingSourceOwnership>()`). Its only production consumer is the handler constructor.
- `SyncRunRepository.StartAsync`, `SourceRecordRepository.InsertOrUpdateAsync`, `SyncCheckpointRepository.SaveAsync`, `IngestionUnitOfWork.ExecuteAsync` — defined in `Ingestion.Infrastructure`; **zero production callers** (grep shows only definitions + XML-doc references).
- Conclusion: **the entire write + admission path is unreachable in production today** (no delivery surface, no ingestion engine). There is **no reachable production bypass** — and equally, no production caller is "already protected"; the W3 writer does not exist.

**Fail-closed boundaries that DO exist (verified):** handler denies cross-tenant / untrusted / unknown-source / lookup-failure with zero provider invocations (`TenantSourceOwnershipTests`); oracle denies cross-tenant and missing scope against real PostgreSQL; repositories deny missing/untrusted/cross-tenant (`TenantIsolationNegativeTests`).

**Preventive guard added (FT-SEC-007).** `OwnershipPathAuthorizationGuard` now covers the **full Ingestion write surface** — `StartAsync`, `InsertOrUpdateAsync`, `SaveAsync`, `ExecuteAsync` — and still flags any caller outside the approved `src/Modules/Ingestion/Ingestion.Infrastructure` boundary. A new synthetic negative test proves an Application-layer caller of `IngestionUnitOfWork.ExecuteAsync`/`SaveAsync` is flagged. This pins the invariant "ownership-gated persistence is reached only from the approved boundary" so a future W3 writer cannot bypass it silently.

**Real-PostgreSQL negative tests added** (`AccountingSourcesPersistenceTests`): the oracle denies an unknown source for a trusted tenant, and uses **only the trusted scope** — the caller-supplied tenant argument is ignored (source identity alone never grants, a mismatched argument cannot elevate/downgrade).

**Verification:** focused guard **4/4**; focused ownership+isolation **12/12**; full Debug **250/250** and Release **250/250** (BuildingBlocks 17, SyncContracts 55, Integration **41** — was 39, Architecture **137** — was 136); `git diff --check` clean.

**W3 dependency (exact):** enforcing source ownership on the **write path** belongs to the future W3 ingestion writer/admission gate: the writer must call `IAccountingSourceOwnership.IsOwnedByAsync` (trusted tenant) **before** entering `IngestionUnitOfWork.ExecuteAsync`, inside an explicitly authorized wave. Adding that enforcement now would either be W3 implementation or duplicated business authorization in Infrastructure — both prohibited here. Therefore **FT-SEC-007 stays OPEN** and can only close once that admission gate exists and is independently reviewed.

**Files:** edited `tests/FaraTaraz.ArchitectureTests/OwnershipPathAuthorizationGuard.cs`, `tests/FaraTaraz.Infrastructure.IntegrationTests/AccountingSourcesPersistenceTests.cs`, this `RECOVERY.md`. No production/source change, no guard weakening. CI result in the task final report.

## ATOMIC-SYNC PERSISTENCE CHECKPOINT — FT-W2-R17 / FT-DATA-002 (2026-10-11)

**Task:** `FT-W2-R17-ATOMIC-SYNC-PERSISTENCE`. **Status:** `PASS` (atomic boundary implemented + proven on real PostgreSQL); **FT-DATA-002 partially mitigated — not VERIFIED**. The production sync *orchestrator* that will call this unit of work is the W3 ingestion engine (NOT AUTHORIZED). No production behavior changed (new component, no caller yet). `OPEN-DEFECTS.md` not modified.

**Baseline / sync:** `44c97e1` (origin/master; fast-forwarded from `c3787ff`).

**Trace / scope.** `SynchronizeCustomersHandler` is a read-only bounded-page Query with **no persistence**; the run/record/checkpoint write path has no production caller yet. `SyncRunRepository`, `SourceRecordRepository` and `SyncCheckpointRepository` are all constructed over the **single scoped `IngestionDbContext`** (same database, same connection) — so an explicit transaction on that context spans every write. The ownership oracle (`AccountingSourcesDbContext`) is a **separate** module database and is consulted read-only/fail-closed **before** the unit of work; it is not part of the write unit, so no cross-database atomicity is needed (none fabricated).

**Implementation (smallest Infrastructure-owned boundary).** New `src/Modules/Ingestion/Ingestion.Infrastructure/Persistence/IngestionUnitOfWork.cs`: `ExecuteAsync(work)` opens one database transaction on the shared context, runs the work, and **commits only after it completes**; on any exception (including a stale-checkpoint CAS rejection) or cancellation before commit, disposal rolls the whole step back. Registered in `IngestionInfrastructureComposition` (`AddScoped<IngestionUnitOfWork>()`). Tenant scope, source ownership, idempotency (`ON CONFLICT`) and the observed-version CAS contract are unchanged.

**Red→green proof (real PostgreSQL).** RED: with `IngestionUnitOfWork` temporarily a pass-through (no transaction), `IngestionUnitOfWorkTests` → **3 failed / 3 passed** (`Failure_rolls_back…`, `Stale_checkpoint_cas_rejection…`, `Cancellation_before_commit…`): the record **and** the advanced cursor both persisted after the step failed. GREEN (transaction restored): **6/6**.

**Negative tests added** (`tests/FaraTaraz.Infrastructure.IntegrationTests/IngestionUnitOfWorkTests.cs`, real PostgreSQL): commit-together; failure → both roll back; **stale CAS rejection rolls back the record write** (cursor never advances past uncommitted records); cancellation before commit → rollback; tenant isolation preserved inside the unit of work; and a reproduction proving that **without** the unit of work partial persistence + premature checkpoint advancement are possible.

**Verification:** focused `IngestionUnitOfWorkTests` **6/6**; full Debug **247/247** and Release **247/247** (BuildingBlocks 17, SyncContracts 55, Integration **39** — was 33, +6, Architecture 136); `Ingestion.Infrastructure` rebuild **0 warnings**; `git diff --check` clean.

**Files:** new `IngestionUnitOfWork.cs`, `IngestionUnitOfWorkTests.cs`; edited `IngestionInfrastructureComposition.cs`, this `RECOVERY.md`. No EF InMemory, no silent checkpoint retries, no early cursor advancement, no new auth, no W3 features, no guard weakening. CI result in the task final report.

## TENANT TRUST-BOUNDARY CHECKPOINT — FT-W2-R16 / FT-SEC-004 (2026-10-11)

**Task:** `FT-W2-R16-TENANT-TRUST-BOUNDARY`. **Status:** `PASS` (investigation + narrow guards + ADR documentation). **FT-SEC-004 = BLOCKED on the delivery/authentication boundary** (not exploitable through the W2 public API). No authentication invented, no W3 code, no production behavior change; `OPEN-DEFECTS.md` not modified.

**Baseline / sync:** `d967f1e` (origin/master; fast-forwarded from `23aefb6`).

**Trust-source findings (re-verified against source).**
- `TenantContext`: **internal** ctor + **internal** `FromAuthenticatedPrincipal`; public surface is read-only (`IsTrusted`, `AssertTrusted`). No public minting API.
- `DatabaseTenantScope`: public `None`, public `FromTrusted(TenantContext)` (calls `AssertTrusted`), **private** ctor — a trusted scope cannot be built without an already-trusted context.
- Production DI: **no** `TenantContext` registration anywhere. The two module Infrastructure compositions only READ `GetService<TenantContext>()` and fall back to `None`. So in production the scope is always `None` → every tenant-scoped operation fails closed.
- `SynchronizeCustomersQuery(TenantContext, SyncRequest)` + handler `AssertTrusted()`; repositories/`EfAccountingSourceOwnership` consume `DatabaseTenantScope` and fail closed.
- **No real authenticated-principal authority exists today.**

**Entry points that could mint/substitute (and status).** Public API/DI: none (DI registration of `DatabaseTenantScope.None` only *denies*). Assembly-access: `InternalsVisibleTo` (3 friend assemblies) — now pinned to TEST assemblies only by a new durable guard. Reflection: possible but outside the guard model and not a public-API bypass.

**Narrow negative guards added.** `tests/FaraTaraz.ArchitectureTests/TenantTrustSourceTests.cs` (8 tests): the `InternalsVisibleTo` friend list must be exactly the explicit allowlist of TEST assemblies (fail-closed allowlist; production/module/host/adapter names rejected by a synthetic negative theory), and no production source registers a `TenantContext` producer (fail-closed W2 state, comments/strings stripped before matching).

**ADR.** `ADR-011` gained a "W2 status and trust-source inventory" section stating the dependency: the authenticated-principal authority (and background admission, Constitution A.5) belongs to the delivery/Endpoints (+background) boundary, is not part of W2, and W2 must remain fail-closed until that boundary is authorized and reviewed.

**Verification:** focused trust/boundary/ownership Debug **28/28**; `TenantContextTests` 4/4; integration tenant isolation + scope composition 12/12; full Debug **241/241** and Release **241/241** (BuildingBlocks 17, SyncContracts 55, Integration 33, Architecture **136** — was 128, +8); `git diff --check` clean.

**Files:** new `tests/FaraTaraz.ArchitectureTests/TenantTrustSourceTests.cs`; edited `docs/architecture/adr/ADR-011-Trusted-Tenant-Boundary.md`, this `RECOVERY.md`. No production/source/project/package/migration change. CI result in the task final report.

## ADAPTER-GUARD NON-VACUOUS CHECKPOINT — FT-W2-R15 (2026-10-11)

**Task:** `FT-W2-R15-ADAPTER-GUARD-NONVACUOUS`. **Status:** `PASS` locally — awaiting CI. No production behavior changed; `OPEN-DEFECTS.md` not modified. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline:** `dc1ca56` (origin/master, fast-forwarded from `9f9603e`).

**Defect (confirmed).** `PhysicalStructureTests.AdapterAssemblies()` selected the adapter DLL with a case-insensitive **substring** filter excluding any path containing `ref` or `debug`. A **Debug-only** build (the Linux CI Debug phase, or any fresh debug build) places the adapter at `bin/Debug/<tfm>/<asm>.dll` → excluded → discovery returned **zero** assemblies and `No_adapter_owns_an_Application_handler` passed **vacuously** (a forbidden Application handler inside an adapter would escape detection). A Release-only presence masked it locally.

**Repair (smallest platform-independent fix).** New `tests/FaraTaraz.ArchitectureTests/AdapterAssemblyDiscovery.cs`: enumerates adapter projects structurally (`src/Adapters/*` with exactly one csproj), selects the built `<asm>.dll` under a real **`bin` directory segment** (OS-separator independent; `obj/.../ref` intermediates rejected), and **fails closed** when no adapter project exists or any expected adapter's built assembly cannot be found. Loads by already-loaded simple name or path. `PhysicalStructureTests` now delegates and asserts `Assert.NotEmpty` (never pass on an empty collection); the handler predicate is `AdapterAssemblyDiscovery.ContainsApplicationHandler`.

**Red→green evidence:**
- RED: `AdapterAssemblyDiscoveryTests` → **1 failed / 10 passed** — `Debug_built_adapter_assembly_is_discovered` failed because the debug-substring selector returned `null` for a Debug-only build (the exact CI condition).
- GREEN: **11/11**; focused discovery+physical-structure **23/23**.

**Part B — warnings.** The two `xUnit2031` warnings in `TenantScopeCompositionTests` are fixed with `Assert.Single(services, x => x.ServiceType == typeof(DatabaseTenantScope))` (predicate overload) — assertion semantics unchanged. Rebuild of `ArchitectureTests` and `Infrastructure.IntegrationTests`: **0 warnings**.

**Verification:** full Debug **233/233** and Release **233/233** (BuildingBlocks 17, SyncContracts 55, Integration 33, Architecture **128** — was 117, +11); `git diff --check` clean.

**Files:** new `AdapterAssemblyDiscovery.cs`, `AdapterAssemblyDiscoveryTests.cs`; edited `PhysicalStructureTests.cs`, `TenantScopeCompositionTests.cs`, this `RECOVERY.md`. No production/source, project, package, migration or CI change. CI result recorded in the task final report.

## CI NATIVE-POSTGRES CHECKPOINT — FT-W2-R14 (FT-CI-002) (2026-10-11)

**Task:** `FT-W2-R14-NATIVE-POSTGRES-CI`. **Status:** workflow rewritten (CI-only); CI result recorded in the task final report. No defect marked VERIFIED; `OPEN-DEFECTS.md` not modified. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline:** `f6ee82a` (origin/master, fast-forwarded from `2911b39`).

**Confirmed failure investigated:** run `38152401479` failed **pre-checkout** in `Initialize containers` with the run's own annotation `Docker pull failed with exit code 1` (+2 back-off retries) for the `public.ecr.amazonaws.com/libraries/postgres:16` service image; checkout/build/tests all skipped. Full job logs are not retrievable unauthenticated (`403 Must have admin rights`), so no lower-level root cause was assumed.

**Fix (minimal, workflow-only).** Removed the workflow-level `services:` block (no Docker image pull before checkout) and provision **native PostgreSQL 16** on the runner: `sudo apt-get install -y postgresql-16 postgresql-client-16`; explicit cluster start (`sudo pg_ctlcluster 16 main start`, with a `pg_createcluster 16 main` fallback); a dedicated **disposable** role `farataraz` + database `farataraz`; readiness verification (`pg_isready`, 30×2s) and an explicit TCP password-auth check that prints `SELECT version()`. `FATARAZ_INTEGRATION_TEST_CONNECTION_STRING` now targets `Host=127.0.0.1;Port=5432;Username=farataraz;Password=ftpw;Database=farataraz`. Diagnostics print install/version/readiness/cluster/log-tail on failure; **credentials are never printed.** Debug build, Debug tests, Release build, Release tests and the whitespace guard are preserved unchanged. No EF InMemory / SQLite / mocked PostgreSQL / skipped tests / reduced guard coverage / disabled failures.

**Verification:** workflow YAML parsed (`yaml.safe_load`) — 8 steps, `services` absent; local Debug **222/222** and Release **222/222**; `git diff --check` clean. CI result recorded in the task final report (not claimed PASS here).

**Files:** `.github/workflows/architecture.yml`, this `RECOVERY.md`. No source/test change.

## COMPOSITION-GUARD HARDENING CHECKPOINT — FT-W2-R12 (D-1/D-6 review) (2026-10-11)

**Task:** `FT-W2-R12-COMPOSITION-GUARD-HARDENING`. **Status:** `PASS` locally — **NOT CERTIFIED**. No production composition behavior changed; no W3; `OPEN-DEFECTS.md` not modified. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline:** `60de13f` (merged HEAD).

**Part A — composition ownership bypass (D-1 review finding) — CONFIRMED and fixed.** The guard authorized Infrastructure declared by any project whose name ended with `.Composition` (`IsCompositionSeam`), so a foreign/fake `*.Composition` project in the host closure could grant a module's Infrastructure — and a legitimate seam could wire **another** module's Infrastructure. Ownership was grantable by naming. **Fix:** new `tests/FaraTaraz.ArchitectureTests/CompositionSeamPolicy.cs` — a seam is sanctioned only when physically module-owned at `src/Modules/<Module>/<Module>.Composition/FaraTaraz.Modules.<Module>.Composition.csproj`, and it may allow ONLY its **own** module's Infrastructure (`src/Modules/<Module>/<Module>.Infrastructure/…`). `HostAuthorityTests` and `CompositionSeamTests` delegate to the policy; `CompositionSeamTests` now also asserts every `*.Composition` project under `src` is a sanctioned module-owned seam wiring only its own module Infrastructure. No Host business-authority rule weakened; classification centralized (adapter/infrastructure/seam) to one source to prevent drift.

**Part B — CS8619.** Root cause: `SourceTreePathsTests.ScannedFileNames` returned `IReadOnlyList<string>` from `.Select(Path.GetFileName)` (which yields `IEnumerable<string?>`). Fixed type-safely with `.Select(file => Path.GetFileName(file) ?? string.Empty)` — no suppression/pragma; rebuild shows 0 CS8619.

**Red→green evidence:**
- RED (policy carrying the old naming-only rule): `CompositionSeamPolicyTests` → **7 failed / 3 passed** — foreign/fake `.Composition` accepted, legitimate seam reported the wrong module, a foreign seam was granted Infrastructure allowance, and cross-module Infrastructure was allowed.
- GREEN (module-owned rule): `CompositionSeamPolicyTests` 10/10; focused policy+seam+host 19/19.
- CS8619 was reproduced (build warning `SourceTreePathsTests.cs(104,12)`) and is gone after the fix (ArchitectureTests rebuild 0 warnings).

**Verification (this HEAD):** ArchitectureTests Debug **117/117** (was 107; +10) and Release **117/117**; full solution Debug **222/222** and Release **222/222** (BuildingBlocks 17, SyncContracts 55, Integration 33, Architecture 117); ArchitectureTests rebuild 0 warnings; `git diff --check` clean.

**Files changed:** new `CompositionSeamPolicy.cs`, `CompositionSeamPolicyTests.cs`; edited `HostAuthorityTests.cs`, `CompositionSeamTests.cs`, `SourceTreePathsTests.cs`, this `RECOVERY.md`. No production/source, project-reference, package, migration or CI change. **`OPEN-DEFECTS.md` NOT modified** (architect-owned); D-1/D-6 not marked VERIFIED.

**Remaining blockers:** independent architect review; Linux CI still not run (FT-CI-002 OPEN); D-2/FT-SEC-004 OPEN; provider-adapter composition still absent.

## GUARD CHECKPOINT — FT-W2-R9-CROSSPLATFORM-GUARDS-001 / FT-CI-003 (D-6) (2026-10-11)

**Task:** `FT-W2-R9-CROSSPLATFORM-GUARDS-001` (target **FT-CI-003**, audit finding **D-6**). **Status:** `PASS` locally — guards fixed, tests green; **NOT CERTIFIED**; no production/source change, no CI claim. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Preflight / divergence:** local HEAD `fe75a35`; `origin/master` `3571ee5`. Histories **diverged** from the common base `91db8b3`: this working branch has `0bf63c9` (FT-ARCH-001/D-1), `9f7ac3e` (D-4), `fe75a35` (FT-ARCH-002/D-2); remote `origin/master` has three architect planning-document commits (`1a16292`, `329b6b3`, `3571ee5`) that touch `ARCHITECT-JOURNAL.md`, `OPEN-DEFECTS.md`, `RECOVERY.md`. No merge/rebase/reset/force-push performed; work stayed on the current local branch. The architect's remote planning edits were **not** overwritten and are not present in this local checkpoint (reconciliation is the architect's call). Working tree was clean at start (no concurrent agent edits).

**Defect (FT-CI-003 / D-6) — confirmed:** three architecture guards excluded build output using **Windows-only** path fragments (`\bin\` / `\obj\`), so on the Ubuntu CI runner generated `obj/**/*.cs` files would be scanned and fail the namespace/path guards (latent; the latest observed FT-CI-002 failure was container initialization, so tests never ran). Files: `PhysicalStructureTests.ProductionSourceFiles` (`:383`), `OrphanSourceFileTests` (two sites `:44`,`:90`), `ProjectDependencyGraph.IsBuildOutput` (`:93`). `OwnershipPathAuthorizationGuard.IsBuildOutput` already checked both separators; `TenantBoundaryGuards.IsInBuildOutput` and `SourceFileSizeGuardTests.EnumerateBudgetViolations` were already separator-independent.

**Repair (smallest coherent correction):** new `tests/FaraTaraz.ArchitectureTests/SourceTreePaths.cs` — a single `internal static SourceTreePaths.IsBuildOutput(string)` that normalizes `\`→`/` and matches an exact `bin`/`obj` **directory segment** (`mybin`/`objo` are not build output). The three defective guards plus `OwnershipPathAuthorizationGuard` now delegate to it. Scanning behavior, ownership enforcement, namespace checks and architecture restrictions are unchanged; no blanket exclusion was added.

**Red→green evidence:**
- RED: `SourceTreePaths.IsBuildOutput` initially carried the exact old Windows-only logic; `SourceTreePathsTests` → **Failed 5 / Passed 11** — every forward-slash (`/`-separated) generated path was **not** excluded, while the `\` cases passed. This reproduces the defect deterministically on Windows.
- GREEN (after the fix): `SourceTreePathsTests` → **16/16 pass** (8 generated-path cases across `\`, `/`, `C:/`; 6 real-source cases; temp-tree scan excludes generated files but keeps real source; negative case proves a misplaced real source is still scanned).

**Verification (commands + counts):**
- `dotnet test tests/FaraTaraz.ArchitectureTests/FaraTaraz.ArchitectureTests.csproj -c Debug` → **107/107 pass** (was 91; +16 new).
- `dotnet test tests/FaraTaraz.ArchitectureTests/FaraTaraz.ArchitectureTests.csproj -c Release` → **107/107 pass**.
- `dotnet test FaraTaraz.sln -c Debug` → **212/212 pass** (BuildingBlocks 17, Architecture 107, SyncContracts 55, Integration 33 against real PostgreSQL).
- `git diff --check` clean.

**Explicitly NOT claimed:** this does **not** prove Linux CI is fixed — no Linux CI run was performed (none is available locally), and the GitHub Actions workflow was intentionally **not** modified. The cross-platform behavior is established by the separator theories against the shared predicate, not by a Linux execution.

**Files changed:** new `SourceTreePaths.cs`, `SourceTreePathsTests.cs`; edited `PhysicalStructureTests.cs`, `OrphanSourceFileTests.cs`, `ProjectDependencyGraph.cs`, `OwnershipPathAuthorizationGuard.cs`, this `RECOVERY.md`. No production/source, project-reference, package, migration or CI change. Baseline `fe75a35`; this task's commit SHA is reported out-of-band (a file cannot self-reference its own commit).

**Remaining risks:** any *other* latent Windows-only assumption in guards not covered by the grep (`Replace("\\","/")` normalizations remain and are correct); an actual Ubuntu CI run is still required to confirm green end-to-end; FT-CI-002 stays OPEN. D-6 is a guard portability fix only — it does not certify W2 and does not authorize W3. The independent architect owns any ledger status change (this checkpoint does **not** mark FT-CI-003/D-6 VERIFIED).

## DECISION CHECKPOINT — FT-ARCH-002 / D-2 trusted tenant boundary (2026-10-11)

**Task:** `FT-ARCH-002` (new finding **D-2**, same gap as **FT-SEC-004**). **Status:** `DECISION RECORDED` — **docs-only**; D-2 **deliberately remains OPEN**; **no code**. Owner chose the ADR-only option. No business feature, no W3, no authentication invented. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline / working state:** baseline `9f7ac3e` (master, after `0bf63c9`/FT-ARCH-001 and `9f7ac3e`/D-4).

**Finding (D-2):** the trusted tenant boundary has **no producer**. `TenantContext` minting is internal to `BuildingBlocks` (`39164cd`) and `TenantBoundaryGuards` forbids `new TenantContext` / `FromAuthenticatedPrincipal` outside `src/BuildingBlocks`; no production code mints or registers a `TenantContext`, yet the module Infrastructure compositions resolve `DatabaseTenantScope` from `GetService<TenantContext>()` (ADR-010 decision 5). Net effect: in production `DatabaseTenantScope` is always `None` → every tenant-scoped operation fails closed → the boundary is defined but has no lawful door.

**Why code was not written (per owner decision):** a producer can only be (1) an authenticated-principal boundary or (2) background execution with explicit non-client context (Constitution A.5). The ledger's FT-SEC-004 closure forbids **inventing authentication**, and the background path is W3 (**not authorized**). The lawful next step is therefore the decision itself.

**Decision recorded — ADR-011 (Proposed):** new `docs/architecture/adr/ADR-011-Trusted-Tenant-Boundary.md` (Status: **Proposed — pending architect acceptance**). It defines a single fail-closed establishment seam converting an `IAuthenticatedPrincipal` port into a trusted `TenantContext`; minting stays internal to `BuildingBlocks`; the boundary is owned by delivery/execution admission (not Host, not Application/Domain/Infrastructure); a composition helper binds the scoped `TenantContext` from the authenticated principal (absent/unauthenticated → untrusted → `None`); `TenantBoundaryGuards` must be extended with negative proofs on any implementation wave. It explicitly does **not** design authentication and does **not** authorize code.

**Files changed (docs only):** new `docs/architecture/adr/ADR-011-Trusted-Tenant-Boundary.md`; `docs/planning/OPEN-DEFECTS.md` (new `FT-ARCH-002` row, OPEN — DESIGN DECIDED); this checkpoint. No source, test, solution, or package change. No build/test required (docs-only).

**Not done / still OPEN:** no establishment seam, no `IAuthenticatedPrincipal` port, no principal supplier, no guard extension; D-2/FT-SEC-004 remain OPEN and **unverified**. W3 remains NOT AUTHORIZED.

**Next exact action:** the architect must **accept or amend ADR-011**, then authorize a boundary wave (delivery Endpoints or explicit background execution per Constitution A.5) that implements the seam + a principal supplier, extends `TenantBoundaryGuards` with inject→fail→revert proofs, and validates end-to-end — only then can D-2/FT-SEC-004 move toward VERIFIED. Do NOT implement the seam or any tenant-scoped production execution before that authorization.

## CODE CHECKPOINT — FT-ARCH-001 / D-1 composition root (2026-10-11)

**Task:** `FT-ARCH-001` (new finding **D-1**, repair of the composition root). **Status:** implemented, `PASS` locally — **NOT CERTIFIED**; awaiting independent architect review. No business feature, no W3, no provider integration, no tenant/auth invention. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline / working state:** baseline `91db8b337223a69cc2de39b2fa063daf033cee70` (branch `master`). Changes are **uncommitted** in the working tree (not pushed). See "Files changed" below.

**Defect (D-1):** `FaraTarazHost.ConfigureFaraTaraz` only called `AddIngestionApplication()`; the two module Infrastructure composition entry points (`AddIngestionInfrastructure` / `AddAccountingSourcesInfrastructure`) were never invoked in production (only by tests). The production dependency graph could not register persistence or the ownership oracle. Additionally the guard set made any wiring impossible: `Host → Infrastructure` is forbidden (AGENTS.md/`HostAuthorityTests`) and `Ingestion.Application → Infrastructure` was forbidden by FT-W3-PREFLIGHT, so there was **no legal path** to compose the outward layers. This also conflicted with **ADR-010 decision 14** ("only the module-owned `Composition` references the module's Infrastructure … the Host composes modules through their `Composition` entry points") and the AGENTS.md "Composition exception review".

**Repair (module-owned composition seams — ADR-010 decision 14):**
- New module-owned composition seam projects (DI wiring only, no business rules, no handler, no tenant decision):
  - `src/Modules/Ingestion/Ingestion.Composition/` (`FaraTaraz.Modules.Ingestion.Composition`) → `AddIngestionModule(connectionString)` composes `Ingestion.Application` + `Ingestion.Infrastructure`.
  - `src/Modules/AccountingSources/AccountingSources.Composition/` (`FaraTaraz.Modules.AccountingSources.Composition`) → `AddAccountingSourcesModule(connectionString)` composes the ownership oracle + persistence.
- `FaraTarazHost.ConfigureFaraTaraz(IServiceCollection, string connectionString)` now composes both modules through their seams. The Host references the **seams**, never the concrete Infrastructure (so the composition-only Host rule is preserved).
- `AccountingSources.csproj` excludes the new sibling `AccountingSources.Composition\**` from compilation (same pattern as the existing `AccountingSources.Infrastructure\**` exclusion).
- `Ingestion.Application` was **not** changed → the existing Application layer guard stays intact. No Application/Domain→Infrastructure reference was added.

**Guard reconciliation (precise refinement, not weakening):**
- `HostAuthorityTests`: the **transitive** guard now permits Infrastructure **only** when it is a directly declared reference of a module `*.Composition` seam; **adapters and non-seam Infrastructure remain forbidden**. The **declared-reference** guard (`Host_declares_no_adapter_or_infrastructure_project_reference`) is unchanged. Split `IsForbiddenLayer` into `IsAdapter`/`IsInfrastructure` + `IsCompositionSeam`/`InfrastructureBehindCompositionSeams`/`ForbiddenInClosure`.
- New `tests/FaraTaraz.ArchitectureTests/CompositionSeamTests.cs`: every `*.Composition` seam must wire a `*.Infrastructure` project, must not reference an adapter, and must contain no handler/MediatR/Application-feature code. Pure classifier `SeamViolations` with synthetic negative proofs.
- New `tests/FaraTaraz.ArchitectureTests/HostCompositionSmokeTests.cs`: proves `ConfigureFaraTaraz(connectionString)` registers `IAccountingSourceOwnership` + the two contexts + `SyncRunRepository`, and fails closed with `ConnectionConfigurationException` on a blank connection string.
- Structural allowlists extended: `PhysicalStructureTests` (namespace roots + single-file leaves) and `OrphanSourceFileTests` (known project directories) for the two new seam projects.
- `FaraTaraz.sln` + `FaraTaraz.ArchitectureTests.csproj` (`Microsoft.Extensions.DependencyInjection` 8.0.1→9.0.1 to match the Infrastructure EF Core 9.0.1 closure).

**Negative proof (inject → fail → revert):** a temporary `Host → FaraTaraz.Adapters.Accounting.Mock` ProjectReference made **both** `Host_declares_no_adapter_or_infrastructure_project_reference` and `Host_effective_dependency_graph_exposes_infrastructure_only_through_a_composition_seam` FAIL (2 failed / 5 passed); the reference was reverted and the guard passes again. This proves the refined transitive guard still rejects adapters (the allowance is exactly "Infrastructure behind a `*.Composition` seam"). Plus synthetic negative proofs in `CompositionSeamTests`.

**Verification:** Debug build 0 errors (2 pre-existing `xUnit2031` warnings in `TenantScopeCompositionTests`, present on the baseline, unrelated). Release build 0 errors. Full suite **196/196 pass** in **both Debug and Release**: BuildingBlocks 17, SyncContracts 55, ArchitectureTests **91** (was 75), Infrastructure.IntegrationTests 33 (real PostgreSQL on `localhost:54320`). No guard weakened.

**Files changed:** new `src/Modules/Ingestion/Ingestion.Composition/**`, `src/Modules/AccountingSources/AccountingSources.Composition/**`, `tests/FaraTaraz.ArchitectureTests/CompositionSeamTests.cs`, `tests/FaraTaraz.ArchitectureTests/HostCompositionSmokeTests.cs`; edited `FaraTaraz.sln`, `src/Host/FaraTaraz.Host/FaraTaraz.Host.csproj`, `src/Host/FaraTaraz.Host/Composition/FaraTarazHost.cs`, `src/Modules/AccountingSources/FaraTaraz.Modules.AccountingSources.csproj`, `tests/FaraTaraz.ArchitectureTests/{HostAuthorityTests,PhysicalStructureTests,OrphanSourceFileTests,FaraTaraz.ArchitectureTests}.cs`.

**Unresolved / NOT covered by this task (do not treat as fixed):**
- **Provider adapter composition is still absent.** No `ISyncablePort<SourceCustomer>` is registered in production (no real provider exists; `Accounting.Mock` is test-only and must not be Host-referenced), so `SynchronizeCustomersHandler` is still not resolvable end-to-end. This is a separate adapter/wave concern, not D-1.
- **D-2 (no production `TenantContext` producer) remains OPEN.** Even with persistence wired, the trusted tenant scope still resolves to `None` (fail-closed) in production until a trusted auth/execution boundary exists (ADR-010 decision 5; FT-SEC-004).
- Existing open defects (FT-CI-002 CI red, FT-DATA-002 transaction boundary, FT-SEC-004, etc.) unchanged.

**Next exact action:** independent architect source + CI review of this change set, reconcile the `*.Composition` seam pattern with ADR-010 (and update the ledger / `structure.md` §10.1 / `CURRENT-STATE.md` accordingly). Do NOT mark W2 certified or authorize W3. If authorized, commit as one coherent change (no push without confirming `origin = https://github.com/mrnikiemami-code/farataraz`).
## CURRENT ARCHITECT HANDOFF — 2026-10-11 (supersedes older current-task/Wake-next instructions)

- **Objective:** finish and independently certify W2, not W3. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Architect reviews source/CI and owns `OPEN-DEFECTS.md`; executor implements. User explicitly requests **task text in chat only; DO NOT send implementation tasks through MCP**.
- **GitHub evidence:** remote `origin/master` at inspection = `91db8b337223a69cc2de39b2fa063daf033cee70`; latest workflow run `38076703312` = FAILURE. Architect's ledger triage commit may follow this SHA; re-fetch master every time.
- **Unpushed local changes reported, not independently inspected:** `0bf63c9` (16-file Host/architecture wiring D-1) and `9f7ac3e` (ignore local `opencode.json` D-4); local HEAD reported `9f7ac3e`, working tree clean, 196/196 Debug and Release tests locally reported. **Never overwrite/reset/rebase/discard these local commits.** Their D-1 scope may conflict with prior architect-selected D-1 deferral; hold acceptance until full diff review. Avoid GitHub write conflict: architect documentation commits have advanced remote master; implementer must fetch and reconcile using non-destructive strategy, no force push.
- **New audit:** 16 findings user-supplied (D-1..D-16); triaged as new ledger FT-ARCH-001, FT-SEC-008, FT-CI-003, FT-ARCH-002/003 and mapped D-2→FT-SEC-004, D-3→FT-DATA-002, D-12→FT-DATA-003. Reports are not proof. Priority: protect credentials; reconcile local commits; reproduce D-6 cross-platform guard problem and actual CI failure; close trust boundary, checkpoint/transaction correctness and remaining W2 verification with source+negative tests+CI.
- **Next task:** `FT-W2-R9-CROSSPLATFORM-GUARDS-001` is a bounded local agent task to be pasted by user; inspect four reported bin/obj guards, reproduce Linux path behavior with negative tests, repair only confirmed defects, run targeted tests; preserve all local commits; no push unless separately authorized. W2 closure requires subsequent independent review and successful CI.
- **Wake:** previous event-triggered architecture chat subscription is bound to a different conversation and may have expired; it is not a prerequisite for manual task handoff in this chat. Do not create subscriptions, send MCP messages, or assume event wake for current work.

## CURRENT WAKE RECOVERY — FaraTaraz end-to-end test VERIFIED (2026-10-10)

This section supersedes the historical Wake checkpoint and stale next-task instructions below. Architect remains ChatGPT; executor is OpenCode. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. No architecture defect was closed by the communication test.

### Verified destination and explicit model
- Project: `D:\FaraTaraz`; bridgeSessionId: `9e669b57-39b0-4636-9a19-356b25f17dd3`; OpenCode session: `ses_edc063b45ffewWoh2DGDN90shm`.
- Always supply `providerID=ornith_bridge` and `modelID=ornith-1.5-35b-a3b-local` explicitly to `opencode_send_message`, with `async=true`. Do not rely on the session's old `local` model selection.
- Project-specific `D:\FaraTaraz\opencode.json` uses the singular `provider.ornith_bridge`, `@ai-sdk/openai-compatible`, baseURL `http://127.0.0.1:18081/v1`, and both `model` and `small_model` set to `ornith_bridge/ornith-1.5-35b-a3b-local`. Full provider definition was copied from bridge-test while preserving other destination settings and backing up an existing destination; user executed the local update and architect independently read the resulting file. Credential presence was checked without displaying its value.
- This local file and its backups contain credentials: NEVER commit/push them or expose their contents. Do not modify global OpenCode, Node, Twenty, network or other services as part of routine task execution.

### Persistent event subscription and correct cycle
- Existing Automation: `FaraTaraz Wake`, bound to this architect conversation `6ac9db99-7bb8-83ed-8657-9c728f05b255`. Connector: `asdk_app_6ac9c737e54881919361d823705e5b1f`.
- Trigger: `opencode.turn.finished`, params `{"bridgeSessionId":"9e669b57-39b0-4636-9a19-356b25f17dd3"}`. Preserve/reuse this subscription; never create a duplicate or substitute a scheduled polling task. Test-project subscription is separate.
- Observed subscription expiry: `2026-10-11T06:33:01.595Z` (2026-10-11 10:03:01 Asia/Tehran). Recheck actual expiry/activation before later work; this timestamp is not permanent.
- Before sending: inspect session status and recent messages; deduplicate task/message/event IDs and avoid concurrent work. Verify actual project/server routing rather than assuming a fixed OpenCode port.
- Send exactly one authorized async instruction with explicit provider/model, then END the architect turn without polling. Accepted send means queued/accepted only, not model success or ChatGPT wake success.
- Upon the event-driven architect run: read `opencode_get_messages` and `opencode_get_session_status`; correlate event messageID/parentMessageID with the actual terminal reply. Report actual result/error. For implementation work also independently inspect diff/source/tests/GitHub and update architecture evidence before any acceptance.
- Keep the persistent subscription enabled after a match or an error. A terminal error also wakes the architect and must not be called a successful model response.
- Current Automation prompt is TEST-ONLY: read/report the result and send NO next work, modify NO files/settings/tasks, execute NO Git. Successful testing does not silently authorize an autonomous implementation loop; reconcile its prompt with an explicitly authorized next scope before real task orchestration.

### Independent success evidence
- Successful request: `msg_124ab62e0001xlOjzbIRiXBnRi`; completed assistant reply: `msg_124ab62ec001rrBKHEuP0BTyrS`; exact text: `WAKE_FARATARAZ_OK`; finish `stop`; provider `ornith_bridge`.
- Wake event: `evt_55def956d04abe3e761d77fcf649c438aa5493e3f1265c68c1045b282d35761b`, timestamp `2026-10-10T07:16:29.073Z` (10:46:29 Tehran). The event-triggered run independently read the completed reply/status and reported `WAKE_RECEIVED` + `WAKE_FARATARAZ_OK`, without another user message or architect polling. Status reported no active execution.
- This verifies ChatGPT architect → Bridge/OpenCode → local model → Wake → SAME architect conversation for this test, not future uptime, arbitrary tools, or architecture certification.

### Failures resolved or diagnostic limits
- First request failed with user-supplied local log `ProviderModelNotFoundError: local/ornith-1.5-35b-a3b-local`.
- Second explicit-provider request also failed with user-supplied local log `ProviderModelNotFoundError: ornith_bridge/ornith-1.5-35b-a3b-local` before the setup chat restarted the FaraTaraz OpenCode server. A correct config file alone did not prove a running server had loaded it.
- Subsequent event independently showed `APIError: Cannot connect to API: Unable to connect` at `127.0.0.1:18081/v1/chat/completions`. That ERROR event successfully woke this chat; it was not model success.
- User then supplied `/health → {"status":"ok"}` for port 18081, authorized one new test, and the successful response/event above followed.
- `opencode_capabilities` repeatedly returned `Transport closed`; user explicitly waived this as a precondition for the bounded test. Do not repeatedly block an authorized test on this tool alone or assert it proves model unavailability.
- Bridge health and retained session baseUrl reported different ports during setup (session 4096, FaraTaraz managed server 4097 then 4098). This discrepancy was observed, but NOT established as the root cause. The final test succeeded in the retained session; do not silently recreate/rebind sessions or restart services based on that discrepancy.

### Resume architecture
Read the current journal and OPEN-DEFECTS, independently fetch latest master, preserve all OPEN/FIXED-UNVERIFIED issues. Latest substantive implementation review remains FT-W2-R3: FT-DATA-001 is partially mitigated; delayed stale writer needs an observed-version contract coordinated with FT-DATA-002. No implementation task was sent after the successful Wake test. Select/authorize the next bounded task from current evidence; do not repeat completed ownership audits from stale historical next-action prose.

## HISTORICAL WAKE CHECKPOINT — superseded (2026-10-10)

- Architect: ChatGPT independently reviews source/diff/tests and owns certification and `docs/planning/OPEN-DEFECTS.md`; OpenCode is executor. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Current next implementation concern: FT-DATA-001 delayed stale writer (partially mitigated in `f373bd4`); coordinate with FT-DATA-002 transaction boundary. Do not run concurrent agents.
- Verified tool path: ChatGPT → OpenCode Wake MCP → local Bridge → OpenCode 1.18.35 → local Ornith. Return path: OpenCode turn completion → Wake `opencode.turn.finished` → event Automation wakes architect → inspect messages/status/diff independently. Successful test returned `WAKE_RECEIVED` and `WAKE_TEST_OK` after roughly four minutes (not an SLA).
- Allowed project: `D:\\FaraTaraz`; retained test: `C:\\Users\\User\\ChatGPTBridge\\bridge-test`. FaraTaraz bridgeSessionId `9e669b57-39b0-4636-9a19-356b25f17dd3`; test session `7ca425d3-9b35-4215-8836-c6670a23fc5e` MUST NOT be used for FaraTaraz.
- Independently checked 2026-10-10: Wake bridge health OK, both repos listed, FaraTaraz session exists and idle, branch master and no reported uncommitted files; Wake status had only the TEST subscription and accepted test delivery. Automation list had only the TEST event automation; no FaraTaraz event subscription confirmed or created. **Do not send async implementation work until a FaraTaraz event subscription is confirmed active.**
- Before each task: health, projects, actual session status, Git branch/HEAD/diff, concurrent actors, provider/model, existing Automation and Wake subscription/expiry. Subscribe only once to `opencode.turn.finished` scoped to the real FaraTaraz bridgeSessionId; verify creation before async send. If subscription creation errors, re-list before retry. On wake, event indicates terminal turn, NOT success; read messages/status/diff, inspect source/tests and GitHub independently, reconcile defects/journal, then issue only authorized next task. Deduplicate event deliveries and outgoing task IDs.
- Wake subscriptions expire. Local model, Bridge, Wake, Tailscale Funnel and required proxy must stay up. No changes to Node 24, Twenty/Docker, other services, network, or secrets. No unapproved file deletion, broad permission, destructive Git operation or automatic certification. Commit/push only per bounded task scope.
- **Tool limitation noted at this checkpoint:** Available general Automation creation API exposes scheduled VEVENT, not the Wake event subscription registration schema. Do not fake an event subscription by creating a periodic task. Obtain event-capable registration interface before using this flow for production work.


**Checkpoint recorded:** 2026-10-10
**Current task (architect override, 2026-10-10):** FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT — READ-ONLY production ownership/DI/persistence-caller audit; target 10–15 minutes, stop by 20. FT-W2-R2-B-SOURCE-OWNERSHIP produced no implementation; prior handler inspection is recorded in ARCHITECT-JOURNAL.md and must not be repeated. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.
**Last reported W2 implementation commit:** `10eb8c9435f9958c3ce3b38bfcec82719827995e` (master; W2 PASS, not certified). Prior FMCA lock baseline: `2dd5e0dac5fcd978a074dfedc9954b3d4e0e1167`.
**Last architect-observed remote HEAD (before this recovery documentation update):** `bfa4c8c89085bdbb9525b28a5dc499bb3c59a206`; only journal/ledger commits followed implementer HEAD `a31aa254`. Always fetch latest master; this is not implementation acceptance.
**Note:** Documentation-only commits may follow the implementation baseline. Always fetch and compare current `origin/master` before executing. Do not assume the recovery SHA equals current HEAD.

**Implementer handoff — FT-W2-R2-B2-OWNERSHIP-GUARD (2026-10-10):** Added a durable architecture guard `tests/FaraTaraz.ArchitectureTests/OwnershipPathAuthorizationGuard.cs`: scans production `src/**/*.cs` (excluding `tests/` and build output) and flags any call to the ownership-write primitives `SyncRunRepository.StartAsync` / `SourceRecordRepository.InsertOrUpdateAsync` made outside the `Ingestion.Infrastructure` layer. Comments/strings are stripped before matching, so a comment cannot satisfy the guard. GREEN on the shipped source (no forbidden caller); RED proven via a synthetic Application-layer caller; comment/string-stripping proof included. All 75 ArchitectureTests pass (72 prior + 3 new); no regressions. csproj reverted to original (no dependency changes). FT-SEC-007 remains **OPEN** (guard is acceptance *evidence*, not certification); W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Pending: independent ChatGPT architect review of the guard.

**Architect continuity journal:** [`docs/planning/ARCHITECT-JOURNAL.md`](ARCHITECT-JOURNAL.md) — ChatGPT-owned durable log of reports received, independent source/CI checks, architectural decisions, exact GitHub commits, blockers and next steps. On every new chat or context recovery, ChatGPT must read this journal together with RECOVERY and OPEN-DEFECTS, independently reconcile GitHub state, and update the journal itself after each substantive review. Implementers do not own or certify this journal.

**Open defects / acceptance ledger:** [`docs/planning/OPEN-DEFECTS.md`](OPEN-DEFECTS.md) — mandatory read before any W2/W3 task. All unresolved security, DI, transaction, CI, guard, and documentation defects remain tracked there; W2 is NOT CERTIFIED and W3 implementation is not authorized until explicit acceptance. Do not interpret older PASS or 'safe foundation' reports below as current architectural acceptance.

**ARCHITECT RECOVERY / OWNERSHIP LOCK:** ChatGPT, acting as the FaraTaraz independent architecture reviewer, is the **accountable owner and maintainer** of `docs/planning/OPEN-DEFECTS.md`. OpenCode/Cursor are implementers and **cannot certify fixes**. For each reported fix, ChatGPT must fetch and inspect actual GitHub files/diffs, relevant negative guard/test evidence and GitHub CI; **ChatGPT itself must update the defect ledger on `master`**, preserving historical findings and marking VERIFIED only with independent evidence. Missing evidence means OPEN or FIXED — UNVERIFIED. Before any W2 acceptance or W3 authorization, review and reconcile every blocking item. On chat recovery, first read this section and the ledger and resume the same responsibility. Updates occur when a review is requested/performed; no autonomous background GitHub monitoring is implied.

**Current acceptance override:** W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Earlier statements in this file describing W2-R1 as “verified”, database-enforced isolation through uniqueness, or W2 as a “safe foundation” are historical implementation reports, **not current independent architectural approval**. Current statuses live in `OPEN-DEFECTS.md`.

## Read first (in order)

1. `AGENTS.md`
2. `docs/architecture/architecture-constitution.md`
3. `docs/planning/RECOVERY.md` (this file) and `docs/planning/OPEN-DEFECTS.md`
4. `docs/planning/CURRENT-STATE.md`
5. `docs/planning/ROADMAP.md` and `DELIVERY-PLAN.md`
6. Relevant accepted ADRs, especially ADR-008 and ADR-009, plus `CONTRIBUTING.md`.

## Verified status

- Architecture: FaraTaraz Modular Clean Architecture (FMCA); modular monolith, composition-only Host.
- W0: CERTIFIED (historic baseline).
- W1 / W1-R1 / W1-R2: PASS, **NOT CERTIFIED**; external reviewer acceptance outstanding. Never upgrade status without review evidence and acceptance.
- FMCA architecture lock: completed at `2dd5e0d`; project-graph + source-level guards and negative tests, CI, contribution rules.
- Local execution reported: Debug and Release zero warnings/errors; 152 tests passed (BuildingBlocks 16, SyncContracts 55, Architecture 72, Infrastructure.IntegrationTests 9), 0 failed.
- GitHub Actions run `37920796253` for `2dd5e0d`: success (independently verified).
- W2 persistence: **implemented** at this checkpoint (see below). ADR-010 accepted; `AccountingSources.Infrastructure` + `Ingestion.Infrastructure` with EF Core, migrations, tenant isolation, unique/idempotency constraints, and real PostgreSQL integration tests; CI provisions PostgreSQL.

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

Execute `FT-W2-R2-B1-OWNERSHIP-PATH-AUDIT` as specified in the latest ARCHITECT-JOURNAL.md entry: production ownership implementation/DI and all run-start/record-write callers, read-only, no repeated handler audit. CI remains red (run 38001093552: container initialization failed, build/tests skipped). All open defects remain; no W3 authorization.

## Historical next-action report — superseded, NOT authorization

First execute a bounded W3 preflight/composition-boundary audit (maximum 45 minutes) and update recovery. Then authorize one small W3 slice at a time. Do not launch the entire W3 engine in one task. First audit the W2 persistence
schema and the W1 sync contract; then implement durable synchronization execution (run
orchestration, retries, failure states, resumability) over the persisted state. Do not add
new provider integrations, dashboards, REST/MCP endpoints, or AI.

W3 must not bypass unresolved W1/W2 certification: report blockers rather than silently
changing the contract. Keep Host composition-only and preserve every invariant below.

## W2 — Persistence Foundation (implementation complete — NOT certified)

**Task:** `FT-W2-PERSISTENCE-FOUNDATION-001`. **Status:** `PASS` (acceptance criteria passed;
**not certified** — awaiting reviewer acceptance as the new baseline). W2 does not authorize
W3; W3 requires a separate explicit task.

**Delivered (per ADR-010, Accepted):**
- New module-owned Infrastructure projects:
  - `src/Modules/AccountingSources/AccountingSources.Infrastructure/` — persists `Tenant` +
    `AccountingSource` and implements the provider-independent, fail-closed
    `IAccountingSourceOwnership` oracle.
  - `src/Modules/Ingestion/Ingestion.Infrastructure/` — persists sync run state,
    checkpoints/cursors, source-record identity, and provenance.
- EF Core is Infrastructure-only (never in Domain/Application/BuildingBlocks/Host, ADR-009
  decision 4). Design-time `DbContextFactory` types wired for both modules.
- Migrations: `FTW2-InitialSchema` for both modules, reviewed manually against the model
  (ADR-010 decision 10), applied by an operator/CI step (never silently at runtime).
- Database-enforced tenant isolation: `(TenantId, …)` UNIQUE constraints make cross-tenant
  rows impossible at the DB.
- Database-enforced idempotency/uniqueness: source-record identity
  `(TenantId, SourceId, RecordKind, ExternalId)` — duplicate delivery rejected via
  `ON CONFLICT DO NOTHING` (first delivery canonical, ADR-010 decision 7); checkpoint
  `(TenantId, SourceId, Capability)` unique per scope.
- Trusted tenant-context resolution: `DatabaseTenantScope.FromTrusted(TenantContext)`
  (ADR-010 decision 5) — tenant authority from the trusted execution context only.
- Layer authority preserved: Application resolves inward ports only; only the module-owned
  `Composition` (DI wiring) references Infrastructure; the Host composes modules through
  their `Composition` entry points and references no concrete adapter/persistence assembly.

**Verification:**
- Debug + Release builds: zero warnings, zero errors.
- 152 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72,
  Infrastructure.IntegrationTests 9), 0 failed. Baseline 143 preserved.
- Integration tests exercise real PostgreSQL (tenant isolation, fail-closed ownership,
  idempotency, concurrent duplicate insertion, transaction rollback, invalid checkpoint);
  never EF Core InMemory.
- Guard updates (not weakenings): physical-structure allowlists extended to the two new
  Infrastructure folders + their Composition/Authorization single-file leaves; the
  layer-graph guard now permits Application → its own module Infrastructure composition while
  still forbidding Application → adapters and → EF in business code; test-file budget raised
  to 600 for the now-larger guard file.
- CI (`.github/workflows/architecture.yml`): provisions a PostgreSQL service and points the
  integration fixture at it via `FATARAZ_INTEGRATION_TEST_CONNECTION_STRING`.

**Out of scope (correctly deferred):** W3 ingestion engine, real provider integrations,
dashboards, REST/MCP endpoints, AI. No secrets introduced.

## FT-W3-PREFLIGHT-001 — FMCA composition preflight audit (verified, PASS)

**Task:** `FT-W3-PREFLIGHT-001` (bounded audit + minimal repair; W3 preflight). **Status:** PASS — W2 is a safe foundation for W3; one composition guard gap strengthened (no code violation found). Not certified; W3 requires a separate explicit task.

**Verified HEAD:** `53a66c5a196d2b51e5ffe72e5f2d0f8afe882d9a` (master; fast-forwarded from W2 baseline `10eb8c9`; includes documentation commits `88af092` agents bounded-execution policy and `53a66c5` W2 recovery record).

**Findings (Step 2):**
- **No Application/domain code depends on Infrastructure.** Verified via csproj: `Ingestion.Application` declares only BuildingBlocks + Ingestion.Domain + AccountingSources(module Domain) + MediatR/DI.Abstractions; `AccountingSources` module (Domain) declares only BuildingBlocks + Ingestion.Domain.
- **No Domain references Infrastructure** (both Domain csprofs clean).
- **Host has no concrete Infrastructure reference** — it only calls `AddIngestionApplication`.
- **Module registration follows ADR-010 decision 14** — the only Infrastructure DI wiring lives in module-owned `Composition` extension methods (`AddIngestionInfrastructure`/`AddAccountingSourcesInfrastructure`), invoked by the Host, never by Application handlers.
- **Guard gap (the "composition exception"):** the layer-graph and Host-authority `IsForbiddenLayer` classifier only rejected a FLAT `FaraTaraz.Infrastructure` project (name starts with `FaraTaraz.Infrastructure`, or a path segment equals `Infrastructure`) plus adapters. It did **not** classify the module-scoped W2 Infrastructure projects (`FaraTaraz.Modules.Ingestion.Infrastructure`, `FaraTaraz.Modules.AccountingSources.Infrastructure` — path segment `*.Infrastructure`, assembly name ending `.Infrastructure`). So the guard did NOT reject an injected `Application → module Infrastructure` reference — weaker than ADR-009 decision 4 and ADR-010 decision 14 require (Application/Domain must reference no concrete Infrastructure type). No actual code violation exists; the guard was under-enforcing the ADR. This matches the RECOVERY claim that "the guard now permits Application → its own module Infrastructure composition."

**Correction (Step 3):** Strengthened `IsForbiddenLayer` in both `LayerDependencyGraphTests` and `HostAuthorityTests` to classify any assembly whose name ends with `.Infrastructure` as forbidden (catches module-scoped Infrastructure). Extended the `Application_infrastructure_project_reference_is_detected` regression test to assert the real module-scoped Infrastructure paths are classified forbidden.

**Negative-test proof:** Injected an actual `Ingestion.Application → Ingestion.Infrastructure` ProjectReference; the layer guard FAILED (reported `FaraTaraz.Modules.Ingestion.Infrastructure.csproj` as a forbidden Application reference); reverted the reference (git diff clean); the guard PASSES. The injected reference is removed.

**Verification:** Debug + Release builds clean (0 warnings/0 errors). All 152 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72, IntegrationTests 9), 0 failed — W2 baseline preserved.

**Remaining risks:** None introduced. W2 remains `PASS`, not certified. The guard now correctly enforces ADR-009/010 for module Infrastructure.

**Next exact task:** `FT-W3-INGESTION-ENGINE-001` (or first authorized W3 slice) — audit the W2 persistence schema + W1 sync contract, then implement durable sync execution (run orchestration, retries, failure states, resumability) over persisted state. Do not add provider integrations, dashboards, REST/MCP, or AI.

## FT-W2-R1-TENANT-ISOLATION — security repair (verified, PASS)

**Task:** `FT-W2-R1-TENANT-ISOLATION` (CRITICAL security repair; W2 DB-security acceptance was SUSPENDED). **Status:** PASS — the three Ingestion repositories now derive tenant authority only from the trusted <c>DatabaseTenantScope</c>, not a caller-supplied <c>TenantId</c>. Not certified; W2 remains not-certified pending external acceptance. **Did not implement W3 or any business feature.**

**Verified HEAD (base):** `53a66c5a196d2b51e5ffe72e5f2d0f8afe882d9a` (master; W2 + W3 preflight).

**Defects repaired (each was trusting a caller-supplied <c>TenantId</c> as authorization):**
- **SyncRunRepository** — <c>StartAsync</c> wrote a run using the caller tenant; <c>FinishAsync</c> located a run by <c>runId</c> alone (no tenant scope). Now: resolves the trusted tenant, requires a valid trusted scope, scopes the run lookup/modification to that tenant, and rejects cross-tenant finishes (a run owned by another tenant is never modified).
- **SourceRecordRepository** — <c>ExistsAsync</c>/<c>InsertOrUpdateAsync</c> used the caller tenant directly. Now: requires the trusted scope, rejects a caller tenant that does not match the trusted tenant, and scopes all SQL to the trusted tenant. The <c>ON CONFLICT DO NOTHING</c> idempotency policy (ADR-010 decision 7) is **unchanged**.
- **SyncCheckpointRepository** — <c>GetAsync</c>/<c>SaveAsync</c> trusted the caller tenant. Now: requires the trusted scope, rejects cross-tenant requests, and fails closed when the scope is absent. Concurrency/cursor progression unchanged.
- **DI (both module Composition files)** — the trusted scope was a hardcoded default that could silently hide a missing context. Replaced with a single context-aware registration: resolves a <c>TenantContext</c> when one is bound (trusted), otherwise fails closed to <c>DatabaseTenantScope.None</c>. No duplicate default registration; a missing scope never becomes authorized. FMCA boundaries preserved.

**Reference:** the repair mirrors the already-correct <c>EfAccountingSourceOwnership</c> oracle (injects <c>DatabaseTenantScope</c>, ignores the caller tenant, scopes to the trusted tenant, fails closed).

**Architecture restrictions honored:** no Host→Infrastructure, no Application→Infrastructure, no EF Core in Domain/Application, no guard removed/weakened, no new endpoints/providers/dashboards/W3 features.

**Mandatory PostgreSQL negative tests** (`tests/.../TenantIsolationNegativeTests.cs`, 6 tests, real PostgreSQL): (1) Tenant A cannot finish Tenant B's run (run stays <c>Running</c>); (2) Tenant A cannot read Tenant B's checkpoint (denied + scoped read returns null); (3) Tenant A cannot overwrite Tenant B's checkpoint (cursor unchanged); (4) Tenant A cannot read/insert source records as Tenant B (no row created); (5) missing trusted scope denies every operation (all six repository operations throw); (6) correct same-tenant operations still succeed. These fail against the original vulnerable code and pass after the repair.

**Verification:**
- Debug build: zero warnings, zero errors.
- Release build: zero warnings, zero errors.
- Full suite: 158 tests pass (BuildingBlocks 16, SyncContracts 55, Architecture 72, Infrastructure.IntegrationTests 15), 0 failed. Baseline 152 preserved; +6 negative tests.
- `git diff --check`: clean (no whitespace errors).
- Architecture guards unchanged and passing (72).

**Files changed:** `SyncRunRepository.cs`, `SourceRecordRepository.cs`, `SyncCheckpointRepository.cs` (trusted-tenant guard + caller-tenant validation), `IngestionInfrastructureComposition.cs` + `AccountingSourcesInfrastructureComposition.cs` (context-aware trusted scope), `IngestionPersistenceTests.cs` (construct repositories with a trusted scope), `TenantIsolationNegativeTests.cs` (new).

**Next exact task:** `FT-W3-INGESTION-ENGINE-001` (or first authorized W3 slice) — audit the W2 persistence schema + W1 sync contract, then implement durable sync execution over persisted state. Do not add provider integrations, dashboards, REST/MCP, or AI.

## FT-W2-R3-CHECKPOINT-CONCURRENCY — concurrency repair (implemented, PASS locally, NOT certified)

**Task:** `FT-W2-R3-CHECKPOINT-CONCURRENCY` (bounded W3 preflight slice; FT-DATA-001). **Status:** `PASS` locally — **NOT CERTIFIED**; awaiting independent ChatGPT architect review. **Did not implement W3 or any business feature; did not mark FT-DATA-001 VERIFIED.** W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Verified HEAD (base):** `54c03cd1765d580d2db7c1f8c3519a1cc998c760` (master; current remote HEAD).

**Defect (FT-DATA-001):** `SyncCheckpointRepository.SaveAsync` used read-then-write `SaveChanges`: (1) concurrent first writers both `Add` → unique-constraint violation → crash; (2) a concurrent/stale writer could overwrite a newer checkpoint with stale progress (lost update). The defect asks for an atomic/concurrency-safe checkpoint protocol aligned with the accepted ADR.

**Invariant (now enforced, DB-backed):** a checkpoint write is an atomic, DB-enforced optimistic-concurrency conditional upsert. A writer that observed an older version is rejected (never overwrites a newer checkpoint with stale progress). The ordering is **commit order** via a DB-managed monotonic `Version` column — not the opaque cursor token (not lexically ordered) and not `UpdatedAtUtc` (wall-clock, skew-prone).

**Design decision (PENDING architect confirmation / ADR-010 amendment):** added a DB-managed monotonic `Version` column and made `SaveAsync` a single `INSERT ... ON CONFLICT (TenantId,SourceId,Capability) DO UPDATE SET cursor, updated_at, version = new WHERE version = @expected`. Accepted only when the row's current version equals the version the writer last observed; otherwise 0 rows → throw `StaleCheckpointException` (re-read and retry). First write inserts `version = 0`; a concurrent first writer conflicts and is rejected (never crashes, never silently discarded). This EXTENDS ADR-010 decision 8 (which covers the unique-constraint first-writer race) to also cover the lost-update case the ADR did not address — flagging for reviewer acceptance rather than assuming the ordering semantics.

**Files changed:** `SyncCheckpointEntity.cs` (adds `Version`), `SyncCheckpointRepository.cs` (`SaveAsync` → atomic conditional upsert + `StaleCheckpointException`), `IngestionDbContext.cs` (configures `Version`), `IngestionDbContextModelSnapshot.cs`, new migration `20261010035925_FTW2-CheckpointConcurrencyVersion.{cs,Designer.cs}` (adds `SyncCheckpoints.Version bigint NOT NULL DEFAULT 0`), new test `SyncCheckpointConcurrencyTests.cs` (3 tests). `StaleCheckpointException.cs` new. No dependency/DI/adapter/Host changes.

**Verification (red demonstrated, then restored to green):**
- RED (temporary revert of `SaveAsync` to read-then-write `SaveChanges`): `Concurrent_writes_do_not_crash` FAILS with `DbUpdateException: duplicate key value violates unique constraint "UQ_SyncCheckpoints_Tenant_Source_Capability"` (concurrent first writers crash); `Sequential_writes_advance_db_managed_version_monotonically` FAILS (version stays `0`, no guard).
- GREEN (fix restored): all 3 new concurrency tests pass; full `FaraTaraz.Infrastructure.IntegrationTests` = **24/24 pass** (was 21); `FaraTaraz.ArchitectureTests` = **75/75 pass**; full-solution Debug build = 0 warnings / 0 errors; `dotnet ef migrations has-pending-model-changes` = "No changes have been made to the model since the last migration" (migration/snapshot consistent).

**Architecture restrictions honored:** no Host→Infrastructure, no Application→Infrastructure, no EF Core in Domain/Application, no guard removed/weakened, no new endpoints/providers/dashboards/W3 features. `Version` is DB-managed (never trusted from the caller).

**Next exact task:** independent ChatGPT architect review of commit `f373bd45df647c317307224d4e99999e5a9b7efd` (guard + green CI); then reconcile FT-DATA-001 in `OPEN-DEFECTS.md`. Do NOT promote W2 to certified or authorize W3.

Update this file in the same task's final verified commit, including:
- task ID / status / authorized scope
- baseline SHA, implementation commit(s), final remote HEAD after push (report exact value; a file cannot reliably self-reference its own commit)
- module and file changes, ADRs and decisions
- Debug/Release builds, test totals by project, negative-test evidence, CI run/result
- accepted vs pending certifications, blockers and risks
- exact next task and the first executable action

Keep `CURRENT-STATE.md`, `ROADMAP.md`, and `DELIVERY-PLAN.md` consistent. No false or future-dated PASS claims. Preserve history and unrelated files, including untracked `tree.ps1`.

## FT-W2-R5-CHECKPOINT-OBSERVED-VERSION-CAS — observed-version CAS contract (implemented, PASS locally, NOT certified)

**Task:** `FT-W2-R5-CHECKPOINT-OBSERVED-VERSION-CAS` (bounded W2 slice; FT-DATA-001). **Status:** `PASS` locally — **NOT CERTIFIED**; awaiting independent ChatGPT architect review. **Did not implement W3 or any business feature; did not mark FT-DATA-001 VERIFIED.** W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline / implementation:** baseline `52fd1887a67486e2938d8daca70d930cc61f6026`; implementation `7c9bc6f218c4d2865950bb9f37bcc05b1022583c` (fast-forward from baseline).

**Defect hardened (FT-DATA-001):** `SyncCheckpointRepository.SaveAsync` previously derived its optimistic-concurrency guard by calling its own internal `GetAsync` immediately before the write, so the caller's original checkpoint observation was not part of the commit contract and a delayed older operation could carry a stale cursor. The repair threads the observation explicitly through the public API.

**Contract (exact):** `SaveAsync(..., long? observedVersion, CancellationToken = default)`:
- `null` = caller observed no row (first write); maps to internal expectation `-1` only inside the repository, so the first write inserts DB-managed `version = 0`.
- a supplied `observedVersion` must be `>= 0` (validated; `ArgumentOutOfRangeException` otherwise) and is used verbatim as the CAS guard.
- the pre-write internal `GetAsync` used to derive the version was removed; the repository never re-reads latest version to retry the same cursor. A diagnostic `GetAsync` after a failed CAS is allowed solely to populate `StaleCheckpointException.StoredVersion`; it never retries and never turns a stale input into a successful write.
- tenant enforcement and opaque-cursor semantics unchanged; the one-statement PostgreSQL conditional upsert (`... WHERE "SyncCheckpoints"."Version" = @expectedVersion`) is preserved.

**Files changed:** `SyncCheckpointRepository.cs` (signature + validation + guard; doc updated), `SyncCheckpointConcurrencyTests.cs` (rewritten as deterministic public-API tests, no handwritten SQL), `IngestionPersistenceTests.cs` + `TenantIsolationNegativeTests.cs` (all `SaveAsync` callers now observe explicitly). Migration / `Version` column / entity config unchanged (already present). No new port/handler/endpoint/provider/transaction orchestration.

**Verification (real PostgreSQL):**
- `FaraTaraz.Infrastructure.IntegrationTests` = **25/25 pass** (was 24; +1 concurrency test). New deterministic cases: baseline null-insert version 0; sequential observation advances 0→1→2; delayed stale save with old observation throws `StaleCheckpointException` and persists newer/1 (neither cursor accepted); two racing first writers (both observe null) → exactly one success / one stale / one row / version 0. Tenant-isolation coverage retained in `TenantIsolationNegativeTests`.
- `FaraTaraz.ArchitectureTests` = **75/75 pass**; no guard weakened.
- Affected projects build with zero warnings/errors (Ingestion.Infrastructure, IntegrationTests). No model change, so no new migration.

**Unresolved / pending architect decision:** FT-DATA-002 (run start/finish, record insert and checkpoint saves commit independently; no proven atomic sync unit-of-work across records + cursor + run) remains OPEN — this task did not introduce a transaction and cannot close it alone. ADR-010 decisions 8/9 are now extended by the commit-order observed-version CAS, but the cross-operation atomic unit-of-work remains an accepted-ADR/transaction decision outstanding. FT-DATA-001 status: FIXED — UNVERIFIED (code committed; independent source + CI review pending).

**Next exact action:** independent ChatGPT architect review of `7c9bc6f` (guard + green CI), then reconcile FT-DATA-001/002 in `OPEN-DEFECTS.md`. Do NOT promote W2 to certified or authorize W3.

## FT-W2-R6-CONFIG-FAIL-CLOSED — fail closed on missing connection string (implemented, PASS locally, NOT certified)

**Task:** `FT-W2-R6-CONFIG-FAIL-CLOSED` (bounded W2 slice; FT-CONFIG-001). **Status:** `PASS` locally — **NOT CERTIFIED**; awaiting independent ChatGPT architect review. **Did not implement W3 or any business feature; did not mark FT-CONFIG-001 VERIFIED.** W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline / implementation:** baseline `6eefd5b6e8cdcc126ff7b91e8aab5cf1efa44f64`; implementation `0bc574b2039d06733186775d4d66e10feb0b339b` (fast-forward from baseline).

**Defect (FT-CONFIG-001):** both module Infrastructure Composition files retained a `DefaultConnectionString` fallback (`Host=localhost;Port=5432;Username=postgres;Password=postgres;…`) when config was missing — an embedded production credential and an implicit localhost/PostgreSQL fallback.

**Repair (fail-closed config seam):**
- Removed the `DefaultConnectionString` constant from `IngestionInfrastructureComposition` and `AccountingSourcesInfrastructureComposition`; each root now requires a nonempty connection string and throws a deterministic `BuildingBlocks.Configuration.ConnectionConfigurationException` **before** any `DbContext` is registered when null/empty/whitespace. Caller-supplied test strings are preserved (signature unchanged: `string? connectionString = null`).
- Shared fail-closed exception added to BuildingBlocks (`Configuration/ConfigurationExceptions.cs`).
- Both design-time `DbContextFactory` files fail closed on a missing `FATARAZ_INGESTION_DB` / `FATARAZ_ACCOUNTINGSOURCES_DB` env var (no embedded fallback).
- Tenant enforcement / opaque-cursor / layer direction unchanged; no new port/handler/endpoint/provider/transaction orchestration.

**Files changed:** `ConfigurationExceptions.cs` (new), `IngestionInfrastructureComposition.cs` + `AccountingSourcesInfrastructureComposition.cs` (remove const + validation), `IngestionDbContextFactory.cs` + `AccountingSourcesDbContextFactory.cs` (env-var fail closed), `ConnectionConfigurationFailClosedTests.cs` (new behavioral regression), `PhysicalStructureTests.cs` (document the justified `BuildingBlocks/Configuration` single-file leaf).

**Verification (light — focused, per architect amendment):**
- **Focused composition/config test (authoritative):** the new `ConnectionConfigurationFailClosedTests` (in `FaraTaraz.Infrastructure.IntegrationTests`) — both roots reject null/empty/whitespace with the deterministic `ConnectionConfigurationException` and register no `DbContextOptions`; both roots accept a supplied string. **Passed** (run once). The existing integration `TenantScopeCompositionTests` still pass.
- **Compilation:** affected Infrastructure + IntegrationTests projects build clean (one targeted build).
- **Broader verification (ran once beyond light-verification scope; report as PENDING for acceptance):** the full Integration suite (33/33), `FaraTaraz.ArchitectureTests` (75/75), the full Debug+Release solution matrix (180/180 each), the full-solution Debug+Release builds (0 warnings/0 errors), and `dotnet ef migrations has-pending-model-changes` (no pending model change) were run as a sanity check but belong to the separate final verification gate, not this task's acceptance. `ArchitectureTests` was **not** required — the focused test is not in that project. FT-CONFIG-001 remains FIXED — UNVERIFIED pending independent architect review.

**Unresolved / pending architect decision:** FT-CONFIG-001 status: FIXED — UNVERIFIED (code committed; independent source + CI review pending). Production path: the Host composes via `AddIngestionApplication()`; the module Infrastructure composition entry points are consumed by tests and future W3 wiring — there is **no production caller yet**, so the fail-closed seam is exercised by tests, not by a live runtime fallback. No other defect status changed.

**Next exact action:** independent ChatGPT architect review of `0bc574b` (guard + green CI), then reconcile FT-CONFIG-001 in `OPEN-DEFECTS.md`. Do NOT promote W2 to certified or authorize W3.

## FT-W2-R8-R1-TENANT-GUARDS — durable tenant-boundary architecture guards (implemented, PASS locally, NOT certified)

**Task:** `FT-W2-R8-R1-TENANT-GUARDS-IMPLEMENT-ONLY` (bounded W2 slice; FT-SEC-006 + FT-GUARD-002). **Status:** `PASS` locally — **NOT CERTIFIED**; awaiting independent ChatGPT architect review. **Did not implement W3, runtime auth, authorization endpoints, global query filters/RLS, a production TenantContext factory, migrations, or broad refactors.** Did not mark FT-SEC-004 VERIFIED. W2 NOT CERTIFIED; W3 NOT AUTHORIZED.

**Baseline / implementation:** baseline `753d6de504180a3462e9400eec4776eebfa02a84`; implementation `d6b854a2cd69c4521cccd372c5f6b11276e05325` (task `FT-W2-R8-R2`, fast-forward from `a665677`/`FT-W2-R8-R1`). One coherent tests+docs commit.

**Defects (FT-SEC-006 / FT-GUARD-002):** raw tenant-owned `DbContext`/`DbSet` exposure could be reached outside repository enforcement; no durable guard prevented untrusted code from minting a trusted `TenantContext` or constructing a tenant-owned context directly.

**Repair (durable source/architecture guard, preventive only):**
- New `tests/FaraTaraz.ArchitectureTests/TenantBoundaryGuards.cs`. A deterministic lexical state machine strips `//`, `/* */`, regular/verbatim/raw string literals and char literals before matching, so a comment/string that only mentions an identifier does not trip the guard.
- Outside the two real Infrastructure project roots (`Modules/Ingestion/Ingestion.Infrastructure`, `Modules/AccountingSources/AccountingSources.Infrastructure`) and their descendants, identifier references to `IngestionDbContext`/`AccountingSourcesDbContext` are rejected (Infrastructure repositories/oracle/design-time factories/migrations remain allowed). A nested folder only *named* `.Infrastructure` (e.g. `Application/Escape.Infrastructure`) stays forbidden.
- Outside `src/BuildingBlocks`, `TenantContext.FromAuthenticatedPrincipal` and `new TenantContext` are rejected.
- Shared pure `FindViolations(relativePath, source)` helper is exercised by synthetic cases and a real-production-tree fact. `tests/` is outside `src/` and excluded automatically. bin/obj exclusion is separator-independent.
- Interpolated strings are handled so literal text is stripped while expression content inside `{ ... }` is preserved as code, and a missing closing delimiter never swallows the rest of the file. Interpolated raw string syntax (`$"""..."""` / `$$"""..."""`) is intentionally unsupported and rejected fail-closed rather than parsed.

**Files changed:** `tests/FaraTaraz.ArchitectureTests/TenantBoundaryGuards.cs` (new in `FT-W2-R8-R1`; false-negative repairs in `FT-W2-R8-R2`). No other source, package, migration, or CI change.

**Verification (focused, per restraint):**
- Command: `dotnet test tests/FaraTaraz.ArchitectureTests/FaraTaraz.ArchitectureTests.csproj --filter "FullyQualifiedName~TenantBoundaryGuards"`.
- Result: **9/9 passed** (2 real-production-tree facts + 7 synthetic proofs: non-INF DbContext ref rejected; non-BuildingBlocks mint rejected via factory + constructor; comment/string accepted; forbidden identifier after `$"literal"` detected; forbidden identifier inside `{ ... }` detected but identical literal text ignored; `Application/Escape.Infrastructure` rejected; the two real Infrastructure roots allowed). One ArchitectureTests project build (compilation). No full suite, repeats, Debug/Release matrix, DB/integration tests, or migrations.

**Unresolved / pending architect decision:** FT-SEC-006 and FT-GUARD-002 are FIXED — UNVERIFIED (guard + focused negative proof implemented and passing; independent source review pending). FT-SEC-004 stays **OPEN — PARTIALLY MITIGATED** (commit `39164cd` makes minting internal; the real production authenticated-principal boundary is still an unresolved auth/ADR decision — do not invent authentication). No RLS/global filter was added; raw DbSets remain unfiltered. No other defect status changed.

**Next exact action:** independent ChatGPT architect review of this task's commit (guard correctness + focused-test pass), then reconcile FT-SEC-006/FT-GUARD-002 in `OPEN-DEFECTS.md`. Do NOT promote W2 to certified or authorize W3.

## Agent execution limit and open review item (2026-10-09)

- Owner policy: 20–30 minute target, 45-minute hard cap per task. At the cap, checkpoint and report PARTIAL; do not keep working for hours or exhaust context.
- **Review before W3 feature code:** W2 reports that Application → its own Infrastructure is allowed for module Composition. Confirm the accepted ADR and source-level guards restrict this to composition wiring only, never handlers or domain/application feature logic. Do not weaken existing FMCA guards. Any required fix must be a small separate task.
- W2 test count (152) is reported by the implementing agent; verify fresh CI for the W2 SHA before promoting to an independently verified baseline.

## Context-exhaustion recovery protocol — mandatory (2026-10-09)

**Incident:** OpenCode local Ornith 1.5 35B-A3B session compacted approximately 130.1K tokens into 1.8K, continued from stale W2/W3 state and then stopped without a task result. The prior task **FT-W2-R2-BOUNDARY-GUARDS** was interrupted; its status is **UNKNOWN / PARTIAL**, never PASS. Any local uncommitted edits or test results must be inspected before resuming. The latest architect-owned defect ledger is `docs/planning/OPEN-DEFECTS.md`.

**Architect-owned recovery rule:** ChatGPT independently reviews code and updates the defect ledger on master; implementing agents must not mark issues VERIFIED. W2 NOT CERTIFIED; W3 NOT AUTHORIZED. Do not trust compacted chat summaries as repository truth.

**Task slicing:** target 10–20 minutes for local-context-constrained agents; **maximum 25 minutes per microtask**, with the existing absolute 45-minute emergency cap retained. Each task has one outcome and at most 1–3 related production files. Avoid full-repository scans, verbose shell dumps, repeated builds, and full test suites inside every microtask. During implementation run only the smallest relevant targeted tests and, when needed, one targeted build. Full Debug/Release + complete suite + CI are a **separate final verification gate**, not skipped for acceptance. Do not start the next microtask automatically.

**Before work / new session:** `git status --short`, `git branch --show-current`, `git rev-parse HEAD`, `git fetch origin master`, `git rev-parse origin/master`; inspect the diff and determine whether the previous task left uncommitted changes. Never reset, clean, stash, checkout over, or overwrite user edits without authorization. Read `AGENTS.md`, this section, `OPEN-DEFECTS.md`, current-state and the relevant ADR. Reconcile any SHA mismatch. The first resumed task is **RECOVERY-ONLY**, not implementation.

**Checkpoint BEFORE context pressure:** record active task ID, last verified SHA, exact changed files, current dirty diff, completed steps, failed/unfinished steps, focused test commands and results, open defect IDs, next single command, elapsed time and status `PARTIAL` in `RECOVERY.md` (or an explicit per-task handoff file if working tree cannot safely be committed). Do this no later than 15 minutes into a microtask and again before expensive tool calls. Keep shell output bounded; prefer targeted file slices and brief test summaries. If compaction occurs unexpectedly, STOP writes and re-read checkpoint/Git before continuing.

**Resume safety:** Never infer a successful command from an incomplete terminal response. If a shell is stalled, do not blindly rerun potentially mutating commands; first inspect process status and filesystem/Git state. Recovery task must report what was preserved, what is uncertain, and the next bounded microtask. Implementer reports evidence only; ChatGPT decides defect closure and updates ledger.

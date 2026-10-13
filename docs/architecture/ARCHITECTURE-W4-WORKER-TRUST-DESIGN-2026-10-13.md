# FaraTaraz — W4 Design: Trusted Local Worker Entrypoint (customer-installed, central-ready)

**Task:** `FT-REBASELINE-W4-WORKER-TRUST-DESIGN-001` — ANALYZE + DESIGN ONLY.
**Status:** DESIGN PROPOSAL for architect review. **No production code, tests, packages, schema or ADR status was changed.**
**Baseline:** `fda7c28` (origin/master, fetched; worktree clean). Last accepted implementation: `115e799` (W3, Linux CI 38311155692 SUCCESS).
**Binding inputs:** `ARCHITECTURE-W4-DUAL-DELIVERY-DECISION-2026-10-13.md`, `ARCHITECTURE-W4-DEPLOYMENT-DECISION-2026-10-13.md`, `ARCHITECTURE-W4-CONNECTIVITY-DECISION-2026-10-13.md`, ADR-009/010/011, `structure.md` §10, constitution.

> Method: every architectural claim below is read from the source at the baseline SHA (file:line). Where a fact is only user-reported or unavailable, it is labelled **USER-REPORTED** or **UNKNOWN**. Method names, not line numbers, are used where a symbol is the real anchor.

---

## 1. Source-grounded execution / trust dependency map

### 1.1 Current production runtime chain (as built)

```
[delivery]                      ✗ DOES NOT EXIST — no Worker, no HTTP/Endpoints project
   │
IAuthenticatedPrincipal         ✗ DOES NOT EXIST — ADR-011 Proposed; no port, no implementation
   │
trusted TenantContext           ⚠ type exists; NEVER produced outside tests
   │   TenantContext.cs: internal ctor (L53) + internal FromAuthenticatedPrincipal (L67);
   │   only public members are read-only + AssertTrusted() (L74-81).
   │   FaraTaraz.BuildingBlocks.csproj grants InternalsVisibleTo to 3 TEST assemblies only.
   ▼
ISender → RunCustomerSyncCommand → RunCustomerSyncHandler.Handle   ✓ implemented
   │   admission: command.Tenant.AssertTrusted() (L64) → trusted-scope agreement via
   │   IIngestionTrustedTenantScope (L68-74) → IAccountingSourceOwnership.IsOwnedByAsync (L77-86)
   │   → IAccountingSourceProviderResolver.ResolveProviderAsync (L91-100)
   │   → IngestionSyncPolicy C5/C6/C8 (L104-129) → ISyncablePort<SourceCustomer>.SyncAsync (L131)
   │   → IIngestionWriteStore.ExecuteInTransactionAsync (L151-216): records + checkpoint CAS + run
   ▼
Infrastructure
   │   EfIngestionWriteStore: TrustedTenantId ← DatabaseTenantScope (L68); RequireTrustedTenant
   │   fails closed (L100-102); session invalidated in finally (L91-97)
   │   IngestionUnitOfWork.ExecuteAsync: one explicit BeginTransaction/Commit (L38-53)
   │   SyncCheckpointRepository.SaveAsync: version-guarded ON CONFLICT CAS (L86-153)
   │   SourceRecordRepository.InsertOrUpdateAsync: unique-key ON CONFLICT DO NOTHING (L113-145)
   ▼
PostgreSQL
```

### 1.2 Existing composition roots and registrations

| Concern | Where | Registered? |
|---|---|---|
| Host composition entry | `FaraTarazHost.ConfigureFaraTaraz(connectionString)` (`FaraTarazHost.cs` L28-39) | yes — aggregates the two module seams; **class library, no `Program.cs`** |
| Ingestion module seam | `IngestionModuleComposition.AddIngestionModule` L26-36 | yes |
| AccountingSources module seam | `AccountingSourcesModuleComposition.AddAccountingSourcesModule` L25-35 | yes |
| Ingestion persistence | `IngestionInfrastructureComposition.AddIngestionInfrastructure` L30-78 | `IngestionDbContext` (L43), `DatabaseTenantScope` TryAdd (L50-56), 3 repositories (L58-60), `IngestionUnitOfWork` (L64), `EfIngestionWriteStore` + 3 Application ports (L68-74) |
| AccountingSources persistence | `AccountingSourcesInfrastructureComposition.AddAccountingSourcesInfrastructure` L33-66 | `AccountingSourcesDbContext` (L46), `DatabaseTenantScope` TryAdd (L53-59), `IAccountingSourceOwnership`→`EfAccountingSourceOwnership` (L61), `IAccountingSourceProviderResolver`→`EfAccountingSourceProviderResolver` (L62) |
| MediatR handlers | `IngestionApplicationComposition.AddIngestionApplication` L21-29 | yes (assembly scan) |
| Connection string | passed as a **parameter** to both module seams; blank fails closed with `ConnectionConfigurationException` (Ingestion L36-41, AccountingSources L39-44) | yes (no secret store, no `IConfiguration`, no user-secrets) |

### 1.3 Missing real production registrations

| Missing | Impact |
|---|---|
| Any delivery entrypoint (Worker/HTTP) | `ISender` is never called in production |
| A trusted `TenantContext` producer | `DatabaseTenantScope` resolves to `None` ⇒ every tenant-scoped op fails closed |
| `ISyncablePort<SourceCustomer>` registration | `RunCustomerSyncHandler` cannot be resolved by DI (registered only in `CqrsTestSupport.cs`) |
| A concrete provider adapter registration | no provider available |
| Provider credential storage | `AccountingSourceEntity` has **no** credential column; `AccountingSource` has none (Constitution I.35) |
| Job scheduling / execution lease | none; only checkpoint CAS + idempotency constraints (§1.5) |
| Hosting runtime package | **no** `Microsoft.Extensions.Hosting`, `BackgroundService` or `IHostedService` anywhere in `src/` |

### 1.4 Trust minting mechanism (exact)

- `TenantContext` constructor and `FromAuthenticatedPrincipal` are **`internal` to `FaraTaraz.BuildingBlocks`** (`TenantContext.cs` L53, L67). No public factory.
- `DatabaseTenantScope.FromTrusted(TenantContext)` calls `context.AssertTrusted()`; the constructor is private; `DatabaseTenantScope.None` is the fail-closed default.
- Both Infrastructure compositions bind the scope with `TryAddScoped<DatabaseTenantScope>(sp => GetService<TenantContext>() is {IsTrusted:true} ? FromTrusted(...) : None)` — they **read** a context; they never register one. With nothing bound, the scope is `None` ⇒ fail-closed.
- `FaraTaraz.BuildingBlocks.csproj` grants `InternalsVisibleTo` to exactly three **test** assemblies (pinned by `TenantTrustSourceTests`). Production cannot currently mint authority.
- **Conclusion:** the trust boundary is *defined* but has no door (ADR-011 §"no sanctioned producer"). Any W4 delivery must add the door *without* creating a public factory.

### 1.5 Concurrency / repeat-execution protection (what exists, what does not)

- **Exists:** `SyncCheckpointRepository.SaveAsync` is an atomic version-guarded `INSERT … ON CONFLICT … WHERE Version = observed` CAS (stale writer ⇒ `StaleCheckpointException` ⇒ rollback); `SourceRecordRepository.InsertOrUpdateAsync` uses a unique `(TenantId, SourceId, RecordKind, ExternalId)` key with `ON CONFLICT DO NOTHING`; `IngestionUnitOfWork` commits records+checkpoint+run atomically.
- **Does not exist:** any **execution lease**. Two concurrent workers can both invoke the provider (double provider traffic) and both attempt the write; only one CAS wins and the other rolls back. That is safe for data integrity but not for provider load or for "one bounded step per source/capability at a time".
- **Also absent:** `AccountingSourceStatus` (`AccountingSource.cs` L56-62: `Disabled/Enabled/Syncing/Error`) is persisted (`AccountingSourceEntity.Status` L33) but **never consulted** by ownership or provider resolution. A `Disabled` source is still owned and resolvable today — a W4 fail-closed gap.

### 1.6 Asan connectivity evidence

| Claim | Status | Evidence |
|---|---|---|
| Provider-neutral capability seam exists (`IAccountingProvider`, `ISyncablePort<T>`, 6 typed ports) | **VERIFIED** | `IAccountingProvider.cs` L13-16; `ISyncablePort.cs` L13-38; `CapabilitySourcePorts.cs` L6-33 |
| Only a deterministic Mock adapter exists | **VERIFIED** | `MockAccountingProvider.cs` L21; `MockCapabilitySync.cs` L21-109 |
| No real Asan/Sepidar/Holoo/Mahak connector, SDK, HTTP client or vendor DTO in the repo | **VERIFIED** | repo-wide grep: vendor names appear only in doc comments, `ProviderLeakageTests` (forbids them in platform code) and one `ProviderId("Asan")` test literal; **zero** `HttpClient`/vendor SDK |
| No remote endpoint / auth protocol / operations / schema | **UNKNOWN** | nothing in source or in repo docs |
| Asan runs locally on each customer machine (three installations) | **USER-REPORTED** | `ARCHITECTURE-W4-CONNECTIVITY-DECISION` §"Verified user-provided facts" |
| Asan was integrated with WordPress in at least one configuration | **USER-REPORTED** | same document |
| WordPress integration implies a documented public Asan API | **NOT ESTABLISHED** (inference boundary) | the architect document explicitly warns it does not prove an API, auth or permission |
| Local PostgreSQL vs central PostgreSQL | **UNKNOWN** | connection string is deploy-time parameter only |

### 1.7 What the Worker can and cannot do today

- **Can:** reuse `FaraTarazHost.ConfigureFaraTaraz(connectionString)`, the module seams, MediatR registration and every persistence/ownership/resolver registration unchanged; invoke `RunCustomerSyncCommand` through `ISender`; pass a `CancellationToken`.
- **Cannot (today):** obtain a trusted `TenantContext`; resolve `ISyncablePort<SourceCustomer>` (no adapter registered); store provider credentials; run on a schedule; prevent concurrent double execution; fail closed on a `Disabled` source.

---

## 2. Minimal local Worker architecture (proposed, NOT implemented)

### 2.1 Shape

```
new project: src/Host/FaraTaraz.Worker/           (delivery adapter; NEW MINIMAL COMPONENT)
  Program.cs                                      generic host (Microsoft.Extensions.Hosting, cross-platform)
    └─ AddFaraTarazWorker(configuration)
         ├─ FaraTarazHost.ConfigureFaraTaraz(connectionString)     ← UNCHANGED composition (reuse)
         ├─ AddIngestionAdapter(configuration)                      ← adapter seam registration (§2.6)
         ├─ AddWorkerAuthority(configuration)                       ← trust seam (§2.4)
         ├─ AddExecutionLease(...)                                  ← §2.7
         └─ AddHostedService<CustomerSyncWorker>()                  ← bounded scheduling loop
```

The Worker is **one more delivery adapter** next to the future API. It contains **no** business rule,
no persistence write, no provider logic and no tenant policy beyond *verifying the provisioned grant*
and *passing the resulting trusted context* into the existing Application command.

### 2.2 Installation and first-time enrollment

1. Operator installs PostgreSQL (local) and the Worker as a Windows Service (or, for central, the same binary under a service/container).
2. **Out-of-band provisioning (not via the Worker's own job input):** the operator/FaraTaraz provisions `Tenant`, `AccountingSource` (with `Provider`), and their credentials for this installation. Provisioning is a separate, audited step — never a job payload.
3. The installation is bound to a **service identity** (service SID/account + machine) and to the provisioned (tenant, source) grant.
4. Enrollment produces a **grant record** the Worker can *verify*, not merely read (§2.4).

### 2.3 Trusted service identity

- The Worker runs under a **dedicated least-privilege Windows service account** (no interactive logon). This gives *operational* identity only — it is **not** tenant authority.
- Tenant authority must come from the verified grant (§2.4), never from the service account, the config file's tenant id, or the job payload (explicit W4 requirement).

### 2.4 Trust boundary — the exact component that verifies the grant

**Proposed seam (extends ADR-011's shape; no new assembly):**

- `BuildingBlocks.Tenancy` gains the ADR-011 `IAuthenticatedPrincipal` port (`IsAuthenticated`, `TenantId`) — still no public factory.
- The *delivery/admission* layer owns the concrete producer: a `IWorkerAuthority` / `IServiceGrantVerifier` implemented in the **Worker** (delivery), which:
  1. loads the provisioned grant (tenant id + source scope + service-identity binding),
  2. **verifies** it (signature/secret binding, see blocker B1),
  3. only then calls the sanctioned establishment seam to mint a trusted `TenantContext` (reusing the internal factory), and
  4. hands it to `ISender.Send(new RunCustomerSyncCommand(ctx, request, intent))`.
- Failure at any step ⇒ **no** trusted context ⇒ `DatabaseTenantScope.None` ⇒ the handler's `AssertTrusted()` throws ⇒ **zero provider calls, zero writes** (already proven by the PostgreSQL negative suite).

**What does NOT establish authority (rejected):** a JSON tenant id in config or job payload; the Windows service account alone; a locally created row; a test/fake principal; a public `TenantContext` factory.

**BLOCKER B1:** no real provisioning authority (issuer/verification material) exists yet. The cryptographic verification of the grant requires an external decision (see §8). Until then this seam can only be *designed*, not honestly implemented.

### 2.5 Tenant and AccountingSource binding

- The grant binds exactly one `(TenantId, AccountingSourceId, capability)` scope per job. The Worker may iterate multiple provisioned grants (multi-customer/multi-source), but **each job re-verifies its own grant**; grants are never derived from a loop variable or payload.
- `AccountingSourceStatus` **must** be consulted before provider access (new fail-closed check, §1.5). Proposed: extend the ownership/resolution read to require `Status == Enabled`, or add a `ISourceActivation` port. **NEW MINIMAL COMPONENT** (small, in `AccountingSources`).

### 2.6 Provider transport (local now, remote later)

- Keep the existing seam: Application depends only on `IAccountingProvider`/`ISyncablePort<T>`; the Worker registers a concrete adapter.
- **Local first:** an Asan adapter that talks to the locally installed accounting software; transport details **UNKNOWN** (blocker B2).
- **Remote later:** the same adapter interface, a different transport (authenticated remote API, or an on-premises bridge/agent if the accounting software is only LAN/PC-reachable). Chosen by **validated connection configuration**, never by branching in Application.
- **No fake provider in production.** A Mock adapter stays test-only.
- Credentials: per-source secret stored outside source control (Windows DPAPI/credential store or an explicitly approved vault), referenced by the `AccountingSource` row, rotated without code change, never logged.

### 2.7 Job trigger and bounded execution

- **First delivery: a Worker loop over provisioned grants** (interval/`TimeProvider`-based), each iteration executing **exactly one bounded step** via `RunCustomerSyncCommand` (the engine already does one page per call) and continuing only until the page reports `IsComplete`.
- **Execution lease (NEW MINIMAL COMPONENT):** a PostgreSQL advisory lock or a lease row keyed by `(TenantId, SourceId, Capability)` so that at most one worker executes a given scope at a time. Complements (does not replace) the existing checkpoint CAS. **Note:** this must not become a new persistence write path bypassing the Application boundary — the lease is a delivery-level concern (advisory lock on the shared connection or a dedicated lease store), not a second ingestion writer.
- **Manual/queued trigger** remains a future option (same command).

### 2.8 Retry, failure handling, cancellation, restart

- **Retry:** deliberately *not* in the engine (ADR-010 §11). The Worker decides a bounded retry policy per job; the classification comes from the Domain/Contracts taxonomy (`SyncTransientException` vs `SyncPermanentException`, `SyncModeNotSupportedException`), plus `StaleCheckpointException` (re-read and retry once) and `UnsupportedChangedRecordException`/`SyncAlreadyCompletedException` (do **not** retry).
- **Cancellation / graceful shutdown:** pass `IHostApplicationLifetime.ApplicationStopping`/hosted-service `stoppingToken` as the `CancellationToken` through `ISender.Send` → handler → `ExecuteInTransactionAsync` → `IngestionUnitOfWork.BeginTransactionAsync/CommitAsync`. Cancellation before commit rolls back (existing behavior); it must never be wrapped or reclassified (existing `OperationCanceledException` propagation).
- **Restart after partial sync:** safe by construction — records+checkpoint+run commit atomically, so a crash mid-step leaves no advanced cursor; the next run resumes from the persisted `Active` checkpoint. A `Completed` checkpoint requires an explicit `Restart` intent.

### 2.9 Logging without secrets or customer-data leakage

- Log **job lifecycle + counts + typed failure category + correlation id**, never credentials, never raw provider payloads, never customer PII fields.
- The structured-diagnostics scaffold was removed in W3; W4 should use the standard `Microsoft.Extensions.Logging` supplied by the hosting package, with a delivery-layer redaction helper. (Do **not** re-introduce a speculative foundation "error model".)

### 2.10 Multiple customers / future central hosting

- One binary; each installation gets its own config/grants/credentials/DB endpoint. Central hosting = the same binary with central config + reachable transport + server PostgreSQL.
- **Guard already present:** `HostCompositionSmokeTests` and `ArchitecturePolicy` keep Host composition-only; the Worker must live in `src/Host/*` (delivery) so it stays outside Domain/Application.

---

## 3. Future central / API compatibility analysis

| Aspect | Local Worker (now) | Future central Worker | Future authenticated API |
|---|---|---|---|
| Business logic | shared Application command | shared | shared |
| Trust source | verified provisioning grant (§2.4) | verified provisioning grant (central operator) | authenticated principal (token/session) |
| Transport | local provider access | remote API or agent/bridge | HTTP request |
| Tenant authority | verified grant → trusted `TenantContext` | same seam | same seam, principal-derived |
| Persistence | local PostgreSQL | server PostgreSQL (migration/cutover needed) | same |
| Host | unchanged composition | unchanged | unchanged |

**Compatibility requirement (from the deployment decision):** the same Worker/Application binaries must run against two isolated authorized tenant/source configurations and different DB endpoints **without code changes** — a configuration/integration test, not a new abstraction.

**No speculative platform is introduced.** The only new seam is the trust producer (ADR-011 shape) + the adapter registration + the lease; all are small, delivery/infrastructure-scoped.

---

## 4. Decision matrix

| # | Decision | Disposition | Proposed component / owning layer |
|---|---|---|---|
| D1 | Windows Worker hosting (local) | **NEW MINIMAL COMPONENT** | `src/Host/FaraTaraz.Worker` (delivery), generic host, cross-platform code + Windows service registration at edge |
| D2 | Future central hosting (same binaries) | **REUSE** | same Worker; only configuration/DB/transport differ |
| D3 | Worker vs future API authority producer | **NEW MINIMAL COMPONENT (shared seam)** | `IAuthenticatedPrincipal` port in `BuildingBlocks.Tenancy` + delivery-owned verifier (Worker now, API later) |
| D4 | Local vs remote accounting transport | **EXISTING SEAM / REUSE** | `IAccountingProvider`/`ISyncablePort<T>` unchanged; adapter selects transport from validated config |
| D5 | Local vs server PostgreSQL | **EXISTING / REUSE + BLOCKED migration** | connection string is already a parameter; data migration/cutover/backup = **BLOCKED** on ops decisions |
| D6 | Job scheduling vs execution lease | **NEW MINIMAL COMPONENT** | scheduling in the Worker loop; lease via PostgreSQL advisory lock/lease row (delivery-level) |
| D7 | Tenant/source enrollment & trust verification | **BLOCKED** (design only) | provisioning grant + verifier; **no issuer exists** (blocker B1) |
| D8 | Missing real provider integration | **BLOCKED** | Asan adapter; transport/auth/schema **UNKNOWN** (blocker B2) |
| D9 | Disabled-source fail-closed | **NEW MINIMAL COMPONENT** (small) | consult `AccountingSourceStatus` before provider access (`AccountingSources`) |
| D10 | Provider credential storage/rotation | **NEW MINIMAL COMPONENT** | per-source secret store referenced by `AccountingSource` (Infrastructure delivery) |
| D11 | Secret redaction in logs | **NEW MINIMAL COMPONENT** (small) | delivery-layer logging helper |
| D12 | Central Host unchanged | **REUSE / EXISTING** | `FaraTarazHost` composition stays composition-only |

Items marked **EXISTING / REUSE** require no new abstraction. Items **NEW MINIMAL COMPONENT** are small, single-responsibility and delivery/infrastructure-scoped. Items **BLOCKED** must not be fabricated.

---

## 5. Security & architecture acceptance-test plan (design only — none has run)

| # | Test | Runnable now? |
|---|---|---|
| T1 | Untrusted tenant (no trusted context) ⇒ zero provider calls, zero writes | **Partly now** — already proven for the Query path (`TenantSourceOwnershipTests`, PostgreSQL); needs a Worker-level equivalent once the producer exists |
| T2 | Wrong tenant/source ownership ⇒ zero provider calls, zero writes | **Now** (existing PostgreSQL negative suite) |
| T3 | Missing/invalid provisioned grant ⇒ fail closed (no context, no provider, no write) | **BLOCKED** on B1 |
| T4 | Missing provider configuration ⇒ fail closed | **Now** (`EfAccountingSourceProviderResolver` returns null; resolver tests exist) |
| T5 | Concurrent duplicate job execution ⇒ single effective step (lease) | **BLOCKED** on the lease component |
| T6 | Cancellation during provider access and before commit ⇒ rollback, no partial write | **Now** (existing rollback/OCE tests) + Worker-level wiring test |
| T7 | Worker restart after partial sync ⇒ resumes from persisted Active checkpoint, no replay of Completed | **Now** (existing completion/restart PostgreSQL tests) + Worker restart test |
| T8 | Same Application command invoked from Worker and future API | **BLOCKED** (API not built); the shared-seam assertion can be added now |
| T9 | Central deployment: two isolated authorized tenant/source configs + different DB endpoints, no code change | **BLOCKED** on B1/B2 + server DB availability |

Each blocked test lists the exact external dependency it needs; none may be claimed as passing.

---

## 6. Explicit blockers and questions requiring external facts

| ID | Blocker | Needed from user/vendor |
|---|---|---|
| **B1** | No real provisioning/trust authority (issuer, verification material, storage) | Who issues the enrollment grant and how is it verified (signed enrollment token from FaraTaraz? DB-bound secret provisioned out-of-band? machine/service-bound credential)? — required before §2.4 can be implemented honestly |
| **B2** | No Asan provider fact base | Local integration mechanism (file/DB/COM/local API?), auth, data model, capability coverage, and — for central — whether any remote route exists and is permitted |
| **B3** | Local PostgreSQL lifecycle | Installer choice, backups, upgrade/rollback, encryption-at-rest expectations |
| **B4** | Windows Service install/upgrade/rollback | Packaging/updater/versioning approach and service-account policy |
| **B5** | Scheduling policy | Interval vs manual queue; per-source concurrency policy; lease TTL |
| **B6** | Thread/DB sizing and observability | Log sink, retention, correlation across local/central |

**ADR-011 remains Proposed; it must not be marked Accepted until B1 is decided and the seam is implemented and independently tested.**

---

## 7. Rejected alternatives (rule violations)

| Alternative | Violated rule | Verdict |
|---|---|---|
| Worker reads `tenantId`/`sourceId` from appsettings/job payload and mints authority | §3.7 (no caller-controlled tenant trust), ADR-011 | **REJECTED** — use the verified provisioning grant |
| Public `TenantContext.FromAuthenticatedPrincipal` factory | §3.7, ADR-011 | **REJECTED** — keep minting internal; add a narrow seam |
| New `TrustedExecution` assembly now | §3.9 (no new assembly unless a seam is insufficient) | **REJECTED** — ADR-011 places the seam in `BuildingBlocks`; no evidence a new assembly is needed |
| Worker writes to the database directly / owns a DbContext | §3.6 | **REJECTED** — all writes go through the Application command and its ports |
| Move business rules into the Worker to simplify | §3.1/§3.3 | **REJECTED** — Domain/Application keep authority |
| Build remote API + local bridge + API server now | §3.9 | **REJECTED** — build local transport only; keep the seam |
| Ship the Mock provider as production | §3.3, constitution | **REJECTED** |
| Lease implemented as a second ingestion writer | §3.6 | **REJECTED** — lease is a delivery-level lock, not a write path |

---

## 8. Recommended next implementation slice (single, bounded)

**Slice W4-A — "Trustless-safe Worker skeleton with a verifiable-grant seam and an execution lease" (design-approved only; implementation pending B1/B2).**

- **In scope:** the `src/Host/FaraTaraz.Worker` project (generic host, configuration loading, graceful shutdown, `stoppingToken` propagation), Worker loop over provisioned grants, the `IAuthenticatedPrincipal` port + delivery-owned verifier **stub that fails closed** (no real issuer yet), the disabled-source fail-closed check, and the execution lease.
- **Out of scope:** any real authentication mechanism, any Asan transport, any central deployment.
- **Effort (estimate):** 4–7 engineering hours (consistent with the W4 estimate), split into ≤45-minute bounded subtasks.
- **Measurable acceptance criteria (all in a real-PostgreSQL environment):**
  1. With **no** valid grant, the Worker performs **zero** provider calls and **zero** DB writes and exits fail-closed (T3 partially unblocked by a fail-closed stub).
  2. With a **test-provisioned** grant (test-only, clearly marked), the Worker drives **exactly one bounded step** through `ISender` and commits atomically.
  3. Two concurrent Worker instances for the same `(Tenant, Source, Capability)` result in **one** effective step (lease) and **no** duplicate provider work; the loser exits without writing.
  4. Killing the Worker mid-step leaves **no** advanced checkpoint; restart resumes from the persisted `Active` state and does not replay a `Completed` sync without an explicit restart.
  5. A `Disabled` `AccountingSource` fails closed with zero provider calls/writes.
  6. `FaraTarazHost` and all W1–W3 guards remain unchanged and green; Architecture/Integration suites stay green in Debug+Release.
  7. Cross-platform build (the Worker binary builds on Linux CI even though service installation is Windows-only).

**Gate:** this slice may start **only** after the architect approves §2.4's trust shape and B1/B2 facts (or explicitly accepts a fail-closed stub with no production activation). Until then W4 stays BLOCKED and this document is the deliverable.

---

## 9. Continuity

W1 audit ACCEPTED; W2 Domain policy ACCEPTED (`60d40ea`); W3 Application/guard cleanup ACCEPTED (`115e799`). This is a **read-only design** for W4's first delivery shape; it authorizes no code. The next chat should read the three W4 decision documents, this design, `RECOVERY.md`, `OPEN-DEFECTS.md` and `ARCHITECT-JOURNAL.md`, fetch live `origin/master`, and reconcile the executor's status before any implementation instruction.

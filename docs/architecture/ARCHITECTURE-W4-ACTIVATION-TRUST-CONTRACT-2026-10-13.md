# FaraTaraz — W4 Activation & Trusted-Execution Contract (V1 design)

**Task:** `FT-REBASELINE-W4-ACTIVATION-TRUST-CONTRACT-002` — BOUNDED ARCHITECTURE DESIGN.
**Status:** DESIGN ONLY for architect review. **No production code, tests, packages, migrations, projects or ADR status changed.**
**Baseline:** `d482c2b` (origin/master, fetched; worktree clean).
**Binding inputs:** `ARCHITECTURE-W4-ACTIVATION-AUTHORITY-DECISION-2026-10-13.md` (company-operated issuer, one-time code, signed installation grant, ≤30-day offline), `ARCHITECTURE-W4-DUAL-DELIVERY-DECISION`, `-DEPLOYMENT-`, `-CONNECTIVITY-`, `ARCHITECTURE-W4-WORKER-TRUST-DESIGN-2026-10-13.md`, ADR-009/010/011, `structure.md` §10, constitution.

> Every claim is read from source at the baseline SHA (file:line). Documentation is not trusted over code; discrepancies are listed in §1.4.

---

## 1. Source-grounded current architecture

### 1.1 Verified runtime facts

| Fact | Evidence |
|---|---|
| `TenantContext` has an **internal** constructor and `internal FromAuthenticatedPrincipal`; only `IsTrusted`/`AssertTrusted()` are public | `TenantContext.cs` L53, L67, L60, L74-81 |
| `DatabaseTenantScope.None` is the fail-closed default; `FromTrusted(context)` calls `context.AssertTrusted()`; ctor is private | `DatabaseTenantScope.cs` L19, L35-40, L47 |
| Both module compositions bind the scope by **reading** a context; they never register one | `IngestionInfrastructureComposition.cs` L50-56; `AccountingSourcesInfrastructureComposition.cs` L53-59 |
| `FaraTaraz.BuildingBlocks` exposes internals only to **three test assemblies** | `FaraTaraz.BuildingBlocks.csproj` L22-26 |
| Ownership is fail-closed and **ignores the caller tenant** (trusted scope only) | `EfAccountingSourceOwnership.cs` L42-77 (scope guard L48-52) |
| Provider identity comes from the **persisted** `AccountingSource.Provider` (trusted scope only, OCE rethrown, other failures fail closed) | `EfAccountingSourceProviderResolver.cs` L35-66 |
| Admission order in the command = trusted tenant → scope agreement → ownership → provider resolution → policy C5/C6/C8 → provider page → single transaction | `RunCustomerSyncHandler.cs` L64, L68-74, L77-86, L91-100, L104-129, L131, L151-216 |
| Atomic write = records + checkpoint CAS + run, one explicit transaction | `IngestionUnitOfWork.cs` L38-53; `EfIngestionWriteStore.cs` L71-98; `SyncCheckpointRepository.cs` L86-153 |
| No hosting runtime exists anywhere in `src/` | repo grep: no `Microsoft.Extensions.Hosting` / `BackgroundService` / `IHostedService` |
| No real provider connector | only `MockAccountingProvider.cs` L21 / `MockCapabilitySync.cs`; vendor names only in doc comments, `ProviderLeakageTests`, one test literal |

### 1.2 The exact missing trust wiring (two separate gaps)

1. **No producer:** nothing in production calls the internal factory; therefore `DatabaseTenantScope` is always `None`.
2. **No DI registration of `TenantContext` at all:** the compositions call `GetService<TenantContext>()`, but no project registers `TenantContext` (`IngestionInfrastructureComposition` L52, `AccountingSourcesInfrastructureComposition` L55). Even if a trusted context existed, the container would not supply it. A production seam must therefore (a) mint and (b) bind the context **into the job scope**.
3. **No capability-port registration:** `ISyncablePort<SourceCustomer>` is registered only in `tests/…/CqrsTestSupport.cs` L46.

### 1.3 How the integration tests bind trust (the pattern to generalize)

`RunCustomerSyncHandlerTests.cs` L33-37, L62-79 construct `DatabaseTenantScope.FromTrusted(TenantContext.FromAuthenticatedPrincipal(...))` and inject it directly, bypassing DI. This is test-only (friend assembly). Production must bind the same scope through a scope-local holder (§3).

### 1.4 Documentation vs code discrepancies (file:line evidence)

| # | Discrepancy | Evidence |
|---|---|---|
| X1 | **`AccountingSourceStatus` is persisted but never enforced.** `Disabled` sources are still "owned" and resolvable | `AccountingSource.cs` L56-62 (`Disabled = 0`); `AccountingSourceEntity.cs` L33; `EfAccountingSourceOwnership.cs` L66-70 (no status filter); `EfAccountingSourceProviderResolver.cs` L48-54 (no status filter) |
| X2 | **Existing PostgreSQL tests seed `Status = 0` (= `Disabled`)** and expect success | `AccountingSourceProviderResolverTests.cs` L38; `AccountingSourcesPersistenceTests.cs` L44, L167 → enforcing the active check has a **real test-compatibility implication** (§5.4) |
| X3 | `AccountingSourceEntity` has **no credential column** — provider credentials have no storage today | `AccountingSourceEntity.cs` L15-34 |
| X4 | `structure.md` §2/§3 are stale after W3 removals (`IApplicationUseCase`, `BuildingBlocks/Diagnostics|Errors`) | architect-owned (AD-6); recorded in `RECOVERY.md` |
| X5 | No scheduling/lease of any kind exists | repo grep: no `Lease`/`Scheduler`/`Timer` in `src/` |

---

## 2. Minimal activation / grant protocol (V1)

```
Company provisioning (NEW, company-operated)
      │  creates Tenant + AccountingSource (Status=Enabled); issues one-time code
      ▼
One-time code  ──TLS──▶  Activation service (NEW, company-hosted)
      │  authenticates code, atomically consumes it, binds provisioned scope
      │  + installation public key, signs the grant with the company signing key
      ▼
Installation-bound signed grant  (+ pinned company verification key in the Worker)
      │
      ▼
Worker verification (NEW, delivery)  →  trusted TenantContext (restricted seam, §3)
      ▼
ISender → RunCustomerSyncCommand → Application admission → provider → atomic PostgreSQL write
```

### 2.1 Roles

| Concern | Owner | V1 disposition |
|---|---|---|
| Creates `Tenant` / `AccountingSource` rows | Company provisioning process | **NEW** (company-operated admin workflow; no UI required for V1) |
| Issues one-time activation codes | Company activation service | **NEW** |
| Signs the installation grant | Company activation service (private key held by company) | **NEW** |
| Verifies the grant, mints trust | Worker (delivery adapter) | **NEW** |
| Ownership / scope enforcement | Existing Application admission (`IsOwnedByAsync`) | **EXISTING** (must remain) |

### 2.2 One-time code — lifecycle and anti-abuse

- Generated server-side: `codeId` + 128-bit random secret; stored only as a **hash** (SHA-256) with `tenantId`, `scope`, `issuedAtUtc`, `notAfterUtc` (short: e.g. 24 h), `consumedAtUtc`.
- **Atomic single use:** consumed by a single conditional statement — `UPDATE ActivationCodes SET ConsumedAtUtc = now WHERE CodeHash = @h AND ConsumedAtUtc IS NULL` — and the activation proceeds only if exactly one row was updated. This is the anti-replay guarantee (same pattern as the existing DB-enforced idempotency: `SourceRecordRepository.cs` L123-145).
- **Rate limiting / brute force:** per-code attempt counter + per-IP/installation throttle; codes are 128-bit so guessing is infeasible; constant-time hash compare.
- Code is **not** the grant: it authorizes exactly one enrollment of one installation.

### 2.3 Installation key pair

- Generated **on first run of the installer/Worker** with `ECDsa` P-256 (preferred) or `RSA-3072`.
- **Private key protection:** prefer Windows CNG/TPM-backed non-exportable key (`CngKey`/`RSACng`/`ECDsaCng`); otherwise DPAPI-protected at machine scope. Never written to `appsettings.json`, never logged, never uploaded.
- Only the **public** key (SPKI) is sent to the activation service; the grant binds its SHA-256 thumbprint.

### 2.4 Signed grant — mandatory fields

| Field | Purpose |
|---|---|
| `iss` | Company issuer identity |
| `aud` | Product/Worker audience (prevents cross-product replay) |
| `grantId` | Unique grant id (renewal/revocation/audit) |
| `installationId` | Local installation identity |
| `installationPublicKeyThumbprint` | SHA-256 of the installation SPKI — binds the grant to the key the client must still possess |
| `tenantId` | Authorized tenant |
| `authorizedSources[]` — each `{ sourceId, capabilities[] }` | Least-scope authorization |
| `issuedAtUtc`, `notAfterUtc` | Validity window |
| `maxOfflineDays` | Offline policy (≤30) |
| `signingKeyId` | Which company signing key to verify with (rotation) |

**Encoding:** canonical UTF-8 JSON payload with a JWS Compact (or detached) signature, `alg` = `ES256` (P-256) or `RS256`. **Standard .NET only** (`System.Security.Cryptography`, `ECDsa`/`RSA` + `SHA256`, `Convert.ToBase64String`); no new NuGet package.

**Verification rules (all must pass, else fail closed):**
1. `signingKeyId` resolves to a **pinned** company public key (from a pinned trust store, not from the grant/network).
2. `alg` is exactly the expected algorithm; signature verifies over the exact canonical bytes.
3. `aud` matches the product; `notAfterUtc` not passed.
4. `installationPublicKeyThumbprint` equals the local key's SPKI SHA-256 (proves key possession without revealing the private key).
5. The job's requested `(sourceId, capability)` ⊆ `authorizedSources`.

### 2.5 Renewal, rotation, replacement, revocation

- **Renewal:** before `notAfterUtc`, the client presents the current grant plus a **proof of installation-key possession** (sign a server nonce with the installation private key) and receives a new grant. No user-entered code is required after activation.
- **Signing-key rotation:** publish the new public key under a new `signingKeyId`; the trust store accepts a bounded set of ids; old grants remain verifiable until expiry.
- **Replacement installation:** new key pair + new one-time code → new grant for the new `installationId`; server revokes the old grant.
- **Revocation:** server-side list; **enforced only when online**. Offline clients cannot see it — an explicit, documented limitation (architect decision §6/§33).
- **Provider credentials are never part of the grant** (§2.6).

### 2.6 Provider credentials stay separate

The grant authorizes **scope**; provider credentials authorize the **connection**. Provider secrets live in a per-source secret store (Windows DPAPI/credential store or an approved vault), referenced by the `AccountingSource` row, rotated independently, never in repo/installer/logs/appsettings. Today there is **no credential column** (`AccountingSourceEntity.cs` L15-34) — a W4 implementation gap (blocker B2).

### 2.7 Offline window, expiry, clock rollback

- Persist `lastSeenOnlineUtc` (server-provided) and a **monotonic local high-water mark** `maxObservedUtc` (only advances).
- On each job start: if `systemNow < maxObservedUtc` → **clock rollback detected ⇒ fail closed** until online. Otherwise advance the mark.
- If `systemNow - lastSeenOnlineUtc > maxOfflineDays` → **fail closed for new sync jobs**; existing data is retained untouched; controlled renewal restores operation.
- Offline cannot guarantee immediate revocation — the 30-day window is a **maximum continuity allowance**, not an online-check guarantee.

---

## 3. Trusted-context establishment seam (smallest legitimate, ADR-011-consistent)

### 3.1 Chain and owning layer

```
Verified signed grant + installation-key possession + authorized job scope      [delivery: src/Host/FaraTaraz.Worker]
      │  builds InstallationPrincipal : IAuthenticatedPrincipal (company-verified only)
      ▼
TenantContext.Establish(principal)   ← new NARROW public API in BuildingBlocks.Tenancy
      │  internally calls the still-internal factory; returns null when !IsAuthenticated
      ▼
ScopedTrustedTenant (scoped, write-once per job scope)                          [BuildingBlocks.Tenancy]
      │
      ▼
DI scope: DatabaseTenantScope = FromTrusted(context)   (existing TryAdd factory, UNCHANGED)
      │
      ▼
ISender → RunCustomerSyncCommand(TenantContext, …) → Application admission (UNCHANGED) → provider → atomic write
```

### 3.2 Exact components

| Step | Component | Assembly / layer | Status |
|---|---|---|---|
| `IAuthenticatedPrincipal` (`IsAuthenticated`, `TenantId`) | port | `BuildingBlocks.Tenancy` (foundation) | **NEW** |
| `TenantContext.Establish(IAuthenticatedPrincipal) → TenantContext?` | narrow establishment API (private ctor still private) | `BuildingBlocks.Tenancy` | **NEW** |
| `ScopedTrustedTenant` — scoped, write-once holder | scope carrier | `BuildingBlocks.Tenancy` | **NEW** |
| `GrantVerifier` + `InstallationPrincipal` + `WorkerAuthority` | verify grant, set holder | `src/Host/FaraTaraz.Worker` (delivery) | **NEW** |
| `DatabaseTenantScope` binding | reads `GetService<TenantContext>()` | module Infrastructure compositions | **EXISTING, UNCHANGED** |
| `DatabaseTenantScope`/`TenantContext` scoped registration over the holder | delivery registration | `src/Host/FaraTaraz.Worker` | **NEW** |

**How the scope gets the context without ambient/global state:** the delivery opens one DI scope per job, sets the scoped holder **once** (before resolving the handler), and registers `TenantContext` scoped as `sp => sp.GetService<ScopedTrustedTenant>()?.Context!`. An unset holder ⇒ `GetService<TenantContext>()` is null ⇒ the existing `TryAddScoped<DatabaseTenantScope>` factory yields `None` ⇒ fail closed. The holder is **per-scope, write-once**, not static and not `AsyncLocal` (Constitution A.5 / ADR-011 rejected alternatives).

### 3.3 Mandatory rules — compliance

| Rule | Compliance |
|---|---|
| No public unrestricted `TenantContext` factory | ctor and `FromAuthenticatedPrincipal` stay `internal`; the only new public API requires an `IAuthenticatedPrincipal` and returns null unless authenticated |
| No `InternalsVisibleTo` for the Worker | not required — `Establish` lives **inside** `BuildingBlocks`, so no friend access is needed |
| No trust from `appsettings.json`, Windows account, job payload or a DB row alone | authority comes only from a **verified company-signed grant** + key possession; config carries no tenant authority |
| No `DbContext` in the Worker | Worker references only ports + module **Composition** seams |
| No Domain dependency on licensing/HTTP/Worker/crypto | Domain untouched; the seam is foundation + delivery |
| No Application dependency on the Worker | unchanged; Application consumes the command parameter |
| No change to Host composition ownership | `FaraTarazHost.ConfigureFaraTaraz` unchanged |
| No production mock principal/provider | the principal type exists only in the Worker verifier; Mock stays test-only |

### 3.4 Assembly-visibility conflict analysis (required by the task)

**No conflict exists.** ADR-011 places the establishment API *inside* `BuildingBlocks.Tenancy`, so `Establish` can call the internal factory without any `InternalsVisibleTo`. The "minimal safe alternative" is therefore **not needed**.

**Residual risk (stated honestly):** any in-process code could implement `IAuthenticatedPrincipal` with `IsAuthenticated => true`. This is the same class of residual as reflection (ADR-011 §"Ways a caller could mint") — it requires already-compromised in-process code. Mitigation: extend `TenantBoundaryGuards` so that, outside the designated authority file, **calling `Establish(`** and **implementing `IAuthenticatedPrincipal`** both fail the guard, alongside the existing ban on `new TenantContext`/`FromAuthenticatedPrincipal`.

---

## 4. Dependency direction and assembly ownership

```
BuildingBlocks.Tenancy   ← principal port + Establish + ScopedTrustedTenant (no crypto/HTTP/DI-heavy code)
        ▲
        │
src/Host/FaraTaraz.Worker  →  BuildingBlocks, Ingestion.Application, Ingestion.Composition, AccountingSources.Composition
        │                     (delivery adapter; holds GrantVerifier + provider adapter registration)
        ▼
Ingestion.Composition / AccountingSources.Composition  →  module Infrastructure (unchanged)
```

- Domain: unchanged, no licensing/crypto/transport.
- Application: unchanged handlers; still owns admission and the transaction boundary.
- Infrastructure: unchanged; implements inward ports.
- **Required guard update (design note):** `ArchitecturePolicy.Classify` currently returns `ProjectLayer.Unknown` for any `src/Host/*` project that is not exactly `FaraTaraz.Host`; adding the Worker will therefore **fail** `Every_production_project_is_classified_and_every_layer_is_present` until the Worker is classified (e.g. an explicit `Host`/delivery bucket) and added to the known-project sets. This must be part of the first Worker slice, not silently exempted.

---

## 5. Shared disabled-source admission correction

### 5.1 The defect (X1)

`AccountingSourceStatus.Disabled` is never consulted: `EfAccountingSourceOwnership.IsOwnedByAsync` returns `true` for any existing `(trusted tenant, source)` row, and `EfAccountingSourceProviderResolver.ResolveProviderAsync` returns the provider for any existing row. A disabled source is therefore fully usable today.

### 5.2 Proposed correction (focused, shared, not Worker-only)

- **New port** in `AccountingSources.Domain/Authorization/IAccountingSourceActivation.cs`: `Task<bool> IsActiveAsync(AccountingSourceId sourceId, CancellationToken)` — fail-closed (`false` on unknown scope/lookup failure).
  *Note:* `IAccountingSourceOwnership` deliberately keeps its meaning ("owned"); folding activation into it would make its name lie. A distinct port keeps each contract honest and makes the required ordering explicit.
- **New implementation** `EfAccountingSourceActivation` in `AccountingSources.Infrastructure/Authorization/` — resolves the trusted scope (same pattern as the ownership oracle), returns `Status != Disabled` for the trusted tenant's source, `false` otherwise.
- **Distinct failure**: `AccountingSourceDisabledException` in `AccountingSources.Domain/Authorization/` (a disabled source is owned but not usable — `UnauthorizedSourceException` would be semantically wrong).
- **Called by both handlers** (`RunCustomerSyncHandler`, `SynchronizeCustomersHandler`) between ownership and provider resolution.
- Registration: one line in `AccountingSourcesInfrastructureComposition` (mirrors the ownership registration at L61).

### 5.3 Required admission ordering (both delivery paths)

`trusted tenant (AssertTrusted)` → `scope agreement` → `source ownership` → **`active-source status`** → `provider resolution` → provider call → atomic write.

### 5.4 Exact behavior and compatibility

| Case | Behavior |
|---|---|
| Disabled (owned) | `AccountingSourceDisabledException`; **zero provider calls, zero writes** |
| Missing / foreign source | unchanged: ownership `false` ⇒ `UnauthorizedSourceException`; zero calls/writes |
| Missing provider config | unchanged: resolver returns null ⇒ fail-closed; zero calls/writes |
| `Syncing` / `Error` status | **not** blocking (a transient runtime state must not block the next bounded step); only `Disabled` blocks |

**Compatibility implication (X2, must be explicit):** the enum value `Disabled = 0` is the persisted default, and three existing integration-test seeds use `Status = 0` and expect success (`AccountingSourceProviderResolverTests.cs` L38; `AccountingSourcesPersistenceTests.cs` L44, L167). Enforcing the check **requires updating those fixtures to `Enabled`** — a legitimate fixture correction (they were accidentally seeding a disabled source), **not** a guard weakening. Provisioning must set `Enabled` explicitly. No schema/migration change is needed (the column exists).

**Alternative (cheaper, if the architect prefers minimal surface):** add `Status != Disabled` to the resolver's `Where` (1-line change) — same fail-closed outcome but **loses the distinction** between "disabled" and "no provider configuration", and makes the ordering less explicit. Recommended: the dedicated port.

### 5.5 Negative tests (to be added in the implementation slice)

1. Disabled source ⇒ `AccountingSourceDisabledException`, **zero provider calls, zero ingestion writes** (PostgreSQL, counting capability port).
2. Missing source ⇒ unchanged ownership denial, zero calls/writes.
3. Foreign-tenant source ⇒ unchanged ownership denial, zero calls/writes.
4. Enabled source ⇒ unchanged success path (existing 85 tests stay green after the fixture correction).

---

## 6. Execution lease — choice and lifecycle proof

### 6.1 Choice: PostgreSQL **session-scoped advisory lock** (`pg_try_advisory_lock`)

**DECISION (established from the actual transaction/connection model), with one sub-item pending.**

- The provider call happens **before** the ingestion transaction (`RunCustomerSyncHandler.cs` L131, then L151), so a transaction-scoped lock (`pg_try_advisory_xact_lock`) **cannot** cover provider access. → session-scoped lock on a **dedicated connection** held for the job.
- **Port:** `IIngestionExecutionLease` in `Ingestion.Application/Ports/` (Application-owned inward port, like `IIngestionWriteStore`), consumed by the **Worker job boundary**, implemented in `Ingestion.Infrastructure` and bound by `Ingestion.Composition`.
- **Key:** 64-bit derived deterministically from `SHA-256(tenantId \x1f sourceId \x1f capability)` truncated to 8 bytes — stable across processes/machines.
- **Acquire:** `SELECT pg_try_advisory_lock(@key)` — non-blocking; `false` ⇒ another worker owns the scope ⇒ the job **skips without writing**.
- **Release:** `pg_advisory_unlock(@key)` in `finally` + connection dispose.
- **Crash safety:** a session advisory lock is released by PostgreSQL when the connection terminates ⇒ a killed worker cannot cause permanent lockout; **no TTL needed**.
- **Deadlock:** impossible — a single non-blocking acquisition per job.

### 6.2 Why not a lease row

- Requires a schema migration (prohibited here) and TTL/cleanup logic.
- Writing a lease row from Application/delivery would be a **persistence write outside the approved Infrastructure boundary** and would be flagged by `OwnershipPathAuthorizationGuard` rule 1.
- An advisory lock performs **no write at all**, so it cannot become a second ingestion write path.

### 6.3 Connection ownership / transaction interaction

- The lease component owns **one raw `NpgsqlConnection`** for the job duration (created from the module connection string supplied by composition). It is **not** a `DbContext` and **not** the ingestion transaction's connection; the ingestion transaction continues to run on the scoped `IngestionDbContext` via `IngestionUnitOfWork` (unchanged).
- Because the lock is held on a different session, it never interferes with the ingestion transaction's commit/rollback, and the checkpoint CAS remains the sole progress guard.

### 6.4 Granularity — **DECISION PENDING (one missing fact)**

Should the lease be held for the whole **job** (all pages) or per **step** (one page)? Not derivable from code. **Recommendation:** per job (covers provider access + every step), because it avoids interleaving two workers between pages. **Missing fact:** acceptable connection-pool sizing when several sources run concurrently on one customer machine, and the desired behaviour when a job exceeds a reasonable duration. Until decided, mark **DECISION PENDING**; implement per-job with a bounded job timeout.

### 6.5 Negative concurrency tests (design)

1. Two processes, same `(tenant, source, capability)`, simultaneous start ⇒ **one** acquires, the other skips with **zero provider calls and zero writes**.
2. Kill the holder mid-provider-call ⇒ the other worker acquires after the connection dies (no permanent lockout).
3. Different capability/source ⇒ both proceed (lock is scoped, not global).
4. Lease + stale checkpoint ⇒ CAS still rejects the stale writer (complement, not replacement).

---

## 7. Local-first / central-ready compatibility

| Aspect | Local V1 | Future central |
|---|---|---|
| Identity | installation key pair + installation-bound grant | new installation identity + new grant (central runtime); **never transfer the customer private key** |
| Trust producer | `WorkerAuthority` (delivery) | central operator authority → same `Establish` seam |
| PostgreSQL | customer-local | central; explicit audited migration/cutover, stable IDs, checkpoint/CAS semantics preserved |
| Provider transport | local Asan access (facts unknown) | remote API **or** customer-local bridge/agent if the accounting software is not reachable; selected by validated connection configuration |
| Business logic | shared | shared — **no Domain/Application transport branching** |

The transport is chosen by the **adapter/infrastructure** layer from validated configuration; Application keeps depending only on `IAccountingProvider`/`ISyncablePort<T>` (`ARCHITECTURE-W4-CONNECTIVITY-DECISION` §approved seam). **WordPress integration is USER-REPORTED and does not establish an Asan HTTP API, its auth protocol, supported operations or permission.**

---

## 8. Threat model and negative test matrix

| # | Threat | Control | Test runnable now? |
|---|---|---|---|
| T1 | Forged/absent grant | pinned-key signature verification; fail closed | **BLOCKED** on the activation service (design only) |
| T2 | Grant replay on another installation | `installationPublicKeyThumbprint` + local key possession | **BLOCKED** (service) |
| T3 | Stolen/duplicated one-time code | hashed storage + atomic single-use UPDATE + rate limit | **BLOCKED** (service) |
| T4 | Clock rollback to extend offline use | monotonic high-water mark ⇒ fail closed | **NEW, testable now** (pure logic) |
| T5 | Offline expiry ignored | `lastSeenOnline + maxOfflineDays` ⇒ block new jobs | **NEW, testable now** (pure logic) |
| T6 | Untrusted tenant | `AssertTrusted` + scope agreement; zero calls/writes | **Partly now** (`TenantSourceOwnershipTests`, PG); Worker-level after the seam |
| T7 | Wrong/foreign source | ownership fail-closed; zero calls/writes | **Now** (PG suite) |
| T8 | **Disabled source used** (§5) | active-source check; zero calls/writes | **Now** once implemented |
| T9 | Missing provider config | resolver returns null; fail closed | **Now** (resolver tests) |
| T10 | Concurrent duplicate jobs | session advisory lock | **BLOCKED** on the lease slice |
| T11 | In-process principal forgery | restricted `Establish` + `TenantBoundaryGuards` extension | **Partly now** (guard tests) |
| T12 | Provider secret leakage | separate per-source secret store; log redaction | **BLOCKED** on credential storage |
| T13 | Central cutover data loss | audited migration; no private-key transfer | **BLOCKED** (ops) |

---

## 9. Decisions, unresolved facts, blockers

### 9.1 Decisions made in this document
- **D1** Grant = company-signed, installation-key-bound, scoped JWS (ES256/RS256, standard .NET only), with the mandatory field set of §2.4.
- **D2** One-time codes: hashed, atomic single-use, rate-limited; not the grant.
- **D3** Establishment seam = `IAuthenticatedPrincipal` + `TenantContext.Establish` + scoped write-once holder in `BuildingBlocks.Tenancy`; **no `InternalsVisibleTo` for the Worker**; no public unrestricted factory.
- **D4** Disabled-source admission = dedicated `IAccountingSourceActivation` port + distinct exception, applied in both Application handlers.
- **D5** Execution lease = **session advisory lock** on a dedicated connection, job-scoped (granularity **DECISION PENDING**, §6.4).
- **D6** Provider credentials stay outside the grant, per source.

### 9.2 Unresolved facts (external)
1. Activation-service hosting/operations, key storage (HSM?), and the company's code issuance workflow.
2. Real Asan local interface (file/DB/COM/local API), auth, data model, capability coverage; whether any remote route exists and is permitted.
3. Local PostgreSQL packaging/backup/upgrade policy; Windows service install/upgrade/rollback packaging.
4. Lease granularity + acceptable connection-pool sizing per customer machine.
5. Trusted clock source expectations for the offline window.

### 9.3 Blockers
- **B1** activation service + company signing key custody (design fixed; implementation not started).
- **B2** provider credential storage + Asan adapter facts.
- **ADR-011 stays Proposed** until the seam is implemented, guarded and independently tested. A fail-closed stub is **not** a production authority.

---

## 10. Recommended first implementation task (bounded, 20–30 minutes)

**Task `FT-W4-B1-DISABLED-SOURCE-ADMISSION`** — enforce active-source admission in the shared Application path (closes architect gate **C**; needs no external facts).

**Exactly allowed files:**
- **NEW** `src/Modules/AccountingSources/AccountingSources.Domain/Authorization/IAccountingSourceActivation.cs`
- **NEW** `src/Modules/AccountingSources/AccountingSources.Domain/Authorization/AccountingSourceDisabledException.cs`
- **NEW** `src/Modules/AccountingSources/AccountingSources.Infrastructure/Authorization/EfAccountingSourceActivation.cs`
- **EDIT** `src/Modules/AccountingSources/AccountingSources.Infrastructure/Composition/AccountingSourcesInfrastructureComposition.cs` (+1 registration line)
- **EDIT** `src/Modules/Ingestion/Ingestion.Application/SynchronizeCustomers/Commands/RunCustomerSyncHandler.cs` (one admission call, after ownership, before provider resolution)
- **EDIT** `src/Modules/Ingestion/Ingestion.Application/SynchronizeCustomers/Queries/SynchronizeCustomersHandler.cs` (same call)
- **EDIT** `tests/FaraTaraz.Infrastructure.IntegrationTests/AccountingSourceProviderResolverTests.cs` and `AccountingSourcesPersistenceTests.cs` (**fixture correction only:** `Status = 0` → `Enabled`)
- **NEW/EDIT** one PostgreSQL test file proving the disabled-source denial (zero provider calls, zero writes)
- **EDIT** `docs/planning/RECOVERY.md`

**Forbidden in that task:** schema/migration, Host/Worker project, packages, ADR status, MediatR handler shape, guards weakening, `OPEN-DEFECTS.md`/`ARCHITECT-JOURNAL.md`.

**Measurable acceptance criteria:**
1. Disabled owned source ⇒ `AccountingSourceDisabledException`; a counting capability port records **zero** calls and the ingestion tables record **zero** writes.
2. Missing/foreign source ⇒ unchanged `UnauthorizedSourceException`; zero calls/writes.
3. Enabled source ⇒ the full existing PostgreSQL suite is green **after the fixture correction** (85 tests, none weakened).
4. `ArchitecturePolicy`/`PhysicalStructureTests`/`TenantBoundaryGuards` etc. remain green; Debug + Release fully green; Linux CI SUCCESS at the exact SHA.
5. `git diff --check` clean; only the allowed files changed.

**Estimated effort:** 25–35 minutes (one bounded slice; if it overruns, stop after the domain port + implementation + one negative test and report PARTIAL).

---

## 11. Continuity

W1 audit, W2 Domain policy (`60d40ea`) and W3 Application/guard cleanup (`115e799`) are accepted. This document is the W4 activation/trust contract for architect review; it authorizes no code. The next chat should read the five W4 documents, this contract, `RECOVERY.md`, `OPEN-DEFECTS.md` and `ARCHITECT-JOURNAL.md`, fetch live `origin/master`, and reconcile executor status before any implementation instruction.

**STOP_FOR_ARCHITECT.**

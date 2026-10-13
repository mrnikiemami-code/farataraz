# FaraTaraz — W4 Verified Installation-Grant Authority & Restricted TenantContext Creation

**PROPOSED — AWAITING INDEPENDENT ARCHITECT ACCEPTANCE.**
**Revision 3** (`FT-W4-B3-R3-AUTHORITY-BOUNDARY-CORRECTION-001`) — resolves five architect findings against revision 2. Revision 2's placement of grant claims, license-specific failure reasons, expiry/scope policy and capability composition in `BuildingBlocks` is **withdrawn**. Revision 1's placement of the verifier in `BuildingBlocks` remains withdrawn.

**Task:** `FT-W4-B3-R3-AUTHORITY-BOUNDARY-CORRECTION-001` — DESIGN ONLY.
**Baseline:** local == remote == `d63367c` (origin/master; worktree clean). W1–W3, W4-B1, W4-B2 accepted; ADR-011 **Proposed**; production delivery incomplete.

---

## 1. Findings → resolutions

| # | Architect finding | Resolution (summary) |
|---|---|---|
| 1 | Keep grant claims, license failures, expiry/scope policy and capability composition out of `BuildingBlocks`; specify the minimum shared primitive | `BuildingBlocks` gains **no grant, license, error, expiry or composition concept**. Its only additions are one value type (`TrustedJobScope`), one scoped binding type, and the AMD-1 visibility grant. All claims, failure reasons, expiry/scope/offline policy and the full DI composition live in the trust-authority module. §3 |
| 2 | Separate signature-validated claims from fully authorized job evidence; signature alone must never permit minting | Two distinct, non-interchangeable stages, both in the authority: `ValidatedGrantClaims` (signature+parse only) → `AuthorizedJobGrant` (possession + validity + offline policy + requested-job scope). **Only `AuthorizedJobGrant` can reach minting.** §4, §5 |
| 3 | Exact job/scope binding, atomic consumption, defensive immutability, rejection of reuse across DI scopes; enforcement when a caller tries another source/capability | `AuthorizedJobGrant` is single-use (`Interlocked` consumption), the binding is write-once per DI scope, and Application verifies that the command's source/capability **equals** the bound job scope. §3.3, §6 |
| 4 | Exact C# declarations, visibility, collection ownership; no "sealed record"/`IReadOnlyList` immutability claim | §7: `sealed class`, private `readonly` fields, copy-on-construction into private arrays, explicit indexer/`Count`, no records, no `with`, no public setters. |
| 5 | Describe AMD-1 honestly as full internal access; enumerate exposed minting paths; separate runtime protection from static guards; no impossibility claim | §8: AMD-1 = **full internal access**; every affected minting path enumerated; static guards labelled supplementary; alternatives listed as rejected-on-ownership-grounds, **not** proven impossible. |

---

## 2. Final ownership / dependency map

| Owner | Owns | Never owns |
|---|---|---|
| **BuildingBlocks** | `TenantContext`, `DatabaseTenantScope` (**unchanged**); the minimal shared trust primitive (§3.1) | grant/license claims, failure taxonomy, expiry/scope/offline policy, crypto, key storage, issuer communication, capability composition |
| **Domain** | business rules | crypto, HTTP, DI, Worker, tenant authority |
| **Application** | use-case execution, admission **ordering** (trusted tenant → ownership → activation → **granted job scope** → provider), inward ports | verifier implementation, EF, transport, crypto |
| **`InstallationAuthority.Infrastructure`** (Infrastructure; the trust authority) | ES256/JWS verification, pinned key storage, OS installation-key possession signer, offline/clock policy, issuer client, `AuthorizedJobGrant` issuance, **its own DI composition** | business rules, use-case orchestration, persistence of ingestion state |
| **Worker** (delivery) | receive work, call the authority, dispatch through `ISender` | security policy, business logic, trust decisions |
| **Host** | composition only | business authority |

```
BuildingBlocks  ←  InstallationAuthority.Infrastructure  (AMD-1: friend for minting + internal binding)
BuildingBlocks  ←  Ingestion.Application / Ingestion.Infrastructure / AccountingSources.* / Domain / Host / Adapters   (unchanged)
InstallationAuthority.Infrastructure  ←  Worker (delivery)   [via its Composition seam]
```
No new dependency direction is introduced except the AMD-1 visibility grant (not a project reference).

---

## 3. Minimum genuinely shared trust primitive (finding 1)

`BuildingBlocks.Tenancy` gains exactly two types and no policy:

### 3.1.1 `TrustedJobScope` — public value type (no trust semantics)
```csharp
public readonly record struct TrustedJobScope(AccountingSourceId SourceId, string Capability);
```
It is a plain value: no issuer, no grant id, no expiry, no failure reason. It exists only so Application can compare "the job the trusted boundary authorized" with "the job the command asks for" without referencing the authority module.

### 3.1.2 `ScopedTrustedTenantBinding` — public sealed class, registered scoped, write-once
```csharp
public sealed class ScopedTrustedTenantBinding
{
    private TenantContext? _context;
    private TrustedJobScope? _scope;
    private int _bound;                       // 0 = unbound, 1 = bound (Interlocked CAS)

    /// Internal: only the trust authority (AMD-1 friend) may bind.
    internal bool TryBind(TenantContext context, TrustedJobScope scope);

    public TenantContext? Context { get; }    // read-only view
    public TrustedJobScope? Scope { get; }    // read-only view
}
```
`TryBind` is **internal** — so `BuildingBlocks` exposes **no public minting or binding API at all**; the authority assembly calls it through AMD-1. `Context`/`Scope` are readable so module Infrastructure can surface them to Application through existing/new inward ports.

### 3.1.3 What is *not* in `BuildingBlocks`
No `TrustedInstallationGrant`, no `TrustEstablishmentResult`, no `Expired`/`OutOfScope`/`PossessionFailed` reasons, no scope-list type, no key type, no verifier, no `AddTrustedTenantBoundary` extension. **The composition of the trust capability is owned by the authority module** (§8.3), not by the foundation.

---

## 4. Two non-interchangeable stages (finding 2)

```
grant bytes ──▶ [A] GrantVerifier            ──▶ ValidatedGrantClaims      (signature + strict parse ONLY)
                     (authority)                  • not sufficient to mint
                                                  • no possession, no validity window, no job scope
       │
       └──▶ [B] JobAuthorizer                ──▶ AuthorizedJobGrant        (possession + validity + offline + scope)
                     (authority)                  • single-use
                                                  • the ONLY input accepted by the minting step
                                                        │
                                                        ▼
                                             mint TenantContext  →  ScopedTrustedTenantBinding.TryBind
                                             (inside the authority, via AMD-1)
```

- `ValidatedGrantClaims` is a `sealed class` with **internal** constructor, produced only by `GrantVerifier`. It carries the raw claim values (tenant, installation, scopes, validity) but **has no path to minting**: no method on it, and `JobAuthorizer` takes it as one input among several.
- `AuthorizedJobGrant` is a `sealed class` with **internal** constructor, produced only by `JobAuthorizer.TryAuthorize(...)`. It contains the resolved `TenantId` and the single authorized `TrustedJobScope`, and is the *only* type the minting step accepts.
- Both types live in `InstallationAuthority.Infrastructure`; neither is public. **Signature validation alone can never create a `TenantContext`** because only stage B produces the object that the minting step requires.

---

## 5. One coherent authorization sequence

```text
Worker (delivery), one DI scope per job:
 1. resolve  InstallationAuthority  (authority module Composition) from the job scope
 2. resolve  grant evidence for this job  (provisioned store; never from the job payload)
 3. authority.TryAuthorizeAsync(evidence, requestedJobScope, ct):
      a. [A] verify ES256/P1363 signature over header.payload with the PINNED key selected by kid
             → ValidatedGrantClaims                       (fail elsewhere: no claims)
      b. verify iss == pinned issuer, aud == pinned audience, nbf ≤ now < exp
      c. verify requestedJobScope ∈ claims.scopes           (job/scope binding)
      d. verify offline policy: lastServerAuthenticatedInstant + maxOfflineDays; clock-rollback check
      e. prove possession: fresh 128-bit challenge → OS key signs (challenge‖tenantId‖installationId‖grantId‖source‖capability)
             → verify against claims.installationPublicKey   (thumbprint equality alone is NOT accepted)
      f. consume the grant atomically (Interlocked CAS, single use)
             → AuthorizedJobGrant
      g. mint TenantContext (internal factory, AMD-1) and ScopedTrustedTenantBinding.TryBind(context, scope)
 4. Worker: await sender.Send(new RunCustomerSyncCommand(authority.Current, request), ct)
 5. Application handler (unchanged ordering, one added equality check):
      AssertTrusted → trusted-scope agreement → ownership → activation
      → NEW: command.Request.SourceId == binding.Scope.SourceId AND capability == binding.Scope.Capability
      → provider resolution → provider call → atomic transaction (+ checkpoint CAS)
```
`authority.Current` is the minted `TenantContext`; the authority never returns claims, scopes or failure reasons to the delivery.

---

## 6. Job/scope binding, consumption, immutability, cross-scope reuse (finding 3)

1. **Exact binding.** `AuthorizedJobGrant` holds exactly one `(TenantId, TrustedJobScope)`. The scope must be an exact member of `claims.scopes`; membership is by ordinal equality of `AccountingSourceId.Value` and the capability namespace string.
2. **Atomic single use.** `AuthorizedJobGrant` carries `private int _consumed;` and `internal bool TryConsume()` implemented with `Interlocked.CompareExchange(ref _consumed, 1, 0) == 0`. `ScopedTrustedTenantBinding.TryBind` requires a successfully consumed grant; a second bind attempt fails.
3. **Write-once per DI scope.** `ScopedTrustedTenantBinding` uses the same CAS, so one scope holds at most one trusted tenant/job; a second bind in the same scope fails closed.
4. **No reuse across DI scopes.** A grant instance is consumed atomically, so it cannot be presented to another scope; a *new* job must obtain a *new* `AuthorizedJobGrant` (fresh challenge, fresh consumption). Nothing is cached statically; the binding is a scoped service, not a singleton and not `AsyncLocal`.
5. **Attempting another source/capability after obtaining a tenant context.** The minted `TenantContext` carries only the tenant, so the Application must compare the *granted* job scope with the *requested* job. Enforcement is added in Application (which owns admission ordering): the handler reads the bound scope through an inward port and requires `request.SourceId == bound.SourceId` **and** `capability == bound.Capability`, failing closed (a distinct `JobScopeMismatch` authorization failure) before ownership/provider work. Ownership and activation remain mandatory *additional* checks, so a same-tenant, owned, enabled, **but non-granted** source is still refused.
6. **Failure is inert.** Every rejection leaves the binding unset ⇒ `DatabaseTenantScope.None` ⇒ `AssertTrusted()` throws ⇒ zero provider calls, zero ingestion writes.

---

## 7. Exact C# declarations, visibility and collection ownership (finding 4)

Immutability is **not** claimed from `sealed record` or `IReadOnlyList`. Rules applied to every trusted type:

- `sealed class` (never `record`): no compiler-generated `with`, no `init`, no public property setters, no public constructor.
- Fields are `private readonly`; the constructor is `internal` and validates every argument before assigning.
- **Collections are copied.** The constructor takes `IEnumerable<TScope>` / `IReadOnlyList<TCap>` and stores a **private array** created by `ToArray()`; the caller's collection is never retained. Nothing exposes the array: expose `int Count` and `TScope this[int index]`, or `ReadOnlyMemory<TScope>` over the private array. `IReadOnlyList<T>` is **not** used as the storage type because it is only an interface view and can alias a mutable list.
- Scope membership is computed once into a private normalised array (`(string sourceId, string[] capabilities)`), with capabilities also defensively copied.
- No structural-equality/`Equals` overrides that could leak identity semantics; reference identity only.
- The single-use flag is `private int` + `Interlocked` (never `bool`, which cannot be CAS-ed atomically).
- `ValidatedGrantClaims` and `AuthorizedJobGrant` are `internal`; `TrustedJobScope` is `public readonly record struct` (a pure value, deliberately copyable) and `ScopedTrustedTenantBinding` is `public sealed class` whose mutation entry point (`TryBind`) is `internal`.

---

## 8. AMD-1 disclosed honestly (finding 5)

### 8.1 What AMD-1 actually is
AMD-1 = one production `InternalsVisibleTo` from `FaraTaraz.BuildingBlocks` to `FaraTaraz.Modules.InstallationAuthority.Infrastructure`. It grants that assembly **full access to all `BuildingBlocks` internals**, not a narrow capability. There is no C# mechanism to grant "only this one type".

### 8.2 Every minting/binding path AMD-1 exposes
1. `new TenantContext(tenantId, TenantContextOrigin.AuthenticatedPrincipal)` — internal constructor.
2. `TenantContext.FromAuthenticatedPrincipal(tenantId)` — internal factory.
3. `ScopedTrustedTenantBinding.TryBind(context, scope)` — internal method.
4. `DatabaseTenantScope.FromTrusted(context)` — already public but requires an already-minted context, so it is a consumer, not a producer.
5. **Reflection/debugging** on any of the above — available to *any* in-process code, friend or not; AMD-1 does not change this.

Any code inside the authority assembly can therefore mint authority without performing verification. The design does not claim otherwise.

### 8.3 Runtime protection vs static guards
- **Runtime protection (the real one):** the internal constructor/factory are enforced by the runtime CLR; only `BuildingBlocks` and its bound friends can call them. The authority assembly is the only production friend. Within it, minting is reachable from exactly one code path (`JobAuthorizer` → bind); other paths would have to be written deliberately.
- **Static guards (supplementary only):** `TenantTrustSourceTests` pins the friend allowlist to `{3 test assemblies} ∪ {InstallationAuthority.Infrastructure}`; a lexical guard can assert that `ScopedTrustedTenantBinding.TryBind`/`FromAuthenticatedPrincipal` are referenced only from the authority's `JobAuthorizer`. These detect drift; **they cannot prevent a compromised or malicious authority assembly from minting**. The architect already ruled that scans do not replace runtime checks; this document does not rely on them.
- **Trusted computing boundary (TCB):** the CLR/host, `BuildingBlocks` (as the type owner), and `InstallationAuthority.Infrastructure` (as the only production friend). A compromise of the authority assembly, of the process, or of a local administrator is **outside** what managed-code visibility can defend (§9).

### 8.4 Alternatives examined (rejected on ownership/security grounds — no impossibility claim)
| Alternative | Status |
|---|---|
| Verifier + keys in `BuildingBlocks` | Rejected by the binding ownership constraints (not impossible) |
| Public factory / caller assertion / caller-implemented verifier port | Rejected: forgeable by any caller (not impossible, just unsafe) |
| `IgnoresAccessChecksTo` / reflection to reach internals | Technically possible; rejected as undocumented, fragile and non-auditable — not claimed impossible |
| Moving `TenantContext` above `BuildingBlocks` | Rejected: Application/Domain reference it; would invert the dependency graph |
| Separate process for the authority | Would move the minting boundary out of process (larger change, new deployment unit); not evaluated to conclusion and therefore **not** claimed impossible |
| Source generators / analyzers to constrain call sites | Supplementary to runtime protection only |

**Honest conclusion:** AMD-1 buys correct ownership placement at the cost of one broadly-privileged assembly. The residual risk is contained by keeping that assembly small, single-purpose and independently reviewed, and by the fact that untrusted external evidence still cannot produce trust without a real signature **and** possession proof.

---

## 9. Threat-model limits (unchanged, honest)

Managed-code visibility, static guards and local storage cannot defeat a local administrator, in-process reflection/debugging, a compromised authority assembly, or a rebuild with a self-supplied pinned key; those parties already own the trust decision point. The guarantee is narrower: **untrusted evidence from outside the process cannot by itself produce a trusted context**, and a copied grant fails on a machine without the installation private key. Mutable local timestamps are not a trusted clock, and offline revocation cannot be immediate.

---

## 10. Frozen V1 verification contract (unchanged from revision 2)

`ES256` only (ECDSA P-256 + SHA-256); JWS Compact; signature **P1363 fixed-width 64-byte `r||s`** (not DER); signed bytes = `base64url(header) + "." + base64url(payload)`; header exactly `{"alg":"ES256","kid":…,"typ":"FT-GRANT+JWS"}` with `jwk/jku/x5u/x5c/crit` absent-or-rejected; payload fields `iss, aud, grantId, installationId, installationPublicKey, tenantId, scopes[], iat, nbf, exp, maxOfflineDays, signingKeyId` (no duplicates; unknown non-critical ignored); pinned compiled key store with `kid` rotation and **no** grant-supplied key/URL; strict bounds (≤8 KiB, ≤64 scopes, ≤8 capabilities/scope, integer timestamps, `exp > nbf ≥ iat`, `exp − iat ≤ 30 d`, `maxOfflineDays ≤ 30`, strict base64url, no trailing bytes); freshness `< exp`; offline ≤ `min(maxOfflineDays,30)` from the last server-authenticated instant; fresh 128-bit verifier-generated possession challenge; `OperationCanceledException` propagates; failure is inert. Upstream enrollment (company-operated issuer; hashed 128-bit single-use code with atomic conditional consumption, ≤24 h expiry, rate-limited, audited) remains a precise upstream contract, not built here.

---

## 11. Acceptance matrix (unchanged set; trimmed)

| # | Scenario | Outcome | Layer |
|---|---|---|---|
| 1 | Valid signature + granted scope + valid possession | authorized; one bounded step | authority unit + PG |
| 2 | Tampered payload / invalid signature | no claims; no context | authority unit |
| 3 | Unknown `kid` / wrong `iss`/`aud`/`alg` | rejected | authority unit |
| 4 | Expired / not-yet-valid | rejected | authority unit |
| 5 | Foreign tenant / non-granted source or capability | rejected | authority unit |
| 6 | Copied grant without installation private key | `PossessionFailed` | authority unit |
| 7 | Replayed possession / grant reuse across scopes | rejected (Interlocked + write-once) | authority unit + scope test |
| 8 | Signature valid but possession missing / job not granted | **no minting** (stage separation) | authority unit |
| 9 | Caller supplies another source/capability after binding | `JobScopeMismatch` in Application; zero calls/writes | PG integration |
| 10 | Missing trust / mismatched DB scope | `None` ⇒ `UnauthorizedTenantException` | PG integration |
| 11 | Two concurrent job scopes, different tenants | independent bindings | PG integration |
| 12 | Cancellation | propagates; no context | authority unit |
| 13 | Disabled source after a valid grant | existing `AccountingSourceDisabledException` | PG integration (W4-B1) |
| 14 | Every rejection | zero provider calls, zero writes | PG integration |

---

## 12. Repair of the C1/C2 inconsistency + one bounded slice

Revision 2's slice C1 both created trusted evidence and created the authority project while claiming not to create authority — inconsistent. Repaired:

- **C1 (proposed, 30–40 min) — "claims verification only; cannot create trusted authority".** Outcome: a pure `GrantVerifier` in `InstallationAuthority.Infrastructure` that turns grant bytes into `ValidatedGrantClaims` (internal ctor, defensive copies) or a failure, using the pinned key store; **no possession, no offline policy, no job authorization, no `AuthorizedJobGrant`, no `TenantContext`, no binding, no `BuildingBlocks` change, no AMD-1**. It is impossible for this slice to create trusted authority because it has no reference to any minting path.
  - Allowed files: `src/Modules/InstallationAuthority/InstallationAuthority.Infrastructure/` (project + `Grants/{GrantVerifier.cs, GrantClaims.cs, ValidatedGrantClaims.cs, TrustedAuthorityKeySet.cs}` + `Composition/…` placeholder only if needed for DI later — not required in C1); `tests/FaraTaraz.InstallationAuthority.Tests/`; architecture allowlist updates for the new project (`ArchitecturePolicy` known-project set, `PhysicalStructureTests`, `OrphanSourceFileTests`).
  - Tests: matrix rows 2–5 plus malformed/oversize/duplicate/cancellation, using a test-only ES256 signer.
- **C2 (separate authorization, not this task) — "job authorization + restricted minting".** Adds `JobAuthorizer`, `AuthorizedJobGrant`, possession signer, offline policy, `ScopedTrustedTenantBinding`, AMD-1 and the Application `JobScopeMismatch` check. **Blocked until the architect accepts AMD-1.**

---

## 13. Unresolved decisions / blockers

1. **AMD-1 acceptance** (one broadly-privileged production friend) — required before C2; C1 does not need it.
2. New project `InstallationAuthority.Infrastructure` and its test project must be approved (necessary: no existing project can own installation authority without violating the ownership map).
3. Pinned key custody (compiled constants vs OS-protected store) — product/ops decision.
4. Activation-service hosting and company signing-key custody (HSM/KMS) — external.
5. Installation private-key facility on the three customer machines (CNG/TPM vs DPAPI) — unverified.
6. Real Asan connectivity — UNKNOWN.
7. Offline-revocation and local-admin residual risk must be explicitly accepted.
8. Whether the Application `JobScopeMismatch` check requires a new inward port or extends an existing one — to be decided in C2.

---

## 14. Continuity

W1–W3 and W4-B1/W4-B2 accepted; ADR-011 stays **Proposed** and additionally requires the amendment note replacing its `IAuthenticatedPrincipal` port with evidence-based verification. This document is design only and authorizes no code. The next chat must read this document, the W4 decision documents, `RECOVERY.md`, `OPEN-DEFECTS.md` and `ARCHITECT-JOURNAL.md`, fetch live `origin/master`, and reconcile executor status before any implementation instruction.

**STOP_FOR_ARCHITECT.**

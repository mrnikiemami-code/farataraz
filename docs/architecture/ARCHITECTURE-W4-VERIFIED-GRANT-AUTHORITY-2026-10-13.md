# FaraTaraz — W4 Verified Installation-Grant Authority & Restricted TenantContext Creation

**PROPOSED — AWAITING INDEPENDENT ARCHITECT ACCEPTANCE.**
**Revision 2** — rewritten after the binding responsibility-ownership constraints (2026-10-13) that **reject** placing the verifier/JWS/keys inside `BuildingBlocks`. Revision 1's placement is withdrawn.

**Task:** `FT-W4-B3-VERIFIED-GRANT-AUTHORITY-DESIGN-001` — BOUNDED SECURITY ARCHITECTURE DESIGN (design only; no production code).
**Baseline:** local == remote == `b7247f171030e42931ec3b34f611aaa463567beb` (origin/master fast-forwarded; worktree clean) — exactly the expected recovery checkpoint.
**Accepted context:** W1, W2, W3, W4-B1, W4-B2 accepted; ADR-011 **Proposed**; production delivery incomplete.
**Still rejected:** public `TenantContext.Establish(IAuthenticatedPrincipal)`; caller-implemented verifier interfaces; fake authority producers; production mock providers.

---

## 1. Binding responsibility map (the acceptance condition)

| Owner | Owns | Must never own |
|---|---|---|
| **BuildingBlocks** | shared concepts **+ the minimal, crypto-free trust-establishment boundary** (the only minting path) | license logic, JWS/crypto, key storage, renewal, activation |
| **Domain** | business rules | crypto, HTTP, DI, Worker, tenant authority |
| **Application** | use-case execution and access-control **ordering**; depends on inward contracts | a verifier implementation, EF, transport |
| **Infrastructure** (clearly owned location) | cryptographic verification, key storage, issuer communication | business rules, use-case orchestration |
| **Worker** (delivery) | receive work, dispatch through `ISender` | security policy, business logic |
| **Host** | composition only | business authority |

No project, layer or interface is added unless it has a real responsibility and a stated necessity (§6).

---

## 2. Actual boundary (file:line evidence)

| Fact | Evidence |
|---|---|
| `TenantContext` ctor and `FromAuthenticatedPrincipal` are **internal**; public surface is read-only + `AssertTrusted()` | `TenantContext.cs` L41, L53, L60, L67, L74 |
| `DatabaseTenantScope.None` is the fail-closed default; `FromTrusted` needs an already-trusted context; ctor private | `DatabaseTenantScope.cs` L19, L25, L35-40, L47 |
| Module DI **reads** a context nothing registers | `IngestionInfrastructureComposition.cs` L50-56; `AccountingSourcesInfrastructureComposition.cs` L53-59 |
| Friends are exactly three **test** assemblies (pinned by a guard) | `FaraTaraz.BuildingBlocks.csproj` L23-25; `TenantTrustSourceTests` |
| Mandatory admission before provider/persistence (unchanged, and stays mandatory) | `RunCustomerSyncHandler.cs` L69 → L83 (`IsOwnedByAsync`) → L97 (`IsActiveAsync`) → L109 (provider resolution) → L136 (provider) → L168 (transaction); Query path mirrors it |
| No production code mints a trusted context today | repo-wide: only tests call the internal factory |
| Constraining guards | `TenantTrustSourceTests`, `TenantBoundaryGuards`, `ArchitecturePolicy`, `DependencyDirectionTests`, `OwnershipPathAuthorizationGuard` |

---

## 3. The conflict (stated, not concealed)

The constraints are jointly unsatisfiable with a purely public surface:

- **C-A:** cryptographic verification, key storage and issuer communication must live in **Infrastructure** (not BuildingBlocks).
- **C-B:** `TenantContext` must stay low (Application/Domain reference it) ⇒ **minting** must stay in `BuildingBlocks`.
- **C-C:** the minting input must be **non-forgeable** (no public factory; no caller assertion; no caller-implemented verifier interface; no fake producer).
- **C-D:** a non-forgeable "verification succeeded" value can only be constructed **inside** the assembly that defines it (C# visibility).
- **C-E:** no production `InternalsVisibleTo` (the friend list is pinned to test assemblies by an explicit guard).

C-C + C-D ⇒ the verifying code must be in the same assembly as the verified-evidence type. C-A ⇒ that assembly must not be `BuildingBlocks`. C-B ⇒ the evidence type must be visible to `BuildingBlocks`. Together ⇒ either the evidence type lives in a **lower** assembly than Infrastructure (contradiction with C-A) or Infrastructure needs **visibility** into the evidence type (contradiction with C-E) — **or** BuildingBlocks hosts crypto (contradiction with C-A). One of C-A…C-E must yield.

### 3.1 Smallest explicit amendment — **AMD-1 (recommended)**

**Grant exactly one production `InternalsVisibleTo` from `FaraTaraz.BuildingBlocks` to the single, named trust-authority Infrastructure assembly** (proposed name: `FaraTaraz.Modules.InstallationAuthority.Infrastructure`). The friend is used **only** to construct the sealed evidence type `TrustedInstallationGrant`; minting remains exclusively in `BuildingBlocks`. The friend guard (`TenantTrustSourceTests`) is updated by **architect decision** to `{3 test assemblies} ∪ {that one assembly}`, still fail-closed against every other production assembly (Host, Worker, Application, Domain, adapters, other Infrastructure).

- Yields **C-E only**, exactly once, explicitly named and reviewable.
- Keeps C-A (crypto in Infrastructure), C-B (minting low), C-C (no public factory/assertion), C-D satisfied for `BuildingBlocks`‑defined evidence.
- Honest limit: an assembly-level friend has *full* internal access; a guard/test that it constructs evidence and does not call the factory directly is **supplementary** (lexical scans cannot prove behaviour — the architect already ruled that scans do not replace runtime checks). A compromised trust-authority assembly is a compromised process.

### 3.2 Alternatives evaluated

| Option | Yields | Verdict |
|---|---|---|
| **AMD-0** Verifier/JWS/keys inside `BuildingBlocks` | C-A | **REJECTED** by the binding constraints (revision 1) |
| **AMD-2** Public `TrustedInstallationGrant` ctor + public factory taking it | C-C, C-D | **REJECTED** — forgeable assertion |
| **AMD-3** Caller-implemented `ITenantEvidenceVerifier` port returning "verified" | C-C | **REJECTED** (explicitly prohibited) |
| **AMD-4** Security policy inside the Worker (Worker verifies and asserts) | ownership | **REJECTED** — Worker must own no security policy |
| **AMD-5** Move `TenantContext`/`DatabaseTenantScope` out of `BuildingBlocks` into the trust authority | C-B | **REJECTED** — Application/Domain reference them; would invert the dependency graph |
| **AMD-1** One named production friend | C-E | **RECOMMENDED** |

If the architect rejects AMD-1, the only remaining honest position is that V1 cannot have both a crypto-free BuildingBlocks and a non-forgeable minting seam; the feature would then have to be deferred rather than shipped with a weakened boundary.

---

## 4. Selected design (under AMD-1)

### 4.1 Placement

| Component | Assembly / layer | Contents |
|---|---|---|
| **Minimal trust boundary** | `FaraTaraz.BuildingBlocks.Tenancy` (foundation) | evidence type, establisher, scoped holder, `TenantContext` scoped registration, composition extension. **No crypto, no keys, no JWS, no license, no renewal, no activation.** |
| **Trust authority** | NEW `src/Modules/InstallationAuthority/InstallationAuthority.Infrastructure` (Infrastructure; §6 justifies necessity) | ES256/JWS verification, pinned key store, OS installation-key signer (possession proof), issuer communication (activation/renewal client), offline window/clock policy, and the public `InstallationJobAuthority` service + module `Composition`. |
| **Delivery** | future `src/Host/FaraTaraz.Worker` | opens one job scope, calls the authority, then `ISender`. No policy. |
| Domain / Application / Host | unchanged | — |

### 4.2 Exact types and signatures

**`BuildingBlocks.Tenancy` (crypto-free boundary):**

| Type | Visibility | Shape |
|---|---|---|
| `GrantJobScope` | public readonly record struct | `(AccountingSourceId SourceId, string Capability)` — carries no trust |
| `TrustedInstallationGrant` | **public sealed record, `internal` constructor** | `TenantId`, `InstallationId`, `GrantId`, `IReadOnlyList<GrantSourceScope> Scopes`, `NotAfterUtc`, `SigningKeyId` — immutable; constructible **only** inside `BuildingBlocks` (or the §3.1 friend) |
| `TrustEstablishmentResult` | public enum | `Verified, Malformed, UnknownKey, InvalidSignature, Expired, NotYetValid, OutOfScope, PossessionFailed, ClockRollback, OfflineWindowExceeded, AuthorityUnavailable` |
| `TenantContextEstablisher` | public sealed (scoped) | `TrustEstablishmentResult Establish(TrustedInstallationGrant grant, GrantJobScope scope)`: validates `scope ⊆ grant.Scopes` and `now < NotAfterUtc`, then mints via the **internal** factory and binds the scoped holder. **The only minting call site.** |
| `TrustedTenantHolder` | internal sealed (scoped) | write-once per scope; holds the minted `TenantContext` |
| `AddTrustedTenantBoundary()` | public extension | registers `TrustedTenantHolder`, `TenantContextEstablisher`, and `TenantContext` (scoped, factory → holder). **Takes no key, URL or store parameter.** |

**`InstallationAuthority.Infrastructure`:**

| Type | Shape |
|---|---|
| `TrustedAuthorityKeySet` / `TrustedAuthorityKeys` | pinned `keyId → ECDSA P-256 public key`; the store is configuration of **this** assembly, never a grant field, never a URL, never a parameter of the boundary |
| `GrantVerifier` | pure, deterministic ES256 verification + strict claim validation → `TrustedInstallationGrant` (constructed through the §3.1 friend) or a failure result |
| `IInstallationKeySigner` / `OsInstallationKeySigner` | signs a fresh challenge with the installation private key (Windows CNG/TPM non-exportable preferred, else DPAPI) |
| `OfflineWindowPolicy` | monotonic high-water mark + `maxOfflineDays` window (§5) |
| `InstallationJobAuthority` | **public** service resolved by delivery: `Task<GrantOutcome> TryAuthorizeAsync(ReadOnlyMemory<byte> grantEvidence, GrantJobScope scope, CancellationToken ct)` → verifies, proves possession, then calls `TenantContextEstablisher.Establish`; exposes `TenantContext? Current`. Owns **no** business rule and **no** persistence. |
| `InstallationAuthorityComposition` | registers the above and calls `AddTrustedTenantBoundary()` |

### 4.3 Why callers cannot forge trust

1. **No assertion input.** The only public entrypoints take opaque grant **bytes** and a `GrantJobScope`. No principal interface, no `bool`, no decoded claims, no caller-supplied verifier.
2. **Crypto precedes trust.** `TrustedInstallationGrant` is only obtainable from a successful ES256 verification of pinned-key-signed bytes **plus** a fresh verifier-generated possession proof.
3. **No injectable trust configuration.** `AddTrustedTenantBoundary()` and `InstallationJobAuthority.TryAuthorizeAsync` accept no key/URL/store; the grant header can never supply one; the pinned store is compiled into the authority assembly.
4. **Evidence-only API.** The public surface never accepts "verification already succeeded".
5. **Sealed, single-tenant, single-job evidence.** The evidence is public-read-only but internally constructed; the establisher enforces `scope ⊆ grant` and expiry, and the holder is **write-once per scope**, so a grant cannot authorize a different job/tenant/source/capability.
6. **One isolated job scope.** Delivery opens a scope, authorizes, then dispatches; `TenantContext` (scoped, from the holder) drives the existing unchanged `TryAddScoped<DatabaseTenantScope>` factories. Before authorization the holder is unset ⇒ `None` ⇒ `AssertTrusted()` throws ⇒ **zero provider calls, zero writes**.
7. **No ambient/global state.** The holder is scoped (not static, not `AsyncLocal`); concurrent job scopes are independent.
8. **Layer purity.** Domain untouched; Application untouched (no verifier/EF dependency — it only consumes the public `TenantContext` value); Host composition-only; Worker sequences two components and dispatches — it decides nothing.

### 4.4 Threat-model limit (honest)

Managed-code visibility, static guards and local storage cannot defeat a local administrator, in-process reflection/debugging, a compromised process, or someone who rebuilds the authority assembly with their own pinned key. Those parties already own the trust decision point. The real guarantee: **untrusted evidence from outside the process (network, file, job payload, caller assertion) cannot by itself produce a trusted context**, and a copied grant cannot be used on a machine that does not hold the installation private key.

---

## 5. Frozen minimal verification contract (V1) — one algorithm, one encoding

- **Algorithm:** `ES256` only (ECDSA P-256 + SHA-256). **Encoding:** JWS Compact; signature **P1363 fixed-width 64-byte `r||s`** (RFC 7515), **not** DER; `.NET ECDsa.VerifyData(..., DSASignatureFormat.IeeeP1363FixedFieldConcatenation)`.
- **Signed bytes:** ASCII `base64url(header) + "." + base64url(payload)`.
- **Header (exact allowed set):** `{"alg":"ES256","kid":"…","typ":"FT-GRANT+JWS"}`; `alg` must be exactly `ES256`; `kid` must resolve in the pinned store; `jwk`/`jku`/`x5u`/`x5c`/`crit` absent-or-rejected.
- **Payload (required, no duplicates):** `iss`, `aud`, `grantId`, `installationId`, `installationPublicKey` (base64url SPKI), `tenantId`, `scopes[{sourceId,capabilities[]}]`, `iat`, `nbf`, `exp`, `maxOfflineDays`, `signingKeyId`. Unknown non-critical fields ignored; duplicate keys rejected.
- **Issuer/audience:** `iss` == compiled company issuer constant; `aud` == `farataraz-worker`.
- **Pinned key source:** compiled into the authority assembly; **rotation** = add the new public key under a new `kid` before the issuer signs with it; unknown `kid` rejected; bounded set.
- **Strict parsing:** ≤ 8 KiB; ≤ 64 scopes; ≤ 8 capabilities/scope; integer timestamps; `exp > nbf ≥ iat`; `exp − iat ≤ 30 d`; `maxOfflineDays ≤ 30`; strict base64url; no trailing bytes; malformed/duplicate/oversize ⇒ no trust.
- **Binding:** one `tenantId`; `installationId` + `installationPublicKey`; explicit `scopes`; the requested `GrantJobScope` must be a member.
- **Validity/offline:** `nbf ≤ now < exp`; offline capped at `min(maxOfflineDays, 30)` from the **last server-authenticated instant** (signed `iat` of the newest grant), not from a local clock read.
- **Possession (mandatory, fresh, verifier-generated):** a 128-bit random challenge is generated per call (never a parameter) and signed by the OS-held installation private key over `challenge ‖ tenantId ‖ installationId ‖ grantId ‖ sourceId ‖ capability`; verified against the grant-bound public key. Thumbprint equality alone is never accepted; a captured proof cannot be replayed.
- **Replay:** offline — fresh internal challenge, never persisted; online (upstream) — issuer nonce ≥128-bit, single-use, session-bound, ≤2 min TTL.
- **Cancellation/failure:** `OperationCanceledException` propagates from every step; every non-cancellation failure leaves the holder unset.
- **Clock/offline limits:** write-only-forward high-water mark; clock behind it ⇒ `ClockRollback`; beyond the window ⇒ `OfflineWindowExceeded` (data retained; only new sync blocked). **Mutable local timestamps are not a trusted clock**, and **offline revocation cannot be immediate** — stronger guarantees need an online check or TPM-monotonic/attested time (out of V1 scope).
- **Upstream enrollment (precise, not built):** company-operated issuer; one-time code = 128-bit secret stored only as a hash with `tenantId`, scoped sources/capabilities, `iat`, `expiresAt ≤ 24 h`, attempt counter, `consumedAt`; consumption is a single atomic conditional update whose row-count gates grant issuance in the same transaction; rate-limited and audited; client retry after consumption requires explicit re-issue.

---

## 6. Necessity of the one new project

`InstallationAuthority.Infrastructure` is the **only** new project, and it is required because no existing project can own it without violating the responsibility map: `AccountingSources.*` owns tenant/source ownership (not installation authority); `Ingestion.*` owns synchronization; `BuildingBlocks` is crypto-free by the binding constraints; Host is composition-only; the Worker must own no policy. One Infrastructure project (with a `Composition` folder, mirroring the existing module pattern) is the smallest real owner. A Domain project is **not** created (no business rules yet; ADR-009 permits omitting a layer with a documented reason). `GrantJobScope` lives in `BuildingBlocks` because Application/Domain must be able to express it without referencing the authority.

---

## 7. Acceptance matrix (scenario → outcome → test layer)

| # | Scenario | Expected outcome | Test layer |
|---|---|---|---|
| 1 | Valid signature + correct scope + valid possession | `Verified`; trusted context bound; one bounded step | Authority unit + PG integration |
| 2 | Tampered payload / invalid signature | `InvalidSignature`; no context | Authority unit |
| 3 | Unknown `kid` / wrong `iss`/`aud` / `alg` not ES256 | no context | Authority unit |
| 4 | Expired / not-yet-valid | no context | Authority unit |
| 5 | Foreign tenant / unauthorized source or capability | `OutOfScope`; no context | Authority unit |
| 6 | Copied grant without the installation private key | `PossessionFailed`; no context | Authority unit |
| 7 | Replayed possession proof / grant reused in the same scope | rejection (fresh challenge; write-once holder) | Authority unit + scope test |
| 8 | Caller-created fake principal/verifier/evidence | no such API exists (public-surface guard test) | Architecture guard |
| 9 | Attempted replacement of the pinned trust configuration | boundary/authority expose no key parameter (guard test) | Architecture guard |
| 10 | Missing trust / mismatched DB scope | `None` ⇒ `UnauthorizedTenantException`; zero calls/writes | PG integration |
| 11 | Two concurrent job scopes, different tenants | independent holders; no leakage | PG integration |
| 12 | Cancellation during establishment | `OperationCanceledException`; no context | Authority unit |
| 13 | Disabled source after a valid grant | existing `AccountingSourceDisabledException`; zero calls/writes | PG integration (W4-B1) |
| 14 | Every rejection | zero provider calls, zero ingestion writes | PG integration (counting port) |

Existing ownership → activation → provider checks stay mandatory and unchanged.

---

## 8. Unresolved decisions and blockers

1. **AMD-1 must be accepted by the architect** (one named production friend + guard allowlist update). Nothing else in this design is implementable without it.
2. New project `InstallationAuthority.Infrastructure` requires approval (necessity argued in §6); if refused, the design cannot be implemented without a weaker boundary.
3. Pinned key custody for V1 (compiled constants vs OS-protected store) — product/ops decision; compiled is assumed.
4. Activation-service hosting and company signing-key custody (HSM/KMS) — external, unimplemented.
5. Installation private-key facility available on the three customer machines (CNG/TPM vs DPAPI) — unverified.
6. Real Asan connectivity — UNKNOWN (W4 connectivity decision).
7. Offline-revocation and local-admin residual risk must be explicitly accepted.

---

## 9. One proposed coding slice (30–40 minutes) — proposal only, not authorization

**Slice `W4-C1 — ES256 installation-grant verification core (authority assembly, no DI, no delivery)`.** This slice is **conditional on AMD-1 + §6 approval**; without them it would have to live in `BuildingBlocks` (rejected).

- **Outcome:** a pure `GrantVerifier` in `InstallationAuthority.Infrastructure` that accepts a valid ES256/P1363 JWS grant signed by the pinned test key and rejects tampering, algorithm confusion, unknown `kid`, bad `iss`/`aud`, expiry/not-yet-valid, oversized/duplicate-field payloads and out-of-scope job requests — producing an internal-ctor `TrustedInstallationGrant` that cannot be publicly constructed. No DI, no Worker, no activation service.
- **Allowed files (exact):**
  - NEW `src/Modules/InstallationAuthority/InstallationAuthority.Infrastructure/FaraTaraz.Modules.InstallationAuthority.Infrastructure.csproj`
  - NEW `…/Grants/{GrantJobScope? (in BuildingBlocks), TrustedAuthorityKeySet.cs, GrantVerifier.cs, GrantClaims.cs}` — `GrantJobScope` itself goes to `BuildingBlocks.Tenancy` (Application/Domain must express it)
  - NEW `…/Composition/InstallationAuthorityComposition.cs`
  - EDIT `src/BuildingBlocks/Tenancy/` (add `GrantJobScope`, `TrustedInstallationGrant` with internal ctor) and `src/BuildingBlocks/FaraTaraz.BuildingBlocks.csproj` (`InternalsVisibleTo` → the authority assembly, **the AMD-1 change**)
  - NEW `tests/FaraTaraz.InstallationAuthority.Tests/` (crypto tests; a dedicated test project is necessary because the crypto internals are not visible to the other test projects)
  - EDIT `tests/FaraTaraz.ArchitectureTests/` allowlists (`ArchitecturePolicy` known-project set, `PhysicalStructureTests`, `OrphanSourceFileTests`) and `TenantTrustSourceTests` (friend allowlist, architect-approved)
  - EDIT `docs/planning/RECOVERY.md`
- **Tests:** matrix rows 2–6 plus valid/malformed/oversize/duplicate/cancellation.
- **Not in this slice:** `TenantContextEstablisher`, scoped holder, `AddTrustedTenantBoundary`, OS key signer, offline window, Worker (slice C2).

---

## 10. Continuity

W1–W3 and W4-B1/W4-B2 accepted; ADR-011 stays **Proposed** and additionally requires the §3.1 amendment note (its `IAuthenticatedPrincipal` port wording is superseded). This document is design only and authorizes no code. The next chat must read this document, the W4 decision documents, `ARCHITECTURE-W4-ACTIVATION-TRUST-CONTRACT-2026-10-13.md`, `RECOVERY.md`, `OPEN-DEFECTS.md` and `ARCHITECT-JOURNAL.md`, fetch live `origin/master`, and reconcile executor status before any implementation instruction.

**STOP_FOR_ARCHITECT.**

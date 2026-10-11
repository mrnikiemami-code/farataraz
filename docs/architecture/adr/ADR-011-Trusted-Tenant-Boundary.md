# ADR-011: Trusted Tenant Boundary — Tenant Authority Establishment (Target)

**Status:** Proposed — pending independent architect acceptance. This ADR records the *decision*
that unblocks implementation; it does **not** authorize code. No production tenant-authority
producer is implemented until an explicit wave is authorized against this ADR.
**Date:** 2026-10-11
**Author:** D-2 decision task (`FT-ARCH-002`)
**Depends on:** ADR-002 (Multi-Tenant Model), ADR-007 (Shared Application Authority),
ADR-009 (FMCA), ADR-010 (Persistence)
**Relates to:** Constitution A.3–A.6, G.27–G.30, I.35–I.36; open defects **FT-SEC-004** and
**D-2** in `docs/planning/OPEN-DEFECTS.md`.

## Context

Tenant authority must originate only from a trusted execution context and never from
client/LLM/MCP input (Constitution A.4). `TenantContext` encodes this: its constructor and
`FromAuthenticatedPrincipal` are **internal to `BuildingBlocks`** (commit `39164cd`,
FT-SEC-004 partial mitigation), so no other assembly can mint a trusted context, and
`tests/.../TenantBoundaryGuards.cs` rejects any `TenantContext.FromAuthenticatedPrincipal` /
`new TenantContext` outside `src/BuildingBlocks`.

That closes the *untrusted-minting* gap but leaves the inverse gap (**D-2**):

- **There is no sanctioned producer of a trusted `TenantContext`.** No production code calls the
  internal factory, and no `TenantContext` is registered in DI. The module Infrastructure
  compositions resolve `DatabaseTenantScope` from `GetService<TenantContext>()`
  (ADR-010 decision 5), so in the current repository the scope is **always `None`** and every
  tenant-scoped operation fails closed. The trust boundary is defined but has no door.
- The ledger's required closure for FT-SEC-004 is explicit: *"Independently establish legitimate
  auth authority source and verify fail-closed wiring … **Do not invent authentication**."*

A producer can only be one of two things: (1) an **authenticated-principal** boundary
(request/connector admission) or (2) **background execution** with explicit, platform-supplied,
non-client context (Constitution A.5). Background execution belongs to W3 (not authorized), and
authentication for a delivery surface belongs to the deferred Endpoints wave. The lawful decision
now is therefore the *shape* of the boundary, not its implementation.

## Decision

1. **A single sanctioned establishment seam.** A trusted `TenantContext` is produced only by an
   explicit admission step that converts an **already-authenticated principal** into a trusted
   context. The conversion is **fail-closed**: a missing or unauthenticated principal yields no
   trusted context (→ `DatabaseTenantScope.None`), never authorization success.

2. **Minting stays internal; the seam is a `BuildingBlocks`-owned API.** `TenantContext`'s
   constructor and `FromAuthenticatedPrincipal` remain `internal`. The public establishment API
   lives in `BuildingBlocks.Tenancy` and is the *only* sanctioned conversion. It accepts an
   `IAuthenticatedPrincipal` port and internally calls the existing internal factory.

3. **The principal is a port; the concrete authentication adapter is out of scope.** This ADR
   defines `IAuthenticatedPrincipal` (`IsAuthenticated`, `TenantId`) as the contract the trusted
   boundary must satisfy. **No authentication mechanism (JWT, cookie, connector handshake,
   scheduler) is designed or implemented here** — that is a separate decision. This ADR must not
   be read as evidence that authentication exists.

4. **Boundary ownership and placement.** The principal supplier is a **delivery/execution admission**
   concern, not a business concern:
   - request-scoped admission belongs to the FMCA `<Capability>.Endpoints` / delivery layer
     (ADR-007, ADR-009);
   - background admission belongs to the background execution boundary (Constitution A.5) and
     runs with explicit, platform-supplied `Tenant` + `AccountingSource` context;
   - it must **not** live in Module Application/Domain/Infrastructure (no business authority) and
     **not** in the composition-only Host as a business behavior.

5. **Scoped production of the context.** A composition helper binds the scoped `TenantContext`
   from the authenticated principal for the current unit of work. When no authenticated principal
   is bound, the produced context is untrusted (fail-closed), so ADR-010 decision 5's
   `DatabaseTenantScope` behavior is unchanged. No static/global tenant and no mutable global
   tenant context is introduced (Constitution A.5).

6. **Enforcement is durable and negative-tested.** `TenantBoundaryGuards` is extended so that,
   outside `src/BuildingBlocks`: (a) the establishment seam may be invoked only from a designated
   trusted-boundary code path; (b) `IAuthenticatedPrincipal` may be implemented only by the
   designated boundary; (c) the existing prohibition on `new TenantContext` /
   `FromAuthenticatedPrincipal` remains. The seam is proven with a **fake authenticated principal**
   (trusted path) and with **missing/unauthenticated input** (fail-closed path), and the guards are
   proven by synthetic negative cases (inject → fail → revert).

7. **No `TenantContextOrigin` flag alone is authority.** The origin flag is not authentication
   proof (FT-SEC-004). Trust is established by the boundary in decision 4 satisfying the
   `IAuthenticatedPrincipal` contract, never by an untrusted caller setting a flag.

## Consequences

- **D-2 / FT-SEC-004 cannot be marked VERIFIED by this ADR.** They remain OPEN until the seam, a
  concrete principal supplier, and the guards are implemented, independently reviewed, and
  exercised end-to-end. This ADR only removes the *design* ambiguity that blocked those steps.
- **W3 and any tenant-scoped production execution remain blocked** until the seam is implemented
  and a principal supplier exists; otherwise production stays fail-closed with no way to authorize
  legitimate work.
- **`DatabaseTenantScope` and the Infrastructure compositions are unchanged** in behavior:
  untrusted/missing context → `None` → fail-closed.
- **New guard surface.** Extending `TenantBoundaryGuards` (decisions 6–7) is mandatory for any
  implementation wave; the guards must not be weakened to obtain a green build.
- **Tenant isolation invariant is preserved** (Constitution A.3): the seam changes where authority
  is *produced*, never how tenant-owned data is *scoped* (still the trusted `DatabaseTenantScope`).

## Rejected alternatives

- **Public minting API on `TenantContext`** (e.g. making `FromAuthenticatedPrincipal` public):
  rejected — regresses FT-SEC-004 and lets arbitrary assemblies mint authority.
- **Host reads the tenant from the request / client / MCP / LLM input:** rejected — violates
  Constitution A.4 and I.35.
- **Static/global or ambient (`AsyncLocal`) tenant context:** rejected — violates A.5 (no
  static/global tenant, no mutable global context).
- **Trusting `TenantContextOrigin.AuthenticatedPrincipal` set by any caller:** rejected — the flag
  is not authentication proof (FT-SEC-004).
- **A single always-`true` ownership/tenant fake for production:** rejected (ADR-002, ADR-010
  decision 6); fail-closed remains the default.
- **Implementing a specific authentication protocol here:** rejected — out of scope and explicitly
  prohibited by the ledger ("do not invent authentication").

## Implementation gating

No agent may implement this seam, a principal supplier, or any tenant-scoped production execution
automatically. Implementation requires an explicitly authorized wave (naming the boundary: delivery
Endpoints or background execution per Constitution A.5) that references this ADR, updates
`TenantBoundaryGuards` with negative proofs, and preserves every existing tenant/CQRS/idempotency
invariant. Until then, D-2 stays **OPEN** and W3 remains **NOT AUTHORIZED**.

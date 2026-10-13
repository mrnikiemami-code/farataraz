# W4 deployment decision — Local-first, central-ready

**Date:** 2026-10-13
**Decision source:** Explicit user choice; architect-owned binding deployment direction.
**Status:** APPROVED DEPLOYMENT DIRECTION ONLY; not a production implementation authorization.

## Binding decision

FaraTaraz is initially **installed on each customer's Windows computer** and runs its customer-sync process as a **.NET background Worker**. It must remain straightforward to redeploy the **same Worker/application/domain/provider-adapter codebase** to a central server later. The future authenticated API is another delivery adapter to the same Application command/query path; it is **not implemented in the first delivery**. No local-vs-central business logic forks.

## Deployment-neutral architecture

- One Worker binary and shared Application/Domain/Contracts for both local and central hosting; hosting configuration selects endpoint, credentials, tenant/source provisioning and connection strings. Host remains composition-only.
- Accounting-provider interaction remains behind capability/provider interfaces. Locally, the adapter may access the customer accounting software directly; centrally, the adapter may connect over a supported remote API. If accounting software is only accessible on customer LAN/PC, a small on-premises connector/agent or an explicitly approved secure tunnel may still be required. **Central switch is conditional on actual accounting software reachability, not guaranteed by configuration alone.**
- Use configuration/secret storage appropriate to each deployment, not source-controlled passwords. A customer installation must be bound to an independently provisioned tenant/source authorization grant. Central runtime must not derive trust from an arbitrary tenant ID in job payload/config without verifying the grant.
- PostgreSQL can be local or server-hosted; connection-string switching is not sufficient by itself for data migration, encryption, backups, tenant separation, schema versioning or cutover. Preserve stable IDs and audit/checkpoint/CAS semantics, and plan explicit safe migration/rollback.
- Windows Service hosting initially; prefer cross-platform .NET Worker host code, avoid Windows-only core dependencies. Platform-specific installation/service registration remains at delivery edge.
- Worker job scheduling, restart behavior, concurrent execution leases, tenant/source scoping, credential rotation, retries and observability require concrete W4 design before implementation.
- Future API must validate authenticated principal, map verified tenant/source authority and call the same MediatR ISender handlers. Never expose unrestricted TenantContext minting.
- No speculative new TrustedExecution assembly or generic distributed platform solely for hypothetical central hosting.

## W4 implementation gate

Before coding, identify actual accounting software connection protocol/provider, installation and service account constraints, trustworthy per-customer enrollment/provisioning mechanism, trusted job source, scheduling trigger, local PostgreSQL choice, central remote connectivity feasibility, and Windows service install/upgrade/rollback approach. Capture negative fail-closed behavior before provider calls and DB writes. ADR-011 remains Proposed until a real trusted authority producer is accepted. A bounded read-only W4 delivery/security design task is the next step.

## Acceptance of central-ready requirement

Prove with configuration/integration tests that the same Worker/Application binaries can run against two isolated authorized tenant/source configurations and different DB endpoints without code changes; do not claim actual central deployment validated until connectivity, credentials, tenant isolation and migration/cutover are tested in a representative environment.

## Continuity

W1 audit, W2 Domain and W3 Application/guard cleanup are accepted. W4 delivery shape: Worker first, future authenticated API (see ARCHITECTURE-W4-DUAL-DELIVERY-DECISION-2026-10-13.md). This document adds the **local-first, central-ready** deployment decision. No W4 production code started; external identity/provider facts still pending.

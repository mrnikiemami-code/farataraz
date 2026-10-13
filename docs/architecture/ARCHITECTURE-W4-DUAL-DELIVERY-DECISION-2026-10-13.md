# W4 delivery decision — Worker first, API ready

**Date:** 2026-10-13
**Authority:** User decision, recorded by independent architect
**Status:** BINDING DESIGN DIRECTION, NOT implementation or production security certification

## Decision
FaraTaraz initially executes customer synchronization as a **background Worker**. The architecture **must support both background Worker and future authenticated HTTP API** without duplicating application/business logic. Do not implement the HTTP API now solely for symmetry.

## Approved shape
- **Background Worker (first delivery):** a hosted runtime invokes the same MediatR Application command via ISender. Worker obtains a *verified* tenant/source authority grant from a trusted, provisioned job/service configuration; not from arbitrary caller-supplied tenant IDs.
- **Authenticated API (future delivery):** a distinct entrypoint validates an authenticated principal and authorizes its tenant/source scope, then invokes the same Application command. It must never trust tenant ID from request body alone.
- **Shared inward seam:** both delivery adapters yield the same validated tenant authority context and source scope; Application retains trust assertions, ownership-before-provider and transaction/CAS semantics. Domain and Contracts remain transport-independent.
- **Host:** composition-only, no business or tenant-authority policy. Do not assume an additional TrustedExecution assembly is necessary.
- **Security:** internal-only TenantContext minting must be resolved through a narrowly reviewed producer interface/assembly boundary. No public unrestricted factory, fake principal, or test-only adapter passed off as production.

## Required W4 design gate before code
Identify: (1) Windows service vs cross-platform Worker runtime and lifecycle; (2) exact job trigger (schedule/manual queue); (3) authoritative tenant/source provisioning and credential storage; (4) trusted service identity and verification; (5) real accounting provider adapter and its API/credentials; (6) retries/idempotency/cancellation/observability; (7) unauthorized/unowned/disabled-source fail-closed behavior before provider or database writes; (8) future HTTP integration contract without implementing it. Keep ADR-011 Proposed until these are evidenced and independently accepted.

## Scope and continuity
This decision **unblocks the delivery-shape choice**, not real trust authority or provider availability. W1–W3 remain accepted; W4 implementation remains conditional on a concrete trusted worker configuration/identity and real provider. Next work should be a bounded **W4 design and authority-contract task** (read-only, no production code), followed by architect approval and only then implementation. W5 remains separate E2E certification. Never invent provider APIs, credentials, tenant grants, or production readiness.

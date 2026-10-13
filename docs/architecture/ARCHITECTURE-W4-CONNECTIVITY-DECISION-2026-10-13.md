# W4 connectivity decision — local now, remote capable

**Date:** 2026-10-13
**Source:** Explicit user requirement
**Status:** Binding architectural direction; remote protocol NOT verified, W4 production implementation not authorized.

## Verified user-provided facts
- The three Asan accounting installations currently run on each customer's machine.
- The user knows that Asan has been connected to WordPress in at least one configuration.
- Both local and future central/remote access must be accommodated.
- The first deployment is a customer-local Windows .NET Worker; future central Worker and authenticated API remain required design options.

## Important inference boundary
A WordPress integration demonstrates some external integration route but does **not** establish a public/documented Asan HTTP API, its authorization protocol, supported operations, server accessibility, or permission to use that integration. Obtain vendor documentation or inspect the actual authorized connector before choosing a remote transport.

## Approved architectural seam
- **Application** depends on provider-neutral `IAccountingProvider` / `ISyncablePort<T>` capability contracts. It does not branch on local versus remote transport.
- **Infrastructure/adapters** select an appropriate connection implementation based on validated connection configuration: customer-local access first; future authenticated remote API or customer-local bridge/agent if direct remote access is impossible.
- A remote API and a local bridge are **alternative transport mechanisms**, not evidence that both must be built now. Avoid speculative abstraction layers; retain existing adapter seams where adequate.
- Keep tenant/source authority independent of connection transport; local installation is not itself proof of tenant ownership. No unrestricted TenantContext creation, no trusting job payload tenant IDs.
- Connection configuration, credentials and secrets remain scoped to a verified source; protect them in storage and transit. Avoid leaking them to logs.
- The same Application/Domain and Worker binaries should be reusable in local and central hosting when transport is reachable. Actual central cutover may require local agent, secret provisioning, network access, PostgreSQL migration, backups and rollback.

## W4 next gate
Before implementation, inspect the actual three Asan interfaces and the WordPress connector, and document available methods, authentication, data model, network accessibility and deployment constraints. Separately design the customer installation/enrollment, trusted job authority, scheduling, service lifecycle, retry and negative trust tests. Do not invent API endpoints or implement a fake provider. W4 remains blocked for real delivery until evidence and architect approval.

## Context
See `ARCHITECTURE-W4-DUAL-DELIVERY-DECISION-2026-10-13.md` and `ARCHITECTURE-W4-DEPLOYMENT-DECISION-2026-10-13.md`. W1–W3 accepted; last accepted implementation `115e799`.

# ADR-002: Multi-Tenant Model

**Status:** Accepted
**Date:** 2026-10-07

## Context
FaraTaraz is sold to multiple independent companies. A Tenant (the purchasing
organization) owns many AccountingSources (accounting instances). Tenant isolation is
non-negotiable, and tenant authority must never come from untrusted input.

## Decision
- Model **`Tenant`** and **`AccountingSource`** as distinct, strongly-typed concepts.
- Every tenant-owned artifact references a **`TenantId`** (a distinct strong type, not a
  bare string).
- **`AccountingSource`** requires a **`TenantId`** as a mandatory constructor argument,
  enforcing ownership at the type level.
- Tenant context flows from a **trusted execution context**
  (`TenantContext.FromAuthenticatedPrincipal`), never from client/LLM/MCP input.
- Persistence and queries are always tenant-scoped (enforced in later layers).

## Consequences
- Cross-tenant isolation is enforced structurally, not just by policy.
- Tenant authority is explicit and auditable.
- Untrusted tenant context is rejected via `TenantContext.AssertTrusted()`.

## Rejected Alternatives
- **Client-supplied `tenantId` string:** rejected — enables cross-tenant access.
- **Static/global TenantId:** rejected — not thread- or request-safe, no trust boundary.

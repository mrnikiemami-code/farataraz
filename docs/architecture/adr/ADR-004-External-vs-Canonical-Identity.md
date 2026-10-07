# ADR-004: External vs Canonical Identity

**Status:** Accepted
**Date:** 2026-10-07

## Context
The same physical product/customer can have different codes across accounting sources
(Asan #1 `10025`, Asan #2 `K-882`, Sepidar `44501`). External codes are scoped to their
source and must not be treated as global identities. The first customer happens to use
identical codes across three Asan instances, but that is NOT a platform invariant.

## Decision
- **External identity** = `AccountingSourceId + ExternalCode` (strong types:
  `ExternalCustomerId`, `ExternalProductId`). It is source-scoped only.
- **Canonical identity** = FaraTaraz-owned, tenant-bound (`CanonicalCustomerId`,
  `CanonicalProduct`), referenced by a `CanonicalId`.
- Keep external and canonical models **distinct**; provider records are never mapped
  directly into canonical entities.
- No fuzzy/AI matching in foundational work. Equal names or equal codes across sources
  do not prove identity. Human review and remapping must be architecturally possible.

## Consequences
- Identity resolution is a dedicated, auditable stage in the pipeline.
- Remapping is possible without corrupting provenance.
- The "same code == same entity" shortcut is explicitly forbidden as a domain rule.

## Rejected Alternatives
- **Using external code as the global key:** rejected — codes collide across sources.
- **Encoding "same code across Asan == same entity":** rejected — that is a customer
  quirk, not a platform invariant.

# ADR-005: Synchronization and Idempotency Direction

**Status:** Accepted
**Date:** 2026-10-07

## Context
Synchronization brings source records into FaraTaraz. It must be robust: duplicate
delivery, retries, partial failure, provider timeouts, and changed/voided records are
expected. If the same source invoice arrives twice, FaraTaraz must be able to prove it
did not create duplicate sales.

## Decision
- Treat **synchronization as a first-class subsystem** with explicit seams:
  incremental/full sync, checkpoints/cursors, retries, partial-failure handling,
  duplicate detection, status, last attempt, last success, diagnostics.
- Design for **idempotent processing**: each source record carries enough identity
  (source + external id) to be de-duplicated.
- **Preserve source provenance** (`SourceProvenance`) for reconciliation, debugging,
  reprocessing, remapping, and auditing.
- **Raw provider payload retention is policy-gated** and documented; do not store
  sensitive payloads forever.
- **Sync semantics are explicit per provider**; assume full sync unless a provider
  explicitly supports incremental.

## Consequences
- Duplicate delivery is handled deterministically.
- Operators can observe sync freshness and diagnose failures.

## Rejected Alternatives
- **Ad-hoc sync helper methods:** rejected — sync is critical, not a utility.
- **Storing raw payloads unconditionally:** rejected — security/retention risk.

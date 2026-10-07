# ADR-006: Customer-Side Connector Boundary

**Status:** Proposed
**Date:** 2026-10-07

## Context
Some accounting APIs are reachable only inside a customer's LAN and have no public
internet exposure. FaraTaraz must still ingest from them without exposing those APIs.

## Decision
- Introduce a **customer-side connector agent** deployed inside the customer network:
  `Accounting System → Provider Adapter → FaraTaraz Connector Agent → outbound secure HTTPS → Cloud Ingestion → Platform Processing`.
- Communication is **outbound-only** from the customer environment; no inbound internet
  exposure of accounting APIs is required.
- **Accounting API credentials ideally remain inside the customer environment.**
- Document this boundary in W0; implement the transport in a later task.

## Consequences
- Works for air-gapped / LAN-only accounting systems.
- Keeps credentials close to the source.
- Adds a clear ingestion seam for later implementation.

## Rejected Alternatives
- **Inbound webhooks to accounting APIs:** rejected — many systems cannot be exposed.
- **Only cloud-hosted providers:** rejected — excludes a realistic deployment model.

# ADR-007: Shared Application Authority for REST and MCP

**Status:** Accepted
**Date:** 2026-10-07

## Context
FaraTaraz will expose behavior through both REST and MCP (and background sync). If each
surface computed its own answers, the dashboard and AI agents could produce conflicting
business results.

## Decision
- **Application use cases are the authoritative interface** to platform behavior.
- **REST, MCP, and background sync are thin adapters** that all delegate to the same
  Application capabilities:
  `REST / MCP / SyncWorker → Application → Domain/Analytics → Persistence`.
- **Forbidden:** `Dashboard → Database`, `MCP → Database`, `LLM → Database`,
  `Provider Adapter → Analytics`.
- **MCP has zero independent business calculation authority** — it only calls existing
  use cases and formats results.
- **The LLM never performs authoritative calculation** of demand, financial totals,
  stock, reorder quantities, or customer metrics; it explains deterministic results.

## Consequences
- One source of business truth across every surface.
- MCP remains a thin, safe consumer.
- Deterministic results are reproducible without an LLM.

## Rejected Alternatives
- **Per-surface business logic:** rejected — guarantees conflicting answers.
- **LLM as the calculator:** rejected — non-deterministic and unsafe for authoritative numbers.

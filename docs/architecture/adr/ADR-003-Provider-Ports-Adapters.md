# ADR-003: Accounting Provider Ports & Adapters

**Status:** Accepted
**Date:** 2026-10-07

## Context
FaraTaraz must connect to many accounting products (Asan, Sepidar, Holoo, Mahak, custom
ERPs). The core must not know what providers exist, and providers expose different
capabilities. A single `IAccountingSystem` interface would couple the core to every
provider and force fake empty methods.

## Decision
- Apply **Ports & Adapters / hexagonal** principles.
- Define **capability-oriented ports** (`ICustomerSource`, `IProductSource`,
  `ISalesSource`, `IInventorySource`, `IPurchaseSource`, `IPaymentSource`) that are
  independent and explicit.
- `IAccountingProvider` declares `SupportedCapabilities` (a `[Flags]` enum) and exposes
  only the ports it genuinely implements. `RequireCapability<TCapability>` throws
  `CapabilityNotSupportedException` for unsupported capabilities.
- All provider-specific DTOs, API clients, field names, auth, and mapping logic live
  **inside the adapter** (e.g. `FaraTaraz.Adapters.Accounting.Mock`).

## Consequences
- Adding a provider = adding a new adapter; core is unchanged.
- Unsupported capabilities are explicit failures, never empty collections.
- Core/BuildingBlocks contain zero provider-specific concepts.

## Rejected Alternatives
- **One `IAccountingSystem` port:** rejected — couples core to all providers and
  invites fake methods.
- **Provider DTOs in core:** rejected — violates provider independence.

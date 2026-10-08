# ADR-008: MediatR License Baseline

**Status:** Accepted
**Date:** 2026-10-08
**Supersedes:** —
**Depends on:** ADR-001 (Modular Monolith), ADR-007 (Shared Application Authority)

## Context

W1-R1 introduces CQRS through a mediator (`ISender` → application query → handler →
capability port). The only widely used mediator for .NET is MediatR. FaraTaraz is a
commercial, multi-tenant, provider-agnostic platform; its dependency license must be
permissive enough to not impose copyleft or a commercial agreement on FaraTaraz or its
customers.

The MediatR license baseline changed across versions, so the version must be chosen
authoritatively rather than assumed. License metadata was verified from the NuGet `.nuspec`
and the package `LICENSE.md`:

- **MediatR 12.5.0** — `<license>Apache-2.0</license>` (SPDX expression). Permissive.
- **MediatR.Contracts 2.0.1** — `<license>Apache-2.0</license>`. Permissive.
- **MediatR 13.0.0 / 14.2.0** (latest stable) — `<license type="file">LICENSE.md</license>`.
  The package `LICENSE.md` states that use is governed by the
  **Reciprocal Public License 1.5 (RPL1.5)**, a strong copyleft license, and offers an
  alternative commercial "License Agreement" at `https://luckypennysoftware.com/license`.
  RPL1.5 requires that derivative works release their source under RPL1.5, which is
  incompatible with FaraTaraz's commercial, closed-source customer deployments unless a
  commercial license is licensed.

## Decision

Pin the mediator to the last freely usable, Apache-2.0 baseline:

- **MediatR `12.5.0`** — Apache-2.0
- **MediatR.Contracts `2.0.1`** — Apache-2.0

Recorded in the relevant `.csproj` as explicit `<PackageReference>` pins.

Do **not** upgrade MediatR to 13.x or 14.x without a new explicit architecture and
licensing decision. A future upgrade path exists only if one of the following is true:

1. A release of MediatR is re-licensed under a permissive SPDX license (e.g., a return to
   Apache-2.0/MIT), **or**
2. FaraTaraz licenses the Lucky Penny Commercial License Agreement and the decision is
   recorded here with the commercial terms.

Silently pinning an older permissive version in place of an approved upgrade is not an
acceptable substitute for an explicit decision.

## Consequences

- The Application layer (`Ingestion.Application`) and the structural test project
  (`FaraTaraz.ArchitectureTests`) reference MediatR 12.5.0 / MediatR.Contracts 2.0.1.
- CQRS is delivered through `ISender` (not `IMediator`), so a future MediatR API change that
  moves delivery remains contained to the Application layer and its tests.
- No copyleft or commercial license obligation is introduced into FaraTaraz or its customers.

## Rejected Alternatives

- **MediatR 14.2.0 (latest stable, RPL1.5):** strong copyleft; requires source release of
  derivative works or a commercial license. Rejected for the commercial baseline.
- **MediatR 13.0.0 (RPL1.5):** same copyleft problem as 14.2.0. Rejected.
- **Hand-rolled mediator:** avoided; adds bespoke dispatch/pipeline code that the platform
  should not maintain. MediatR 12.5.0 satisfies the permissive requirement at zero license
  cost.

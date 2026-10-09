# Contributing to FaraTaraz

FaraTaraz is a **modular monolith** built to the **FaraTaraz Modular Clean Architecture
(FMCA)**. This document is the operating contract for contributors. It supplements
[`AGENTS.md`](./AGENTS.md) and is subordinate to the
[architecture constitution](./docs/architecture/architecture-constitution.md).

Read, in order, before changing anything:

1. [`AGENTS.md`](./AGENTS.md)
2. [`docs/architecture/architecture-constitution.md`](docs/architecture/architecture-constitution.md)
3. [`docs/planning/CURRENT-STATE.md`](docs/planning/CURRENT-STATE.md)
4. relevant ADRs (`docs/architecture/adr/`)
5. relevant roadmap wave (`docs/planning/ROADMAP.md`)

---

## Source-of-Truth hierarchy

Highest authority wins. If a lower-level instruction conflicts with a higher one, **stop**
and do not silently follow the lower-level instruction.

1. Architecture Constitution
2. Accepted ADRs
3. Current State
4. Roadmap / Delivery Plan
5. Current authorized task
6. Implementation

---

## Invariants you must preserve

These are enforced by automated architecture tests. Breaking one without an equivalent new
guard is a failed change.

- **Tenant boundary** — tenant authority comes only from a trusted execution context, never
  from client/LLM/MCP input. Every tenant-owned artifact is tenant-bound.
- **Provider independence** — no `Asan*` / `Sepidar*` / `Holoo*` / `Mahak*` types, DTOs, or
  field names in the platform modules or `BuildingBlocks`. Adding a provider is a new
  adapter, never a change to an existing module.
- **Source-scoped external identity** — `AccountingSourceId` + `ExternalCode`. Equal codes
  across sources do **not** prove equal canonical entities.
- **Canonical identity** — FaraTaraz-owned and tenant-bound.
- **Dependency direction** — never reverse the graph
  (`BuildingBlocks → (none)`, `Modules → BuildingBlocks`, `Adapters → Modules`, `Host →
  Modules`). The platform never references a concrete adapter.
- **Host zero authority** — no business handlers, domain rules, validators, repositories,
  provider implementations, or direct tenant-scoped operations in `Host` (or `Endpoints`).
  Only composition, configuration, startup, middleware and dispatch.
- **CQRS** — handlers are leaves: no `ISender`, no concrete adapter, only inward ports.
- **Source ownership + fail-closed authorization** — the ownership oracle is authoritative;
  source identity alone never grants authorization.
- **Sync idempotency** — duplicate source records must not create duplicate sales.

---

## Adding an architecture guard

Whenever you add or change anything that touches the invariants above, **add or update an
architecture test** in `tests/FaraTaraz.ArchitectureTests/`. If a rule cannot yet be
automated, document exactly why and when it must become executable — do not claim it is
guarded when it is not.

A guard must be a **negative test**: inject a temporary violation, confirm the guard fails
for the intended reason, then remove it. A guard that passes against its own violation is
useless.

---

## Build, test, commit

```bash
dotnet build FaraTaraz.sln            # Debug
dotnet test  FaraTaraz.sln
dotnet build FaraTaraz.sln -c Release # Release
```

Both configurations must build with **zero warnings and zero errors** and all tests must
pass before committing. `git diff --check` must be clean.

- One coherent change per commit. Do not mix unrelated experimentation.
- Do not implement business features (sales/inventory/customer intelligence, forecasting,
  dashboard, MCP tools, AI, real sync, provider integration) unless the active task says so.
- **No force-push.** Preserve history.
- If `origin` is not `https://github.com/mrnikiemami-code/farataraz`, stop and report before
  pushing.

---

## Certification governance (W1 / W1-R1 / W1-R2)

`PASS` means the acceptance criteria passed. It is **not** `CERTIFIED`. A wave becomes the
baseline only when an **external reviewer accepts the evidence**.

- Do not claim `CERTIFIED` for W1, W1-R1 or W1-R2 without reviewer acceptance.
- P0 does not authorize W1; a wave is started by an explicit task, not by P0 or by the
  previous wave completing.
- W1 does **not** authorize W2. W2 (PostgreSQL / EF Core) requires a separate explicit task
  and an explicit architecture decision to introduce a persistence stack.
- Keep `master` untouched until a migration branch is verified and explicitly approved for
  merge.

---

## CI

[`.github/workflows/architecture.yml`](.github/workflows/architecture.yml) runs on every
push and pull request to `master`. It builds Debug + Release (zero warnings/errors) and runs
all suites — the architecture suite fails on any invariant breach — plus a `git diff --check`
whitespace guard. The build is the guardrail; it must fail on drift.

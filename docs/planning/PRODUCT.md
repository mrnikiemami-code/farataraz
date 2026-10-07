# PRODUCT — FaraTaraz / فراتراز

**Status:** Approved (P0)
**Baseline:** `d499e61730579c2ad9810d306dd2620c986bd9f3` (W0 CERTIFIED)

This document is the product definition and source of truth for *what* FaraTaraz is,
*why*, and *what is deferred*. Architectural rules remain authoritative in
[`../architecture/architecture-constitution.md`](../architecture/architecture-constitution.md).
Where this document and the Constitution differ, the Constitution wins (see
[`../architecture/adr/ADR-007-Shared-Application-Authority.md`](../architecture/adr/ADR-007-Shared-Application-Authority.md)
and the Source-of-Truth hierarchy in `DELIVERY-PLAN.md`).

---

## Product

**FaraTaraz / فراتراز** is a commercial **multi-tenant, provider-agnostic Accounting
Intelligence Platform**.

It sits *above* accounting systems and converts operational accounting data into
**deterministic management intelligence**.

It is **NOT** intended to replace the customer's accounting software.

It connects to accounting systems through **replaceable provider adapters**.

FaraTaraz is **not** an AI dashboard for Asan Accounting. Accounting systems are
replaceable adapters; source identities are explicitly mapped to FaraTaraz-owned
canonical identities; deterministic application capabilities own business truth; AI is a
controlled consumer of those capabilities.

---

## Target customers

Independent companies / organizations that already operate accounting software and need:

- consolidated visibility
- sales intelligence
- inventory intelligence
- purchasing / replenishment recommendations
- customer intelligence
- forecasting
- management dashboards
- natural-language access to trusted business intelligence

**Tenant** = the purchasing organization.

**One Tenant may have multiple AccountingSources** (accounting instances).

---

## First real deployment

The first known deployment has:

- one Tenant
- three independent **Asan 6** accounting-system instances
- the same customer codes across those three instances
- the same product codes across those three instances

**IMPORTANT:** Equal codes across these three systems are a property of this *first
deployment only*. They **MUST NOT** become a platform invariant. Equal external codes do
**not** prove equal canonical entities (see ADR-004).

---

## Known Asan API capabilities

Documented only what is currently known (customer / documentation report):

- retrieve product price list
- retrieve inventory / product statistics
- retrieve defined products
- retrieve defined persons / customers
- retrieve a person's financial ledger
- create new persons / customers
- submit sales proforma invoices
- submit final sales invoices
- submit receipt / bank-transfer documents

### CRITICAL — unverified read capabilities

We have **NOT** yet proven that the Asan API can read:

- historical sales invoices
- historical invoice lines
- historical purchasing documents
- all fields needed for analytics

**Do NOT assume these capabilities.** Mark them:

> `UNVERIFIED_PROVIDER_CAPABILITY`

This becomes a **formal discovery gate** before any real Asan analytics integration. See
`DECISIONS-AND-OPEN-QUESTIONS.md` (OQ-001, OQ-002).

---

## Product outcomes (MVP questions)

### Sales

- Best-selling products?
- Which products are slowing down?
- How have sales changed over time?
- How do accounting sources / branches compare?

### Inventory

- Fast-moving products?
- Slow-moving products?
- Inventory becoming dead stock?
- Items that may stock out soon?

### Purchasing / Replenishment

- What should we buy?
- How much should we buy?
- Stock required for the next 30 / 45 / 60 days?
- Suggested reorder quantity?

### Customer Intelligence

- Which customers have stopped buying?
- Which customers are at risk?
- Which high-value customers need follow-up?
- What does each customer usually buy?
- What is each customer's purchase cycle?

### Forecasting

- Forecast demand per product.
- Estimate future stock requirements.
- Produce reproducible replenishment recommendations.

### AI Assistant

Persian management questions such as:

> برای ۴۵ روز آینده از هر کالا چندتا باید بخرم؟

> کدام مشتری‌های مهم مدتی است خرید نکرده‌اند؟

> فروش سه مجموعه را با هم مقایسه کن.

The AI is **NOT** the calculation authority. It consumes deterministic application
capabilities.

---

## MVP definition

### Foundation

- multi-tenancy
- accounting-source management
- provider adapter architecture
- secure source configuration
- synchronization
- source provenance
- canonical master data
- identity mapping

### Data

- customers
- products
- sales / invoices, **IF** provider supports read access
- inventory
- necessary historical data

### Intelligence

- sales metrics
- product velocity
- slow / fast-moving detection
- customer inactivity / risk
- basic deterministic forecasting
- replenishment recommendations

### Product surface

- authenticated management API
- management dashboard
- sync health / freshness visibility
- Persian AI assistant
- controlled MCP tools

### Operations

- deployable production configuration
- tenant isolation
- auditability
- observability
- backup / recovery documentation
- customer-side connector where required

---

## Explicit non-goals (initial MVP)

Unless later approved:

- replacing accounting software
- full ERP
- payroll
- tax filing
- accounting journal authoring
- arbitrary autonomous writes into accounting systems
- microservices
- Kubernetes
- Kafka
- Event Sourcing
- vector database
- model fine-tuning
- advanced deep-learning forecasting
- autonomous financial decisions
- AI-controlled canonical identity merging

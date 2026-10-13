# W4 activation and trusted installation authority — architect decision

**Date:** 2026-10-13
**Status:** APPROVED PRODUCT/SECURITY DIRECTION; implementation still subject to scoped design and tests
**Decision source:** User approved company-installed customer-local Worker, online one-time activation code, signed installation grant, and 30-day offline operating window.

## First-release operating model

1. The FaraTaraz team installs a Windows Worker on the customer's PC.
2. The company provisions a customer/tenant and allowed accounting sources in a trusted administration process, issues a short-lived, single-use activation code, rate-limits attempts and records activation audit events.
3. The installer generates a per-installation asymmetric key pair; the private key remains non-exportable when supported (prefer Windows CNG/TPM) or protected with appropriate OS facilities. The activation request sends the one-time code and installation public key over authenticated TLS to a company-controlled activation service.
4. The activation service authenticates the code, binds it to the provisioned tenant, installation identity, permitted sources/capabilities, validity period, and public-key thumbprint, and signs the grant with a company-held private signing key. The client only has the corresponding pinned/trusted verification public key.
5. Before each Worker job, a narrow verified-authority boundary validates grant signature, scope, expiry, installation key possession, revocation freshness policy and source binding. It establishes a trusted tenant context through a restricted internal path (ADR-011), never through a public unrestricted factory, raw configuration ID or job payload. The existing Application admission independently checks ownership, active status and provider configuration before provider calls/writes.
6. Online renewal refreshes the time-limited grant; the approved **initial policy** is up to **30 days of offline operation** from the last valid renewal. Offline revocation cannot be immediate; this risk must be explicit. On expiry, fail closed for new sync jobs, retain local data safely, and allow controlled renewal.
7. Central deployment later re-enrolls the central runtime with new installation identity/grant and migrates credentials/data/checkpoints through a controlled audited cutover. Do not transfer the customer's private key as a migration shortcut. The same Domain/Application/Worker code is reused where provider connectivity allows.

## Distinct authorization layers

- Activation/license authorizes **an installation** to run within stated policy; it is not, alone, proof of arbitrary tenant or accounting-source access.
- The authoritative provisioning system binds installation identity to tenant/source/capability scope; verifier authenticates this scoped grant. Existing Application ownership admission remains mandatory and must fail closed for disabled sources.
- No company signing private key, plaintext activation code or provider secret in repository, installer, logs, or ordinary appsettings.
- Worker/service account is an OS runtime identity, not a tenant principal.
- Do not conflate software-license expiry with deletion or loss of customer records; expiry blocks new sync only.

## Architecture constraints

- No licensing logic in Domain ingestion policy; licensing/authority remains in security/delivery infrastructure with an explicit restricted BuildingBlocks trust-establishment seam.
- Worker and future authenticated API use different trusted-principal producers but invoke the same Application/MediatR handlers.
- Host remains composition-only. No DbContext in delivery. No mock adapter in production. No vendor protocol guessed from reported WordPress integration.
- Avoid unnecessary new modules/assemblies or a large licensing administration panel for V1. Small company-operated issuance workflow and activation endpoint suffice when their deployment/security ownership is explicit.
- Time-limited grants must define trusted clock and clock-rollback handling; never blindly accept a locally edited system clock. Consider monotonic last-seen timestamp and renewal audit with realistic offline limitations.
- Signing-key rotation, lost-device replacement, revocation, reinstall/recovery, tenant transfer, backup, anti-replay and service outage handling require documented bounded acceptance tests before shipping.
- A 30-day offline allowance is a maximum continuity window, not an online check guarantee or immediate-revocation guarantee.

## Gate for W4 implementation

The *business model* of the issuer is now chosen: **the FaraTaraz company operates the enrollment and grant-signing authority**. Still unknown are concrete hosting/operations, protocol and key storage, the first provider's documented Asan interface, and precise DB/service installation decisions. Begin with a bounded architecture design of the signed grant format, trust producer placement and threat/negative test plan; then authorize small independent implementation slices. No production-ready claim until end-to-end tests with a real authority, real provider and PostgreSQL.

## Continuity

W1–W3 accepted; W4 Worker design accepted with corrections; ADR-011 Proposed pending real producer verification. Related decisions: ARCHITECTURE-W4-DUAL-DELIVERY-DECISION-2026-10-13.md; ARCHITECTURE-W4-DEPLOYMENT-DECISION-2026-10-13.md; ARCHITECTURE-W4-CONNECTIVITY-DECISION-2026-10-13.md; ARCHITECTURE-W4-WORKER-TRUST-DESIGN-2026-10-13.md.

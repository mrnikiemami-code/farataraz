namespace FaraTaraz.Modules.Ingestion.Domain.SourceModel;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.MasterData;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;

/// <summary>
/// Provider-agnostic representation of a customer as seen from one accounting source.
///
/// This is the "External/Source Representation" stage of the pipeline. It is deliberately
/// NOT provider-specific (no Asan/Sepidar field names) and NOT the canonical model.
/// Provider adapters map provider DTOs (e.g. AsanCustomer) into this neutral shape.
///
/// <see cref="RecordId"/> is the deterministic sync/idempotency identity;
/// <see cref="ExternalId"/> is the source-scoped external identity used later by canonical
/// mapping. Both are carried explicitly because they serve different layers.
/// </summary>
public sealed record SourceCustomer(
    SourceRecordId RecordId,
    ExternalCustomerId ExternalId,
    string Code,
    string Name,
    SourceRecordVersion? Version = null);

/// <summary>
/// Provider-agnostic representation of a product as seen from one accounting source.
/// See <see cref="SourceCustomer"/> for the provenance/identity contract.
/// </summary>
public sealed record SourceProduct(
    SourceRecordId RecordId,
    ExternalProductId ExternalId,
    string Code,
    string Name,
    SourceRecordVersion? Version = null);

/// <summary>
/// Source representation for the Sales capability. Business shape is defined in the Sales
/// wave; the sync identity + version contract is established here.
/// </summary>
public sealed record SourceSalesRecord(
    SourceRecordId RecordId,
    SourceRecordVersion? Version = null);

/// <summary>
/// Source representation for the Inventory capability. Business shape is defined in the
/// Inventory wave; the sync identity + version contract is established here.
/// </summary>
public sealed record SourceInventoryRecord(
    SourceRecordId RecordId,
    SourceRecordVersion? Version = null);

/// <summary>
/// Source representation for the Purchases capability. Business shape is defined in the
/// Purchasing wave; the sync identity + version contract is established here.
/// </summary>
public sealed record SourcePurchaseRecord(
    SourceRecordId RecordId,
    SourceRecordVersion? Version = null);

/// <summary>
/// Source representation for the Payments capability. Business shape is defined in its
/// wave; the sync identity + version contract is established here.
/// </summary>
public sealed record SourcePaymentRecord(
    SourceRecordId RecordId,
    SourceRecordVersion? Version = null);

/// <summary>
/// Immutable record of where a source fact originated, captured at the ingestion boundary.
///
/// Preserves source-side identity plus ingestion metadata so the platform can:
/// reconcile, debug, reprocess, remap identities, and audit.
///
/// Two distinct timestamps are kept on purpose:
/// - <see cref="RetrievedAtUtc"/> is the PLATFORM-observed retrieval time (from the clock).
/// - <see cref="ProviderModifiedAtUtc"/> is the PROVIDER-reported modification time.
/// They are not the same and must not be conflated.
///
/// Retention of raw provider payloads is a separate, policy-gated concern (see
/// architecture constitution). Provenance itself is lightweight and always kept.
/// </summary>
public sealed record SourceProvenance(
    SourceRecordId RecordId,
    FaraTaraz.BuildingBlocks.Accounting.ProviderId Provider,
    AccountingSourceId SourceId,
    DateTime RetrievedAtUtc,
    string? Checkpoint = null,
    DateTime? ProviderModifiedAtUtc = null);

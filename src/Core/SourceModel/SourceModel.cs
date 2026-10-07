namespace FaraTaraz.Core.SourceModel;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Core.MasterData;

/// <summary>
/// Provider-agnostic representation of a customer as seen from one accounting source.
///
/// This is the "External/Source Representation" stage of the pipeline. It is deliberately
/// NOT provider-specific (no Asan/Sepidar field names) and NOT the canonical model.
/// Provider adapters map provider DTOs (e.g. AsanCustomer) into this neutral shape.
/// </summary>
public sealed record SourceCustomer(
    ExternalCustomerId Id,
    string Code,
    string Name);

/// <summary>
/// Provider-agnostic representation of a product as seen from one accounting source.
/// See <see cref="SourceCustomer"/> for the provenance contract.
/// </summary>
public sealed record SourceProduct(
    ExternalProductId Id,
    string Code,
    string Name);

/// <summary>
/// Immutable record of where a source fact originated.
///
/// Preserves source-side identity plus ingestion metadata so the platform can:
/// reconcile, debug, reprocess, remap identities, and audit.
///
/// Retention of raw provider payloads is a separate, policy-gated concern (see
/// architecture constitution). Provenance itself is lightweight and always kept.
/// </summary>
public sealed record SourceProvenance(
    AccountingSourceId SourceId,
    string ExternalId,
    System.DateTime IngestedAtUtc,
    string? Checkpoint);

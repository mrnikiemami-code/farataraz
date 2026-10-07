namespace FaraTaraz.Adapters.Accounting.Mock;

using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Core.MasterData;
using FaraTaraz.Core.SourceModel;
using FaraTaraz.Core.Synchronization;

/// <summary>
/// Deterministic fixture builders for Mock synchronization scenarios.
///
/// Every source record gets a deterministic <see cref="SourceRecordId"/> and a
/// deterministic content fingerprint (via <see cref="SourceContentFingerprint"/>).
/// Therefore:
/// - identical content yields identical identity AND fingerprint (duplicates),
/// - differing content yields a differing fingerprint while keeping the identity
///   (changed record).
///
/// No random data is used anywhere in the Mock.
/// </summary>
public static class MockSources
{
    public static SourceCustomer Customer(
        AccountingSourceId sourceId,
        string code,
        string? name = null)
    {
        var recordId = new SourceRecordId(sourceId, "Customer", code);
        var externalId = new ExternalCustomerId(sourceId, code);
        var resolvedName = name ?? $"Customer {code}";
        var version = new SourceRecordVersion
        {
            ContentFingerprint = SourceContentFingerprint.Compute(new { code, name = resolvedName })
        };

        return new SourceCustomer(recordId, externalId, code, resolvedName, version);
    }

    public static SourceProduct Product(
        AccountingSourceId sourceId,
        string code,
        string? name = null)
    {
        var recordId = new SourceRecordId(sourceId, "Product", code);
        var externalId = new ExternalProductId(sourceId, code);
        var resolvedName = name ?? $"Product {code}";
        var version = new SourceRecordVersion
        {
            ContentFingerprint = SourceContentFingerprint.Compute(new { code, name = resolvedName })
        };

        return new SourceProduct(recordId, externalId, code, resolvedName, version);
    }
}

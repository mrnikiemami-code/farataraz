namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// An external customer identity is SCOPED TO its accounting source.
///
/// <c>AccountingSourceId + ExternalCode</c> identifies a source-side identity.
/// The same external code in two sources are DIFFERENT identities, even if the
/// code text is identical. Equal codes do NOT prove equal canonical entities.
/// </summary>
public sealed record ExternalCustomerId(AccountingSourceId SourceId, string ExternalCode)
{
    public override string ToString() => $"{SourceId}![{ExternalCode}]";
}

/// <summary>
/// An external product identity is SCOPED TO its accounting source.
/// See <see cref="ExternalCustomerId"/> for the provenance contract.
/// </summary>
public sealed record ExternalProductId(AccountingSourceId SourceId, string ExternalCode)
{
    public override string ToString() => $"{SourceId}![{ExternalCode}]";
}

/// <summary>
/// Platform-owned internal identity of a canonical entity.
/// Owned by FaraTaraz; never derived from provider code.
/// </summary>
public readonly record struct CanonicalId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// A canonical customer — owned by FaraTaraz and BOUND to a tenant.
/// One canonical customer may be linked to several external identities across sources.
/// </summary>
public sealed record CanonicalCustomer(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

/// <summary>
/// A canonical product — owned by FaraTaraz and BOUND to a tenant.
/// See <see cref="CanonicalCustomer"/> for the provenance contract.
/// </summary>
public sealed record CanonicalProduct(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

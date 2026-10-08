namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// A canonical product — owned by FaraTaraz and BOUND to a tenant.
/// See <see cref="CanonicalCustomer"/> for the provenance contract.
/// </summary>
public sealed record CanonicalProduct(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

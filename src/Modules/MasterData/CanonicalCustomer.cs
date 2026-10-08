namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// A canonical customer — owned by FaraTaraz and BOUND to a tenant.
/// One canonical customer may be linked to several external identities across sources.
/// </summary>
public sealed record CanonicalCustomer(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

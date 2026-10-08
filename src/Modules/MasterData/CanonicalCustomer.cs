namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>Tenant-bound canonical customer, independent of source customer codes.</summary>
public sealed record CanonicalCustomer(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

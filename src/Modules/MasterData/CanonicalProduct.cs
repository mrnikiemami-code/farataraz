namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>Tenant-bound canonical product, independent of source product codes.</summary>
public sealed record CanonicalProduct(TenantId TenantId, CanonicalId Value)
{
    public override string ToString() => $"tenant({TenantId})#canon({Value})";
}

namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Platform-owned internal identity of a canonical entity.
/// Owned by FaraTaraz; never derived from provider code.
/// </summary>
public readonly record struct CanonicalId(string Value)
{
    public override string ToString() => Value;
}

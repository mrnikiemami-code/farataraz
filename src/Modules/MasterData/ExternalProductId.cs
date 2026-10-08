namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// An external product identity is SCOPED TO its accounting source.
/// See <see cref="ExternalCustomerId"/> for the provenance contract.
/// </summary>
public sealed record ExternalProductId(AccountingSourceId SourceId, string ExternalCode)
{
    public override string ToString() => $"{SourceId}![{ExternalCode}]";
}

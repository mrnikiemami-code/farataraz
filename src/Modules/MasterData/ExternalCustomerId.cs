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

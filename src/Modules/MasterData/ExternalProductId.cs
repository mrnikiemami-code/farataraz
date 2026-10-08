namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>Source-scoped external product identity; codes from distinct sources are not interchangeable.</summary>
public sealed record ExternalProductId
{
    public AccountingSourceId SourceId { get; }
    public string ExternalCode { get; }

    public ExternalProductId(AccountingSourceId sourceId, string externalCode)
    {
        if (string.IsNullOrWhiteSpace(sourceId.Value)) throw new ArgumentException("A source identity is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(externalCode)) throw new ArgumentException("An external product code is required.", nameof(externalCode));
        SourceId = sourceId;
        ExternalCode = externalCode;
    }

    public override string ToString() => $"{SourceId}![{ExternalCode}]";
}

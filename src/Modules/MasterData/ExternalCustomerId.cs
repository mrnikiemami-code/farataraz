namespace FaraTaraz.Modules.MasterData;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>Source-scoped external customer identity. Equal codes across sources do not establish canonical equality.</summary>
public sealed record ExternalCustomerId
{
    public AccountingSourceId SourceId { get; }
    public string ExternalCode { get; }

    public ExternalCustomerId(AccountingSourceId sourceId, string externalCode)
    {
        if (string.IsNullOrWhiteSpace(sourceId.Value)) throw new ArgumentException("A source identity is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(externalCode)) throw new ArgumentException("An external customer code is required.", nameof(externalCode));
        SourceId = sourceId;
        ExternalCode = externalCode;
    }

    public override string ToString() => $"{SourceId}![{ExternalCode}]";
}

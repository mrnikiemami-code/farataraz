namespace FaraTaraz.Modules.AccountingSources.Authorization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Thrown when a trusted Tenant fails to own the requested AccountingSource. The Application
/// use case throws this after <see cref="IAccountingSourceOwnership.IsOwnedByAsync"/> returns
/// <c>false</c>, so no provider work proceeds for a source the Tenant does not own.
/// </summary>
public sealed class UnauthorizedSourceException : Exception
{
    public AccountingSourceId SourceId { get; }

    public UnauthorizedSourceException(AccountingSourceId sourceId, string message)
        : base(message)
    {
        SourceId = sourceId;
    }
}

namespace FaraTaraz.Modules.AccountingSources.Authorization;

using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Thrown when a trusted Tenant owns the requested AccountingSource but that source is
/// administratively <c>Disabled</c>. The Application use case throws this after
/// <see cref="IAccountingSourceActivation.IsActiveAsync"/> returns <c>false</c>, so no provider
/// work and no ingestion write proceeds for a disabled source.
///
/// This is distinct from <see cref="UnauthorizedSourceException"/> on purpose: a disabled source
/// is <b>owned</b>, so reporting it as "unauthorized" would be misleading.
/// </summary>
public sealed class AccountingSourceDisabledException : Exception
{
    public AccountingSourceId SourceId { get; }

    public AccountingSourceDisabledException(AccountingSourceId sourceId)
        : base($"The accounting source '{sourceId}' is disabled; synchronization is refused until it is enabled.")
    {
        SourceId = sourceId;
    }
}

namespace FaraTaraz.Modules.AccountingSources;

using FaraTaraz.BuildingBlocks.Identifiers;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provider-independent authorization port that proves a trusted Tenant owns an
/// <see cref="BuildingBlocks.Tenancy.AccountingSource"/> before any tenant-scoped provider
/// work is performed.
///
/// This is NOT a capability port and NOT a data store. It is a minimal authorization oracle
/// that the orchestration/Application layer consults as part of the trusted execution
/// boundary. It is deliberately provider-independent: no provider adapter or concrete
/// implementation leaks into the platform here.
///
/// <b>Fail-closed contract.</b> <see cref="IsOwnedByAsync"/> returns <c>false</c> — never
/// throws for authorization purposes — when the relationship cannot be confirmed: unknown
/// tenant, unknown source, or a lookup failure. Any failure is treated as <em>not owned</em>,
/// never as authorization success. Source identity alone never grants authorization; ownership
/// is only ever established by a trusted execution context consulting this port.
/// </summary>
public interface IAccountingSourceOwnership
{
    /// <summary>
    /// Returns <c>true</c> only when the trusted <paramref name="tenantId"/> owns
    /// <paramref name="sourceId"/>.
    ///
    /// Returns <c>false</c> on unknown tenant, unknown source, or lookup failure (fail-closed).
    /// Propagates <paramref name="cancellationToken"/>; a caller cancellation surfaces as
    /// <c>OperationCanceledException</c> and is never classified as authorization.
    /// </summary>
    Task<bool> IsOwnedByAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default);
}

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

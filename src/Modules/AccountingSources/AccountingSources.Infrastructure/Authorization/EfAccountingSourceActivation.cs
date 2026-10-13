namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Database-backed implementation of the provider-independent source-activation oracle
/// (<see cref="IAccountingSourceActivation"/>).
///
/// <b>Trusted tenant only.</b> Tenant authority comes only from the trusted execution context
/// (Constitution A.4/A.5). Like the ownership oracle it uses ONLY the trusted
/// <see cref="DatabaseTenantScope"/> bound to the current unit of work and ignores the
/// caller-supplied tenant id, so an arbitrary caller can never activate a source for a tenant it
/// does not control.
///
/// <b>Only <c>Disabled</c> blocks.</b> <see cref="AccountingSourceStatus.Syncing"/> and
/// <see cref="AccountingSourceStatus.Error"/> are transient runtime states, not administrative
/// refusals; they must not block the next bounded step, so they are treated as active.
///
/// <b>Errors are not mislabeled.</b> A missing row or an unbound trusted scope returns
/// <c>false</c> (fail-closed). A genuine persistence failure is deliberately NOT caught and
/// re-classified as "disabled": it surfaces as its own error, and cancellation propagates as
/// <c>OperationCanceledException</c>. The Application admission verifies ownership first, so a
/// <c>false</c> result is only reached for an owned source.
/// </summary>
public sealed class EfAccountingSourceActivation : IAccountingSourceActivation
{
    private readonly AccountingSourcesDbContext _db;
    private readonly DatabaseTenantScope _scope;

    /// <summary>Wraps the module unit of work and the trusted tenant scope.</summary>
    public EfAccountingSourceActivation(AccountingSourcesDbContext db, DatabaseTenantScope scope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <inheritdoc />
    public async Task<bool> IsActiveAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        // Ignore the caller-supplied tenantId. Only the trusted scope is authoritative.
        if (!_scope.IsTrusted || _scope.TenantId is not { } trusted)
        {
            return false;
        }

        var status = await _db.AccountingSources
            .Where(e => e.Id == sourceId.Value && e.TenantId == trusted.Value)
            .Select(e => (int?)e.Status)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return status is not null && status.Value != (int)AccountingSourceStatus.Disabled;
    }
}

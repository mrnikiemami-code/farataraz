namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Database-backed provider resolver over the persisted <c>AccountingSource.Provider</c>
/// configuration (task M5).
///
/// <b>Fail-closed and trusted-tenant-only.</b> It uses ONLY the trusted
/// <see cref="DatabaseTenantScope"/> bound to the current unit of work; it ignores any
/// caller-supplied tenant id, so an arbitrary caller can never resolve a provider for a source it
/// does not own. Unknown/cross-tenant source, absent scope, or blank provider configuration all
/// return <c>null</c>.
/// </summary>
public sealed class EfAccountingSourceProviderResolver : IAccountingSourceProviderResolver
{
    private readonly AccountingSourcesDbContext _db;
    private readonly DatabaseTenantScope _scope;

    /// <summary>Wraps the module unit of work and the trusted tenant scope.</summary>
    public EfAccountingSourceProviderResolver(AccountingSourcesDbContext db, DatabaseTenantScope scope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <inheritdoc />
    public async Task<ProviderId?> ResolveProviderAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        // Ignore the caller-supplied tenant id; only the trusted scope is authoritative.
        if (!_scope.IsTrusted || _scope.TenantId is not { } trusted)
        {
            return null;
        }

        try
        {
            var provider = await _db.AccountingSources
                .Where(e => e.Id == sourceId.Value && e.TenantId == trusted.Value)
                .Select(e => e.Provider)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(provider) ? null : new ProviderId(provider);
        }
        catch
        {
            // Lookup failure -> fail closed (no provider), never a fabricated identity.
            return null;
        }
    }
}

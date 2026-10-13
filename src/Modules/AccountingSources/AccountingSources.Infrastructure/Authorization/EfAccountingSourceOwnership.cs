namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Database-backed implementation of the provider-independent ownership oracle
/// (<see cref="IAccountingSourceOwnership"/>.
///
/// <b>Fail-closed contract.</b> <see cref="IsOwnedByAsync"/> returns <c>false</c> — never
/// authorizes — for an unknown tenant, unknown source, or a lookup failure. Caller cancellation
/// is NOT a lookup failure: <see cref="OperationCanceledException"/> propagates and is never
/// converted into an authorization result (Constitution A.4/A.5; the caller's cancellation must
/// not be swallowed as "not owned").
///
/// <b>Trusted tenant only.</b> Tenant authority comes only from the trusted execution context
/// (Constitution A.4/A.5). This oracle uses ONLY the trusted <see cref="DatabaseTenantScope"/>
/// bound to the current unit of work; it ignores any <c>tenantId</c> supplied by a
/// caller, so an arbitrary tenant id can never grant authorization. Source identity alone
/// never grants authorization.
/// </summary>
public sealed class EfAccountingSourceOwnership : IAccountingSourceOwnership
{
    private readonly AccountingSourcesDbContext _db;
    private readonly DatabaseTenantScope _scope;

    /// <summary>
    /// Wraps the module unit of work and the trusted tenant scope for the ownership oracle.
    /// </summary>
    public EfAccountingSourceOwnership(AccountingSourcesDbContext db, DatabaseTenantScope scope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    /// <summary>
    /// Returns <c>false</c> (never authorizes) for an unknown tenant, unknown source, or a
    /// lookup failure; <c>true</c> only when the trusted tenant owns the source.
    ///
    /// Cancellation is a caller action, not an authorization outcome: an
    /// <see cref="OperationCanceledException"/> propagates and is never returned as
    /// <c>false</c>.
    /// </summary>
    public async Task<bool> IsOwnedByAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default)
    {
        // Ignore the caller-supplied tenantId. Only the trusted scope is authoritative.
        if (!_scope.IsTrusted)
        {
            // No trusted context bound -> fail-closed (not owned), never authorization success.
            return false;
        }

        // IsTrusted is guaranteed true by the guard above, so a tenant is bound.
        var boundTenantId = _scope.TenantId;
        if (boundTenantId is null)
        {
            // Defensive fail-closed: a trusted scope must carry a tenant.
            return false;
        }

        var trustedTenantId = boundTenantId.Value;

        try
        {
            // WHY this shape (not AnyAsync): the contract is that caller cancellation propagates.
            // Empirically, EF Core's server-side EXISTS (`AnyAsync(predicate, ct)`) does not surface
            // an already-cancelled token on this path, so the documented cancellation contract was
            // unreachable and untestable. A scoped existence projection through
            // `FirstOrDefaultAsync(ct)` (the same pattern the provider resolver already uses) is
            // behaviour-identical for authorization — it returns whether the trusted tenant owns
            // the source — and it genuinely observes and propagates cancellation.
            var owner = await _db.AccountingSources
                .Where(e => e.Id == sourceId.Value && e.TenantId == trustedTenantId.Value)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return owner is not null;
        }
        catch (OperationCanceledException)
        {
            // Caller cancellation must propagate; it is never classified as "not owned".
            throw;
        }
        catch
        {
            // Lookup failure -> fail-closed (not owned), never authorization success.
            return false;
        }
    }
}

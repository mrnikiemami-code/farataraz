namespace FaraTaraz.Modules.AccountingSources.Authorization;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Provider-independent authorization port that proves an AccountingSource the trusted Tenant
/// owns is currently <b>active</b> (not administratively disabled) before any provider work or
/// write is performed.
///
/// This is deliberately a <b>separate</b> contract from <see cref="IAccountingSourceOwnership"/>:
/// ownership answers "does the trusted tenant own this source?", activation answers "is the owned
/// source currently usable?". Folding the two together would make the ownership oracle's name
/// lie and would conflate a foreign/unknown source with a disabled one.
///
/// <b>Fail-closed contract.</b> <see cref="IsActiveAsync"/> returns <c>false</c> when the source
/// is not active for the trusted tenant, or when no trusted scope is bound. It never authorizes
/// on failure. The Application admission checks ownership <b>first</b>, so by the time activation
/// is consulted the source is already known to be owned; a <c>false</c> result therefore means
/// "owned but not usable" and is reported as a disabled-source failure.
///
/// Cancellation propagates as <c>OperationCanceledException</c> and is never classified as a
/// disabled source. A genuine persistence failure is not silently re-classified as "disabled" —
/// it surfaces as its own error (the implementation does not swallow infrastructure errors).
/// </summary>
public interface IAccountingSourceActivation
{
    /// <summary>
    /// Returns <c>true</c> only when the trusted tenant's <paramref name="sourceId"/> is active
    /// (its operational status is not <c>Disabled</c>).
    ///
    /// Returns <c>false</c> when the source is disabled or no trusted scope is bound (fail-closed).
    /// The caller-supplied <paramref name="tenantId"/> is never an authorization authority; the
    /// trusted execution-context scope is.
    /// </summary>
    Task<bool> IsActiveAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default);
}

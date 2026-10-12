namespace FaraTaraz.Modules.AccountingSources.Authorization;

using System.Threading;
using System.Threading.Tasks;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Identifiers;

/// <summary>
/// Provider-independent read port that resolves the <b>authoritative</b> provider identity of an
/// accounting source from its persisted configuration (task M5).
///
/// Callers must NOT supply the provider id themselves: provenance must reflect the source's
/// configured provider, never a value carried by the request. The implementation is tenant-scoped
/// and fail-closed — an unknown/cross-tenant source, a missing trusted tenant scope, or a blank
/// provider configuration resolves to <c>null</c>, which the orchestration treats as "not usable".
/// </summary>
public interface IAccountingSourceProviderResolver
{
    /// <summary>
    /// Resolves the configured provider of the source for the trusted tenant, or <c>null</c> when it
    /// cannot be trusted (unknown source, not owned, missing scope, or blank configuration).
    /// </summary>
    Task<ProviderId?> ResolveProviderAsync(
        TenantId tenantId,
        AccountingSourceId sourceId,
        CancellationToken cancellationToken = default);
}

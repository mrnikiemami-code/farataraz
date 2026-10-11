namespace FaraTaraz.Modules.AccountingSources.Composition;

using FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Module-owned composition seam for the AccountingSources module.
///
/// This is the single entry point the Host calls to compose the module: it binds the module's
/// inward ownership port (<c>IAccountingSourceOwnership</c>) and persistence to their
/// Infrastructure implementations. It contains NO business rules and takes NO tenant decision
/// — it only wires the module inward (ADR-010 decision 14).
///
/// The Host references this seam instead of the concrete
/// <c>AccountingSources.Infrastructure</c> assembly, so the composition root wires persistence
/// without owning it (D-1; AGENTS.md). A missing/blank connection string fails closed through
/// the Infrastructure composition, before any <c>DbContext</c> is registered.
/// </summary>
public static class AccountingSourcesModuleComposition
{
    /// <summary>
    /// Composes the AccountingSources module: the fail-closed ownership oracle and the module's
    /// persistence over the supplied connection string.
    /// </summary>
    public static IServiceCollection AddAccountingSourcesModule(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAccountingSourcesInfrastructure(connectionString);

        return services;
    }
}

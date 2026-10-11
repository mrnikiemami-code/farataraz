namespace FaraTaraz.Host.Composition;

using FaraTaraz.Modules.AccountingSources.Composition;
using FaraTaraz.Modules.Ingestion.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Composition root for the FaraTaraz host.
///
/// This type has ZERO business authority. It only wires modules together through their
/// module-owned <c>Composition</c> seams. It implements no use case, owns no domain rule,
/// references no provider adapter and no concrete persistence assembly. Business authority
/// remains in the Application layer (see ADR-009 and <c>structure.md</c> §10.3): the host
/// composes modules, never behaviors.
///
/// The host references each module's composition seam — not the module's Infrastructure — so a
/// runnable production graph is composed without the host owning persistence (D-1; ADR-010
/// decision 14). A null/blank connection string fails closed inside the module Infrastructure
/// composition before any <c>DbContext</c> is registered.
/// </summary>
public static class FaraTarazHost
{
    /// <summary>
    /// Composes the FaraTaraz modules over the supplied connection string. Each module
    /// registers itself through its own composition seam; the host only aggregates module
    /// composition and never chooses or implements business behavior.
    /// </summary>
    public static IServiceCollection ConfigureFaraTaraz(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Modules compose themselves. The host never implements or selects business behavior.
        services.AddIngestionModule(connectionString);
        services.AddAccountingSourcesModule(connectionString);

        return services;
    }
}

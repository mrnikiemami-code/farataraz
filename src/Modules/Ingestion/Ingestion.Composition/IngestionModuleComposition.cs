namespace FaraTaraz.Modules.Ingestion.Composition;

using FaraTaraz.Modules.Ingestion.Application.Composition;
using FaraTaraz.Modules.Ingestion.Infrastructure.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Module-owned composition seam for the Ingestion module.
///
/// This is the single entry point the Host calls to compose the module: it registers the
/// module's Application use cases and its Infrastructure persistence in one place. It contains
/// NO business rules, NO handler and NO tenant decision — it only binds the module's inward
/// ports to their implementations (ADR-010 decision 14).
///
/// The Host references this seam instead of the concrete <c>Ingestion.Infrastructure</c>
/// assembly, so the composition root wires persistence without owning it (D-1; AGENTS.md
/// "Host composes modules, never owns persistence"). A missing/blank connection string fails
/// closed through the Infrastructure composition, before any <c>DbContext</c> is registered.
/// </summary>
public static class IngestionModuleComposition
{
    /// <summary>
    /// Composes the Ingestion module: Application use cases + Infrastructure persistence
    /// (context, trusted tenant scope, repositories) over the supplied connection string.
    /// </summary>
    public static IServiceCollection AddIngestionModule(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddIngestionApplication();
        services.AddIngestionInfrastructure(connectionString);

        return services;
    }
}

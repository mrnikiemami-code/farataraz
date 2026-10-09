namespace FaraTaraz.Modules.Ingestion.Infrastructure.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Module-local DI registration for the Ingestion Infrastructure layer.
///
/// This is the ONLY place the module's persistence is wired into the container. It contains no
/// business rules and no provider concept: it registers the EF Core context, the trusted tenant
/// scope, and the tenant-scoped repositories used by the (future) ingestion engine. The Host
/// composes modules through this extension and never references the concrete persistence
/// assembly directly (ADR-010 decision 14, structure.md §10.3).
/// </summary>
public static class IngestionInfrastructureComposition
{
    /// <summary>Default local PostgreSQL connection used when the composition root does not supply one.</summary>
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=farataraz";

    /// <summary>
    /// Registers the Ingestion persistence (context, trusted tenant scope, repositories).
    /// When <paramref name="connectionString"/> is null a default local PostgreSQL connection is used.
    /// </summary>
    public static IServiceCollection AddIngestionInfrastructure(
        this IServiceCollection services,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var cs = connectionString ?? DefaultConnectionString;

        services.AddDbContext<IngestionDbContext>(options =>
            options.UseNpgsql(cs));

        // Trusted tenant scope for the current unit of work. Resolves from a trusted execution
        // context (TenantContext) when one is bound; otherwise fails closed to "None". This is a
        // TryAdd registration shared across module composition, so a second module cannot overwrite it, and
        // a missing scope never silently becomes authorized (ADR-010 decision 5).
        services.TryAddScoped<DatabaseTenantScope>(provider =>
        {
            var context = provider.GetService<TenantContext>();
            return context is not null && context.IsTrusted
                ? DatabaseTenantScope.FromTrusted(context)
                : DatabaseTenantScope.None;
        });

        services.AddScoped<SourceRecordRepository>();
        services.AddScoped<SyncCheckpointRepository>();
        services.AddScoped<SyncRunRepository>();

        return services;
    }
}

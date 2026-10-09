namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;

using Microsoft.Extensions.DependencyInjection;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Authorization;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Module-local DI registration for the AccountingSources Infrastructure layer.
///
/// This is the ONLY place the module's persistence is wired into the container. It contains no
/// business rules and no provider concept: it binds the inward-facing
/// <c>IAccountingSourceOwnership</c> port to its database-backed implementation and registers
/// the EF Core context. The Host composes modules through this extension and never references
/// the concrete persistence assembly directly (ADR-010 decision 14, structure.md §10.3).
/// </summary>
public static class AccountingSourcesInfrastructureComposition
{
    /// <summary>
    /// Default local PostgreSQL connection used when the composition root does not supply one.
    /// Tests and the Host override this with a real / disposable database.
    /// </summary>
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=farataraz";

    /// <summary>
    /// Registers the AccountingSources persistence (context, trusted tenant scope, ownership
    /// oracle). When <paramref name="connectionString"/> is null a default local PostgreSQL
    /// connection is used.
    /// </summary>
    public static IServiceCollection AddAccountingSourcesInfrastructure(
        this IServiceCollection services,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var cs = connectionString ?? DefaultConnectionString;

        services.AddDbContext<AccountingSourcesDbContext>(options =>
            options.UseNpgsql(cs));

        // Trusted tenant scope for the current unit of work. Resolves from a trusted execution
        // context (TenantContext) when one is bound; otherwise fails closed to "None". This is the
        // single registration of the scope, so it is never overwritten by a competing default, and
        // a missing scope never silently becomes authorized (ADR-010 decision 5).
        services.AddScoped<DatabaseTenantScope>(provider =>
        {
            var context = provider.GetService<TenantContext>();
            return context is not null && context.IsTrusted
                ? DatabaseTenantScope.FromTrusted(context)
                : DatabaseTenantScope.None;
        });

        services.AddScoped<IAccountingSourceOwnership, EfAccountingSourceOwnership>();

        return services;
    }
}

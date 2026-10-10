namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FaraTaraz.BuildingBlocks.Configuration;
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
    /// Registers the AccountingSources persistence (context, trusted tenant scope, ownership
    /// oracle).
    ///
    /// <b>Fail-closed on missing configuration.</b> A nonempty connection string is required
    /// (FT-CONFIG-001): a null / empty / whitespace value throws
    /// <c>ConnectionConfigurationException</c> before any <c>DbContext</c> is registered, so the
    /// module never silently binds to an embedded default or an implicit localhost / PostgreSQL
    /// fallback. The caller must supply an explicit connection string.
    /// </summary>
    public static IServiceCollection AddAccountingSourcesInfrastructure(
        this IServiceCollection services,
        string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ConnectionConfigurationException(
                $"{nameof(AccountingSourcesInfrastructureComposition)} requires a nonempty connection string; " +
                "missing configuration fails closed.");
        }

        services.AddDbContext<AccountingSourcesDbContext>(options =>
            options.UseNpgsql(connectionString));

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

        services.AddScoped<IAccountingSourceOwnership, EfAccountingSourceOwnership>();

        return services;
    }
}

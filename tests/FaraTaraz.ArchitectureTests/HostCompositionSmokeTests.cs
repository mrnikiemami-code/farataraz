namespace FaraTaraz.ArchitectureTests;

using FaraTaraz.BuildingBlocks.Configuration;
using FaraTaraz.Host.Composition;
using FaraTaraz.Modules.AccountingSources.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Composition-root smoke guard (D-1; ADR-010 decision 14).
///
/// The composition-only Host must actually compose the modules' outward layers through their
/// module-owned <c>*.Composition</c> seams — otherwise the production dependency graph is
/// unresolvable (persistence and the ownership oracle are never registered). This proves the
/// host wires persistence + the ownership port, and that a missing connection string still
/// fails closed before any <c>DbContext</c> is registered.
///
/// The guard deliberately avoids referencing the Infrastructure assemblies: it asserts on
/// registered service types (the ownership port by type, the persistence services by name), so
/// the architecture test project never depends on module Infrastructure.
/// </summary>
public class HostCompositionSmokeTests
{
    private const string Connection =
        "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=farataraz";

    [Fact]
    public void Host_composition_wires_module_infrastructure_and_the_ownership_oracle()
    {
        var services = new ServiceCollection();

        services.ConfigureFaraTaraz(Connection);

        Assert.Contains(services, d => d.ServiceType == typeof(IAccountingSourceOwnership));
        Assert.Contains(services, d => d.ServiceType.FullName
            == "FaraTaraz.Modules.Ingestion.Infrastructure.Persistence.IngestionDbContext");
        Assert.Contains(services, d => d.ServiceType.FullName
            == "FaraTaraz.Modules.Ingestion.Infrastructure.Persistence.SyncRunRepository");
        Assert.Contains(services, d => d.ServiceType.FullName
            == "FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence.AccountingSourcesDbContext");
    }

    [Fact]
    public void Host_composition_fails_closed_without_a_connection_string()
    {
        var services = new ServiceCollection();

        Assert.Throws<ConnectionConfigurationException>(
            () => services.ConfigureFaraTaraz(string.Empty));
    }
}

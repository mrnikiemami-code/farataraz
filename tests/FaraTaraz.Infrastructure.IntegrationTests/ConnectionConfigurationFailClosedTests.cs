namespace FaraTaraz.Infrastructure.IntegrationTests;

using FaraTaraz.BuildingBlocks.Configuration;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;
using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using FaraTaraz.Modules.Ingestion.Infrastructure.Composition;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Fail-closed regression guard for FT-CONFIG-001.
///
/// Each module Infrastructure composition root rejects a null / empty / whitespace connection
/// string with a deterministic <see cref="ConnectionConfigurationException"/> <b>before</b> any
/// <see cref="DbContext"/> is registered, and accepts an explicitly supplied disposable test
/// connection string. No embedded default or implicit localhost / PostgreSQL fallback remains.
/// These are behavioral tests against the public composition entry points, not source-text checks.
/// </summary>
public sealed class ConnectionConfigurationFailClosedTests
{
    private const string ValidConnection =
        "Host=localhost;Port=54320;Username=postgres;Password=unused;Database=farataraz";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ingestion_composition_rejects_missing_connection_when_supplied(string? connectionString)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ConnectionConfigurationException>(() =>
            services.AddIngestionInfrastructure(connectionString));

        Assert.Contains("fails closed", exception.Message);
        // Thrown before AddDbContext: no DbContext options are registered for this module.
        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(DbContextOptions<IngestionDbContext>));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AccountingSources_composition_rejects_missing_connection_when_supplied(string? connectionString)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<ConnectionConfigurationException>(() =>
            services.AddAccountingSourcesInfrastructure(connectionString));

        Assert.Contains("fails closed", exception.Message);
        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(DbContextOptions<AccountingSourcesDbContext>));
    }

    [Fact]
    public void Ingestion_composition_registers_dbcontext_when_connection_supplied()
    {
        var services = new ServiceCollection();

        services.AddIngestionInfrastructure(ValidConnection);

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(DbContextOptions<IngestionDbContext>));
    }

    [Fact]
    public void AccountingSources_composition_registers_dbcontext_when_connection_supplied()
    {
        var services = new ServiceCollection();

        services.AddAccountingSourcesInfrastructure(ValidConnection);

        Assert.Contains(
            services,
            x => x.ServiceType == typeof(DbContextOptions<AccountingSourcesDbContext>));
    }
}

namespace FaraTaraz.Infrastructure.IntegrationTests;

using FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;
using FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared fixture for the real PostgreSQL integration tests.
///
/// Connects to a disposable PostgreSQL database (the CI-provisioned container, or a local one
/// via <c>FATARAZ_INTEGRATION_TEST_CONNECTION_STRING</c>), applies the module migrations once,
/// and hands out fresh <c>DbContext</c> instances so each test runs against a real database and
/// exercises real relational behaviour (tenant isolation, unique-constraint idempotency,
/// transactional rollback) — never the EF Core InMemory provider (ADR-010 decision 12).
/// </summary>
public sealed class IntegrationTestDb
{
    /// <summary>The PostgreSQL connection string used by the integration tests.</summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("FATARAZ_INTEGRATION_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=54320;Username=postgres;Password=ftpw;Database=farataraz";

    /// <summary>Creates a fresh <see cref="AccountingSourcesDbContext"/> bound to the test database.</summary>
    public AccountingSourcesDbContext AccountingSources()
        => new(DbContextOptionsFactory<AccountingSourcesDbContext>());

    /// <summary>Creates a fresh <see cref="IngestionDbContext"/> bound to the test database.</summary>
    public IngestionDbContext Ingestion()
        => new(DbContextOptionsFactory<IngestionDbContext>());

    /// <summary>Applies the module migrations to the test database (idempotent).</summary>
    public async Task ApplyMigrationsAsync()
    {
        await using var accounting = new AccountingSourcesDbContext(DbContextOptionsFactory<AccountingSourcesDbContext>());
        await accounting.Database.MigrateAsync();

        await using var ingestion = new IngestionDbContext(DbContextOptionsFactory<IngestionDbContext>());
        await ingestion.Database.MigrateAsync();
    }

    private static DbContextOptions<TContext> DbContextOptionsFactory<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(ConnectionString)
            .Options;
}

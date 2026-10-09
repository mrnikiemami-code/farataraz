namespace FaraTaraz.Modules.Ingestion.Infrastructure.Persistence;

using FaraTaraz.Modules.Ingestion.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <c>IngestionDbContext</c>.
///
/// It lets <c>dotnet ef migrations</c> scaffold from the model without a running application or a
/// live database (scaffolding never opens the connection). It is never used at runtime: the
/// module <c>Composition</c> wires the context through DI. The connection string comes from the
/// <c>FATARAZ_INGESTION_DB</c> environment variable when set, otherwise the module default.
/// </summary>
public sealed class IngestionDbContextFactory
    : IDesignTimeDbContextFactory<IngestionDbContext>
{
    /// <summary>
    /// Creates the context for the design-time tooling. The returned context is used only to
    /// read the model; its connection string is not opened during migration scaffolding.
    /// </summary>
    public IngestionDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("FATARAZ_INGESTION_DB")
            ?? IngestionInfrastructureComposition.DefaultConnectionString;

        return new IngestionDbContext(
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseNpgsql(connectionString)
                .Options);
    }
}

namespace FaraTaraz.Modules.AccountingSources.Infrastructure.Persistence;

using FaraTaraz.Modules.AccountingSources.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for <c>AccountingSourcesDbContext</c>.
///
/// It lets <c>dotnet ef migrations</c> scaffold from the model without a running application or a
/// live database (scaffolding never touches the database). It is never used at runtime: the
/// module <c>Composition</c> wires the context through DI. The connection string comes from the
/// <c>FATARAZ_ACCOUNTINGSOURCES_DB</c> environment variable when set, otherwise the module default.
/// </summary>
public sealed class AccountingSourcesDbContextFactory
    : IDesignTimeDbContextFactory<AccountingSourcesDbContext>
{
    /// <summary>
    /// Creates the context for the design-time tooling. The returned context is used only to
    /// read the model; its connection string is not opened during migration scaffolding.
    /// </summary>
    public AccountingSourcesDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("FATARAZ_ACCOUNTINGSOURCES_DB")
            ?? AccountingSourcesInfrastructureComposition.DefaultConnectionString;

        return new AccountingSourcesDbContext(
            new DbContextOptionsBuilder<AccountingSourcesDbContext>()
                .UseNpgsql(connectionString)
                .Options);
    }
}

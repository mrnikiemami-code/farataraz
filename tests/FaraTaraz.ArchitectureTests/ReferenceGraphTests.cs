namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.MasterData;
using Xunit;

/// <summary>
/// Asserts the documented project reference graph for the modular monolith:
///   BuildingBlocks           -> (none)
///   MasterData               -> BuildingBlocks
///   Ingestion.Domain         -> BuildingBlocks, MasterData
///   Ingestion.Application    -> BuildingBlocks, Ingestion.Domain, AccountingSources
///   AccountingSources        -> BuildingBlocks, Ingestion.Domain
///   Mock (adapter)           -> BuildingBlocks, MasterData, Ingestion.Domain,
///                               AccountingSources
///
/// Dependency direction is inward: platform modules never depend on a concrete adapter,
/// the foundation depends on nothing platform-wide, the Application layer depends on the
/// Domain (never the reverse), and the provider adapter implements the capability ports
/// without depending on the Application use cases.
/// </summary>
public class ReferenceGraphTests
{
    private static readonly Assembly BuildingBlocks = typeof(Tenant).Assembly;
    private static readonly Assembly MasterData = typeof(ExternalCustomerId).Assembly;
    private static readonly Assembly IngestionDomain = typeof(SyncRequest).Assembly;
    private static readonly Assembly IngestionApplication = typeof(SynchronizeCustomersQuery).Assembly;
    private static readonly Assembly AccountingSources = typeof(IAccountingProvider).Assembly;
    private static readonly Assembly Mock = typeof(MockAccountingProvider).Assembly;

    private static IReadOnlyList<string> FaraTarazReferences(Assembly assembly)
        => assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("FaraTaraz"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList()!;

    [Fact]
    public void BuildingBlocks_depends_on_no_platform_project()
        => Assert.Empty(FaraTarazReferences(BuildingBlocks));

    [Fact]
    public void MasterData_depends_only_on_BuildingBlocks()
        => Assert.Equal(
            new[] { "FaraTaraz.BuildingBlocks" },
            FaraTarazReferences(MasterData));

    [Fact]
    public void IngestionDomain_depends_only_on_BuildingBlocks_and_MasterData()
        => Assert.Equal(
            new[] { "FaraTaraz.BuildingBlocks", "FaraTaraz.Modules.MasterData" },
            FaraTarazReferences(IngestionDomain));

    [Fact]
    public void IngestionApplication_depends_on_BuildingBlocks_IngestionDomain_and_AccountingSources()
        => Assert.Equal(
            new[]
            {
                "FaraTaraz.BuildingBlocks",
                "FaraTaraz.Modules.AccountingSources",
                "FaraTaraz.Modules.Ingestion.Domain"
            },
            FaraTarazReferences(IngestionApplication));

    [Fact]
    public void AccountingSources_depends_on_BuildingBlocks_and_IngestionDomain()
        => Assert.Equal(
            new[] { "FaraTaraz.BuildingBlocks", "FaraTaraz.Modules.Ingestion.Domain" },
            FaraTarazReferences(AccountingSources));

    [Fact]
    public void Mock_adapter_depends_on_platform_capabilities_but_not_application()
        // The adapter implements the capability ports and uses the Domain/SourceModel types,
        // but never dispatches Application use cases through ISender.
        => Assert.Equal(
            new[]
            {
                "FaraTaraz.BuildingBlocks",
                "FaraTaraz.Modules.AccountingSources",
                "FaraTaraz.Modules.Ingestion.Domain",
                "FaraTaraz.Modules.MasterData"
            },
            FaraTarazReferences(Mock));
}

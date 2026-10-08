namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.MasterData;
using Xunit;

/// <summary>
/// Guards the dependency direction of the platform modules.
///
/// No platform module (MasterData / Ingestion.Domain / Ingestion.Application /
/// AccountingSources) or the foundation (BuildingBlocks) may depend on a concrete
/// provider adapter. Provider adapters are the OUTERMOST layer and depend inward.
/// </summary>
public class DependencyDirectionTests
{
    private static readonly Assembly[] PlatformModules =
    {
        typeof(ExternalCustomerId).Assembly,        // MasterData
        typeof(SyncRequest).Assembly,              // Ingestion.Domain
        typeof(SynchronizeCustomersQuery).Assembly, // Ingestion.Application
        typeof(IAccountingProvider).Assembly       // AccountingSources
    };

    private static bool ReferencesAdapter(Assembly assembly)
    {
        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name);
        return referenced.Any(n => n is not null &&
            (n.Contains("Adapters", StringComparison.OrdinalIgnoreCase) ||
             n.Contains("Mock", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void No_platform_module_must_reference_a_concrete_provider_adapter_assembly()
    {
        foreach (var module in PlatformModules)
        {
            Assert.False(ReferencesAdapter(module),
                $"Platform module '{module.GetName().Name}' must not reference a provider adapter.");
        }
    }

    [Fact]
    public void No_platform_module_must_expose_a_provider_adapter_namespace()
    {
        foreach (var module in PlatformModules)
        {
            var violating = module.GetTypes()
                .Where(t => t.Namespace is not null
                            && (t.Namespace.Contains("Adapters") || t.Namespace.Contains("Mock")))
                .ToList();

            Assert.Empty(violating);
        }
    }

    [Fact]
    public void BuildingBlocks_must_not_reference_any_platform_project()
    {
        var referencingFaraTaraz = typeof(Tenant).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("FaraTaraz"));

        Assert.DoesNotContain(referencingFaraTaraz, _ => true);
    }
}

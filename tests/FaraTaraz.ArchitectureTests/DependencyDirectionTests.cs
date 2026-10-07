namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Core.Application;
using Xunit;

/// <summary>
/// Guards the dependency direction and the recorded project reference graph.
/// Core/platform must never depend on a concrete provider adapter.
/// </summary>
public class DependencyDirectionTests
{
    private static readonly Assembly Core = typeof(IApplicationUseCase).Assembly;
    private static readonly Assembly BuildingBlocks = typeof(Tenant).Assembly;

    [Fact]
    public void Core_must_not_reference_a_concrete_provider_adapter_assembly()
    {
        var referenced = Core.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(referenced, n => n!.Contains("Adapters", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referenced, n => n!.Contains("Mock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Core_must_not_expose_any_provider_adapter_namespace()
    {
        var violating = Core.GetTypes()
            .Where(t => t.Namespace is not null
                        && (t.Namespace.Contains("Adapters") || t.Namespace.Contains("Mock")))
            .ToList();

        Assert.Empty(violating);
    }

    [Fact]
    public void BuildingBlocks_must_not_reference_any_platform_project()
    {
        var referencingFaraTaraz = BuildingBlocks.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("FaraTaraz"));

        Assert.DoesNotContain(referencingFaraTaraz, _ => true);
    }
}

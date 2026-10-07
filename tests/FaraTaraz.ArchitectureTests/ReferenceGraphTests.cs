namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Core.Application;
using Xunit;

/// <summary>
/// Asserts the documented project reference graph:
///   BuildingBlocks -> (none)
///   Core           -> BuildingBlocks
///   Mock           -> BuildingBlocks, Core
/// </summary>
public class ReferenceGraphTests
{
    private static readonly Assembly BuildingBlocks = typeof(Tenant).Assembly;
    private static readonly Assembly Core = typeof(IApplicationUseCase).Assembly;
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
    public void Core_depends_only_on_BuildingBlocks()
        => Assert.Equal(
            new[] { "FaraTaraz.BuildingBlocks" },
            FaraTarazReferences(Core));

    [Fact]
    public void Mock_depends_on_BuildingBlocks_and_Core()
        => Assert.Equal(
            new[] { "FaraTaraz.BuildingBlocks", "FaraTaraz.Core" },
            FaraTarazReferences(Mock));
}

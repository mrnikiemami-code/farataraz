namespace FaraTaraz.ArchitectureTests;

using System.Reflection;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Core.Application;
using Xunit;

/// <summary>
/// Provider-specific concepts must never live in Core or BuildingBlocks.
/// No type name or namespace in those assemblies may contain a provider product name,
/// nor the word "Adapter"/"Mock" (i.e. no adapter types leak into the platform core).
/// </summary>
public class ProviderLeakageTests
{
    private static readonly string[] ProviderTokens = { "Asan", "Sepidar", "Holoo", "Mahak" };
    private static readonly string[] AdapterTokens = { "Adapter", "Mock" };

    private static readonly Assembly[] PlatformAssemblies =
    {
        typeof(IApplicationUseCase).Assembly,
        typeof(Tenant).Assembly
    };

    [Fact]
    public void No_provider_specific_concepts_leak_into_platform_core()
    {
        var all = string.Join(
            " || ",
            PlatformAssemblies.SelectMany(a => a.GetTypes())
                .SelectMany(t => new[] { t.FullName ?? string.Empty, t.Namespace ?? string.Empty }));

        foreach (var token in ProviderTokens.Concat(AdapterTokens))
        {
            Assert.False(
                all.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"Provider/adapter token '{token}' leaked into platform core.");
        }
    }
}

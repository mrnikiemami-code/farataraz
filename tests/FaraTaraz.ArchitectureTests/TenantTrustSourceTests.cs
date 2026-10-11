namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Trust-source guards for FT-SEC-004 / D-2.
///
/// <see cref="FaraTaraz.BuildingBlocks.Tenancy.TenantContext"/> minting is internal to
/// <c>BuildingBlocks</c>; the only assemblies that can reach the internal factory are the
/// <c>InternalsVisibleTo</c> "friend" assemblies. This guard pins that surface to TEST assemblies
/// only, so a future production/module/host/adapter assembly cannot silently gain tenant-authority
/// minting, and it pins that W2 has NO production <c>TenantContext</c> producer (fail-closed):
/// the module compositions only READ a possibly-bound context and fall back to
/// <c>DatabaseTenantScope.None</c>.
/// </summary>
public class TenantTrustSourceTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    // Explicit, fail-closed allowlist of the ONLY assemblies granted internal minting access to
    // BuildingBlocks. Adding a friend requires an explicit, reviewed change here.
    private static readonly string[] AllowedFriendAssemblies =
    {
        "FaraTaraz.BuildingBlocks.Tests",
        "FaraTaraz.Infrastructure.IntegrationTests",
        "FaraTaraz.ArchitectureTests",
    };

    // The composition/DI surface is a READ, never a registration (see the two Infrastructure
    // compositions: GetService<TenantContext>()).
    private static readonly string[] ForbiddenRegistrationPatterns =
    {
        "AddScoped<TenantContext>", "TryAddScoped<TenantContext>",
        "AddSingleton<TenantContext>", "TryAddSingleton<TenantContext>",
        "AddTransient<TenantContext>", "TryAddTransient<TenantContext>",
        "AddScoped(typeof(TenantContext)", "TryAddScoped(typeof(TenantContext)",
        "AddSingleton(typeof(TenantContext)", "TryAddSingleton(typeof(TenantContext)",
        "AddTransient(typeof(TenantContext)", "TryAddTransient(typeof(TenantContext)",
    };

    [Fact]
    public void BuildingBlocks_grants_internal_minting_only_to_test_assemblies()
    {
        var friends = InternalsVisibleTo(Path.Combine(
            SrcRoot, "BuildingBlocks", "FaraTaraz.BuildingBlocks.csproj"));

        Assert.NotEmpty(friends);
        foreach (var friend in friends)
        {
            Assert.True(
                IsAllowedFriendAssembly(friend),
                $"'{friend}' is granted internal minting access to BuildingBlocks. Only TEST " +
                "assemblies may be friends; a production/module/host/adapter assembly must never " +
                "be able to reach the internal TenantContext factory (FT-SEC-004).");
        }
    }

    /// <summary>
    /// Negative proof: production/module/host/adapter assembly names are rejected by the
    /// friend-assembly classifier, so the guard above cannot pass on a widened friend list.
    /// </summary>
    [Theory]
    [InlineData("FaraTaraz.Modules.Ingestion.Application")]
    [InlineData("FaraTaraz.Modules.Ingestion.Infrastructure")]
    [InlineData("FaraTaraz.Modules.AccountingSources.Infrastructure")]
    [InlineData("FaraTaraz.Host")]
    [InlineData("FaraTaraz.Adapters.Accounting.Mock")]
    public void Production_assembly_names_are_not_acceptable_friend_assemblies(string assemblyName)
        => Assert.False(IsAllowedFriendAssembly(assemblyName));

    [Fact]
    public void Test_assembly_names_are_acceptable_friend_assemblies()
    {
        Assert.True(IsAllowedFriendAssembly("FaraTaraz.BuildingBlocks.Tests"));
        Assert.True(IsAllowedFriendAssembly("FaraTaraz.Infrastructure.IntegrationTests"));
        Assert.True(IsAllowedFriendAssembly("FaraTaraz.ArchitectureTests"));
    }

    [Fact]
    public void Production_registers_no_tenant_context_producer_fail_closed()
    {
        // W2 has no production tenant-authority producer: nothing registers a TenantContext, so the
        // module compositions resolve DatabaseTenantScope.None and every tenant-scoped operation
        // fails closed (ADR-011 context; FT-SEC-004 blocked on the auth boundary). This pins the
        // current fail-closed state so a producer cannot appear without an explicit, reviewed change.
        var offenders = new List<string>();

        foreach (var file in ProductionSourceFiles())
        {
            var cleaned = TenantBoundaryGuards.StripCommentsAndStrings(File.ReadAllText(file));
            foreach (var pattern in ForbiddenRegistrationPatterns)
            {
                if (cleaned.Contains(pattern, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetRelativePath(RepoRoot, file)} -> {pattern}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "No production TenantContext producer is expected in W2 (fail-closed). Found: " +
            string.Join("; ", offenders));
    }

    internal static bool IsAllowedFriendAssembly(string assemblyName)
        => AllowedFriendAssemblies.Contains(assemblyName, StringComparer.Ordinal);

    private static IReadOnlyList<string> InternalsVisibleTo(string csprojPath)
        => XDocument
            .Parse(File.ReadAllText(csprojPath))
            .Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();

    private static IEnumerable<string> ProductionSourceFiles()
        => Directory
            .EnumerateFiles(SrcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !SourceTreePaths.IsBuildOutput(file));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FaraTaraz.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repository root (FaraTaraz.sln) not found.");
    }
}

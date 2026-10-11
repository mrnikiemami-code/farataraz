namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

/// <summary>
/// Negative/positive proofs for <see cref="CompositionSeamPolicy"/> (D-1 hardening).
///
/// These demonstrate that a foreign or fake <c>*.Composition</c> project cannot grant Infrastructure
/// access to the host closure, and that a sanctioned, module-owned seam grants ONLY its own module's
/// Infrastructure. They are the failing reproduction of the naming-only bypass and the regression
/// guard once the module-owned rule is enforced.
/// </summary>
public class CompositionSeamPolicyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string Seam(string module)
        => Path.Combine(
            RepoRoot, "src", "Modules", module, module + ".Composition",
            $"FaraTaraz.Modules.{module}.Composition.csproj");

    private static string Infra(string module)
        => Path.Combine(
            RepoRoot, "src", "Modules", module, module + ".Infrastructure",
            $"FaraTaraz.Modules.{module}.Infrastructure.csproj");

    [Fact]
    public void Legitimate_module_seams_are_recognized_with_their_owning_module()
    {
        Assert.True(CompositionSeamPolicy.TryGetSeamModule(RepoRoot, Seam("Ingestion"), out var ingestion));
        Assert.Equal("Ingestion", ingestion);

        Assert.True(CompositionSeamPolicy.TryGetSeamModule(RepoRoot, Seam("AccountingSources"), out var accounting));
        Assert.Equal("AccountingSources", accounting);
    }

    [Theory]
    [InlineData("src/Evil/Fake.Composition/Fake.Composition.csproj")]
    [InlineData("src/Host/Fake.Composition/Fake.Composition.csproj")]
    [InlineData("src/Modules/Ingestion/Evil.Composition/FaraTaraz.Modules.Ingestion.Evil.Composition.csproj")]
    [InlineData("src/Modules/Ingestion/Ingestion.Composition/FaraTaraz.Modules.Ingestion.Evil.csproj")]
    [InlineData("src/Modules/Evil/Evil.Composition/FaraTaraz.Modules.Ingestion.Composition.csproj")]
    public void Foreign_or_fake_composition_projects_are_not_sanctioned_seams(string relative)
    {
        var path = Path.Combine(RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.False(CompositionSeamPolicy.TryGetSeamModule(RepoRoot, path, out _));
    }

    [Fact]
    public void Foreign_composition_project_grants_no_infrastructure_allowance()
    {
        var foreign = Path.Combine(RepoRoot, "src", "Evil", "Fake.Composition", "Fake.Composition.csproj");
        var infra = Infra("Ingestion");
        var closure = new[] { foreign, infra };

        static IReadOnlyList<string> References(string _) => new[] { Infra("Ingestion") };

        var allowed = CompositionSeamPolicy.AllowedInfrastructure(RepoRoot, closure, References);

        Assert.DoesNotContain(Path.GetFullPath(infra), allowed);
    }

    [Fact]
    public void Module_owned_seam_grants_only_its_own_module_infrastructure()
    {
        var seam = Seam("Ingestion");
        var own = Infra("Ingestion");
        var foreign = Infra("AccountingSources");
        var closure = new[] { seam, own, foreign };

        IReadOnlyList<string> References(string _) => new[] { own, foreign };

        var allowed = CompositionSeamPolicy.AllowedInfrastructure(RepoRoot, closure, References);

        Assert.Contains(Path.GetFullPath(own), allowed);
        Assert.DoesNotContain(Path.GetFullPath(foreign), allowed);
    }

    [Fact]
    public void Sanctioned_seam_still_allows_its_own_infrastructure()
    {
        var seam = Seam("AccountingSources");
        var own = Infra("AccountingSources");
        var closure = new[] { seam, own };

        IReadOnlyList<string> References(string _) => new[] { own };

        var allowed = CompositionSeamPolicy.AllowedInfrastructure(RepoRoot, closure, References);

        Assert.Contains(Path.GetFullPath(own), allowed);
    }

    [Fact]
    public void IsOwnModuleInfrastructure_rejects_cross_module_paths()
    {
        Assert.True(CompositionSeamPolicy.IsOwnModuleInfrastructure(RepoRoot, "Ingestion", Infra("Ingestion")));
        Assert.False(CompositionSeamPolicy.IsOwnModuleInfrastructure(RepoRoot, "Ingestion", Infra("AccountingSources")));
    }

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

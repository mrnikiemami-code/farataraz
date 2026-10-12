namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Inventory-wide architecture-enforcement tests (task FT-ARCH-GUARD-LOCK-003).
///
/// These tests apply the <see cref="ArchitecturePolicy"/> to the ENTIRE production project
/// inventory (every <c>src/**/*.csproj</c>), not to a hand-picked list of current modules, and
/// they prove the policy is effective with <b>real negative fixtures</b>: an isolated temporary
/// project graph with a deliberately forbidden reference is built on disk, fed to the same
/// enforcement code, and must be rejected. The fixture <c>.csproj</c> files are never part of any
/// compilation and live only under the OS temp directory.
/// </summary>
public class ArchitecturePolicyTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly ProjectDependencyGraph Graph = ProjectDependencyGraph.Build(RepoRoot);

    // --- Inventory coverage --------------------------------------------------------------

    [Fact]
    public void Production_dependency_graph_satisfies_the_architecture_policy()
    {
        var violations = ArchitecturePolicy.Violations(RepoRoot, Graph);

        Assert.True(
            violations.Count == 0,
            "The production project graph violates the architecture policy:\n  - " +
            string.Join("\n  - ", violations));
    }

    [Fact]
    public void Every_production_project_is_classified_and_every_layer_is_present()
    {
        var projects = ArchitecturePolicy.ProductionProjects(RepoRoot);
        Assert.NotEmpty(projects);

        foreach (var project in projects)
        {
            Assert.True(
                ArchitecturePolicy.Classify(project) != ProjectLayer.Unknown,
                $"Production project '{Path.GetFileName(project)}' is not classified. Add it to the " +
                "layer classification so it cannot silently escape the dependency policy.");
        }

        // The policy must actually exercise every layer kind, so a policy branch cannot go
        // untested by the real inventory.
        var layers = projects.Select(ArchitecturePolicy.Classify).ToHashSet();
        foreach (var layer in Enum.GetValues<ProjectLayer>().Where(l => l != ProjectLayer.Unknown))
        {
            Assert.Contains(layer, layers);
        }
    }

    [Fact]
    public void MediatR_handlers_live_only_in_application_projects()
        => Assert.Empty(ArchitecturePolicy.HandlerViolations(RepoRoot));

    // --- Negative proof: real invalid graphs are rejected --------------------------------

    [Fact]
    public void Negative_fixture_domain_referencing_infrastructure_is_rejected()
    {
        var root = WriteFixture(
            ("src/Modules/Alpha/Alpha.Domain/FaraTaraz.Modules.Alpha.Domain.csproj",
                new[] { @"..\Alpha.Infrastructure\FaraTaraz.Modules.Alpha.Infrastructure.csproj" }),
            ("src/Modules/Alpha/Alpha.Infrastructure/FaraTaraz.Modules.Alpha.Infrastructure.csproj",
                Array.Empty<string>()));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.Contains(violations, v =>
                v.Contains("Alpha.Domain", StringComparison.Ordinal) &&
                v.Contains("Infrastructure", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Negative_fixture_domain_referencing_application_is_rejected()
    {
        // The corrected D11 Domain boundary: a Domain project that depends on an Application
        // project must be rejected by the same policy the real inventory is checked against.
        var root = WriteFixture(
            ("src/Modules/Alpha/Alpha.Domain/FaraTaraz.Modules.Alpha.Domain.csproj",
                new[] { @"..\Alpha.Application\FaraTaraz.Modules.Alpha.Application.csproj" }),
            ("src/Modules/Alpha/Alpha.Application/FaraTaraz.Modules.Alpha.Application.csproj",
                Array.Empty<string>()));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.Contains(violations, v =>
                v.Contains("Alpha.Domain", StringComparison.Ordinal) &&
                v.Contains("Application", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Negative_fixture_application_referencing_infrastructure_is_rejected()
    {
        var root = WriteFixture(
            ("src/Modules/Alpha/Alpha.Application/FaraTaraz.Modules.Alpha.Application.csproj",
                new[] { @"..\Alpha.Infrastructure\FaraTaraz.Modules.Alpha.Infrastructure.csproj" }),
            ("src/Modules/Alpha/Alpha.Infrastructure/FaraTaraz.Modules.Alpha.Infrastructure.csproj",
                Array.Empty<string>()));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.Contains(violations, v =>
                v.Contains("Alpha.Application", StringComparison.Ordinal) &&
                v.Contains("Infrastructure", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Negative_fixture_buildingblocks_referencing_a_module_is_rejected()
    {
        var root = WriteFixture(
            ("src/BuildingBlocks/FaraTaraz.BuildingBlocks.csproj",
                new[] { @"..\Modules\Alpha\Alpha.Domain\FaraTaraz.Modules.Alpha.Domain.csproj" }),
            ("src/Modules/Alpha/Alpha.Domain/FaraTaraz.Modules.Alpha.Domain.csproj",
                Array.Empty<string>()));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.Contains(violations, v =>
                v.Contains("FaraTaraz.BuildingBlocks.csproj", StringComparison.Ordinal) &&
                v.Contains("Domain", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Negative_fixture_accountingsources_referencing_ingestion_domain_is_rejected()
    {
        var root = WriteFixture(
            ("src/Modules/AccountingSources/AccountingSources.Domain/FaraTaraz.Modules.AccountingSources.csproj",
                new[] { @"..\..\Ingestion\Ingestion.Domain\FaraTaraz.Modules.Ingestion.Domain.csproj" }),
            ("src/Modules/Ingestion/Ingestion.Domain/FaraTaraz.Modules.Ingestion.Domain.csproj",
                Array.Empty<string>()));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.Contains(violations, v =>
                v.Contains("AccountingSources", StringComparison.Ordinal) &&
                v.Contains("Ingestion.Domain", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // --- Negative proof: the allowed graph passes ---------------------------------------

    [Fact]
    public void Allowed_fixture_contracts_and_inward_references_pass()
    {
        // AccountingSources -> Ingestion.Contracts (the accepted cross-module boundary),
        // Domain -> Contracts, and Application -> Domain + Contracts are all allowed.
        var root = WriteFixture(
            ("src/Modules/AccountingSources/AccountingSources.Domain/FaraTaraz.Modules.AccountingSources.csproj",
                new[] { @"..\..\Ingestion\Ingestion.Contracts\FaraTaraz.Modules.Ingestion.Contracts.csproj" }),
            ("src/Modules/Ingestion/Ingestion.Contracts/FaraTaraz.Modules.Ingestion.Contracts.csproj",
                Array.Empty<string>()),
            ("src/Modules/Ingestion/Ingestion.Domain/FaraTaraz.Modules.Ingestion.Domain.csproj",
                new[] { @"..\Ingestion.Contracts\FaraTaraz.Modules.Ingestion.Contracts.csproj" }),
            ("src/Modules/Ingestion/Ingestion.Application/FaraTaraz.Modules.Ingestion.Application.csproj",
                new[]
                {
                    @"..\Ingestion.Domain\FaraTaraz.Modules.Ingestion.Domain.csproj",
                    @"..\Ingestion.Contracts\FaraTaraz.Modules.Ingestion.Contracts.csproj",
                }));

        try
        {
            var violations = ArchitecturePolicy.Violations(root, ProjectDependencyGraph.Build(root));

            Assert.True(
                violations.Count == 0,
                "The allowed fixture graph must pass, but the policy reported:\n  - " +
                string.Join("\n  - ", violations));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // --- Helpers ------------------------------------------------------------------------

    /// <summary>
    /// Writes an isolated, deliberately minimal project graph to a temp directory. The generated
    /// <c>.csproj</c> files are never referenced by any real project and are never compiled.
    /// </summary>
    private static string WriteFixture(params (string Path, string[] References)[] projects)
    {
        var root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ft-arch-policy", Guid.NewGuid().ToString("N"));

        foreach (var (relativePath, references) in projects)
        {
            var file = System.IO.Path.Combine(root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);

            var referenceXml = string.Join(
                Environment.NewLine,
                references.Select(r => $"    <ProjectReference Include=\"{r}\" />"));

            File.WriteAllText(
                file,
                $"<Project Sdk=\"Microsoft.NET.Sdk\">{Environment.NewLine}" +
                $"  <ItemGroup>{Environment.NewLine}{referenceXml}{Environment.NewLine}  </ItemGroup>" +
                $"{Environment.NewLine}</Project>{Environment.NewLine}");
        }

        return root;
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

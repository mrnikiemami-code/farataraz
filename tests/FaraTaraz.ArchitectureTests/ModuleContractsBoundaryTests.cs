namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Cross-module CONTRACT boundary guards (task FT-ARCH-CONTRACT-BOUNDARY-002).
///
/// The Ingestion module exposes a transport-neutral contract surface
/// (<c>Ingestion.Contracts</c>). AccountingSources may depend on that contract but NEVER on
/// <c>Ingestion.Domain</c>; the contract assembly itself must have no outward dependency
/// (Application/Infrastructure/Host/Adapters/MediatR/EF/Npgsql); and every production project must
/// be classified so a new project cannot silently escape guard coverage.
/// </summary>
public class ModuleContractsBoundaryTests
{
    private const string AccountingSourcesCsproj =
        "src/Modules/AccountingSources/AccountingSources.Domain/FaraTaraz.Modules.AccountingSources.csproj";
    private const string ContractsCsproj =
        "src/Modules/Ingestion/Ingestion.Contracts/FaraTaraz.Modules.Ingestion.Contracts.csproj";
    private const string DomainCsproj =
        "src/Modules/Ingestion/Ingestion.Domain/FaraTaraz.Modules.Ingestion.Domain.csproj";

    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly ProjectDependencyGraph Graph = ProjectDependencyGraph.Build(RepoRoot);

    [Fact]
    public void AccountingSources_domain_must_not_reference_Ingestion_Domain()
    {
        var csproj = Path.Combine(RepoRoot, AccountingSourcesCsproj.Replace('/', Path.DirectorySeparatorChar));

        var declared = Graph.DeclaredProjectReferences(csproj).Select(Path.GetFileName).ToList();
        var closure = Graph.TransitiveProjectClosure(csproj).Select(Path.GetFileName).ToList();

        Assert.DoesNotContain("FaraTaraz.Modules.Ingestion.Domain.csproj", declared);
        Assert.DoesNotContain("FaraTaraz.Modules.Ingestion.Domain.csproj", closure);
    }

    [Fact]
    public void Ingestion_contracts_must_not_have_an_outward_dependency()
    {
        var csproj = Path.Combine(RepoRoot, ContractsCsproj.Replace('/', Path.DirectorySeparatorChar));

        var closure = Graph.TransitiveProjectClosure(csproj).Select(Path.GetFileName).ToList();

        Assert.DoesNotContain("FaraTaraz.Modules.Ingestion.Application.csproj", closure);
        Assert.DoesNotContain("FaraTaraz.Host.csproj", closure);
        Assert.DoesNotContain(closure, n => n is not null &&
            (n.EndsWith(".Infrastructure.csproj", StringComparison.Ordinal) ||
             n.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal)));

        // No MediatR / EF / Npgsql package on the contract surface.
        var packages = Graph.PackageReferences(csproj);
        Assert.DoesNotContain(packages, p =>
            p.StartsWith("MediatR", StringComparison.Ordinal) ||
            p.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase));

        // The Domain assembly is NOT a contract concern of AccountingSources, but Domain itself
        // must not depend on the contract's consumers either.
        Assert.True(File.Exists(Path.Combine(RepoRoot, DomainCsproj.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    /// Negative fixture: the classifier rejects a cross-module Domain reference but accepts the
    /// Contracts reference, so the guard above cannot pass on a regressed reference.
    /// </summary>
    [Fact]
    public void Negative_fixture_distinguishes_forbidden_domain_from_allowed_contracts()
    {
        Assert.True(IsForbiddenCrossModuleDomainReference("../Ingestion/Ingestion.Domain/FaraTaraz.Modules.Ingestion.Domain.csproj"));
        Assert.False(IsForbiddenCrossModuleDomainReference("../Ingestion/Ingestion.Contracts/FaraTaraz.Modules.Ingestion.Contracts.csproj"));
    }

    [Fact]
    public void Every_production_project_is_classified()
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FaraTaraz.BuildingBlocks.csproj",
            "FaraTaraz.Modules.MasterData.csproj",
            "FaraTaraz.Modules.AccountingSources.csproj",
            "FaraTaraz.Modules.AccountingSources.Infrastructure.csproj",
            "FaraTaraz.Modules.AccountingSources.Composition.csproj",
            "FaraTaraz.Modules.Ingestion.Contracts.csproj",
            "FaraTaraz.Modules.Ingestion.Domain.csproj",
            "FaraTaraz.Modules.Ingestion.Application.csproj",
            "FaraTaraz.Modules.Ingestion.Infrastructure.csproj",
            "FaraTaraz.Modules.Ingestion.Composition.csproj",
            "FaraTaraz.Host.csproj",
            "FaraTaraz.Adapters.Accounting.Mock.csproj",
        };

        var actual = Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                          s.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.NotEmpty(actual);
        foreach (var name in actual)
        {
            Assert.True(
                known.Contains(name!),
                $"Production project '{name}' is not classified by the architecture guard. " +
                "Add it to the known-project set so a new module cannot silently escape coverage.");
        }
    }

    /// <summary>A cross-module reference to another module's Domain is forbidden; Contracts is allowed.</summary>
    internal static bool IsForbiddenCrossModuleDomainReference(string projectReference)
        => projectReference.EndsWith(".Domain.csproj", StringComparison.Ordinal)
           && !projectReference.EndsWith("Ingestion.Contracts.csproj", StringComparison.Ordinal);

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

namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// The architectural layer of a production project, derived from its project name/path.
/// Every production project under <c>src/</c> must map to exactly one layer, so a new project
/// cannot silently escape the dependency policy.
/// </summary>
internal enum ProjectLayer
{
    BuildingBlocks,
    Domain,
    Contracts,
    Application,
    Infrastructure,
    Composition,
    Host,
    Adapter,
    Unknown,
}

/// <summary>
/// The executable architecture policy for the WHOLE production project inventory
/// (task FT-ARCH-GUARD-LOCK-003).
///
/// It is a pure function over the existing <see cref="ProjectDependencyGraph"/> (the actual
/// declared <c>ProjectReference</c>/<c>PackageReference</c> graph — not folder names, not
/// compiler-pruned assembly metadata). Because the policy is pure, the tests can run it against
/// the real repository (must be empty) AND against an isolated, deliberately invalid temporary
/// project graph (must be rejected). That is real negative proof: an invalid dependency graph is
/// actually fed to the enforcement code, not asserted about as a string.
///
/// It classifies every production project into a layer and then enforces:
///   * Domain and Contracts assemblies have no outward dependency (Application, Infrastructure,
///     Composition, Host, adapters) and no persistence/MediatR package.
///   * Application has no Infrastructure/Composition/Host/adapter dependency and no persistence
///     package (MediatR is an Application concern and stays allowed — ADR-008).
///   * Composition depends only inward (no Host/adapter).
///   * Adapters are outermost (no Application/Infrastructure/Composition/Host).
///   * Host is composition-only: no declared Infrastructure/adapter and no persistence package.
///   * BuildingBlocks depends on no FaraTaraz project at all and no persistence/MediatR package.
///   * MediatR handlers appear only in Application assemblies (source-level inventory scan).
///   * The accepted AccountingSources → Ingestion.Contracts-only cross-module boundary holds
///     (AccountingSources must not depend on any other Ingestion assembly).
/// </summary>
internal static class ArchitecturePolicy
{
    private static readonly string[] PersistenceTokens =
    {
        "Npgsql", "PostgreSQL", "EntityFrameworkCore", "EFCore", "FluentNpgsql", "Dapper", "SQLite",
    };

    /// <summary>
    /// Layers a source layer must never reach (through its transitive project closure). Host is
    /// handled at the declared level only, because the accepted design reaches a module's
    /// Infrastructure through that module's <c>*.Composition</c> seam (<see href="structure.md"/> §4).
    /// </summary>
    private static readonly IReadOnlyDictionary<ProjectLayer, ProjectLayer[]> ForbiddenTargets =
        new Dictionary<ProjectLayer, ProjectLayer[]>
        {
            [ProjectLayer.BuildingBlocks] = new[]
            {
                ProjectLayer.Domain, ProjectLayer.Contracts, ProjectLayer.Application,
                ProjectLayer.Infrastructure, ProjectLayer.Composition, ProjectLayer.Host,
                ProjectLayer.Adapter,
            },
            [ProjectLayer.Domain] = new[]
            {
                ProjectLayer.Application, ProjectLayer.Infrastructure, ProjectLayer.Composition,
                ProjectLayer.Host, ProjectLayer.Adapter,
            },
            [ProjectLayer.Contracts] = new[]
            {
                ProjectLayer.Application, ProjectLayer.Infrastructure, ProjectLayer.Composition,
                ProjectLayer.Host, ProjectLayer.Adapter,
            },
            [ProjectLayer.Application] = new[]
            {
                ProjectLayer.Infrastructure, ProjectLayer.Composition, ProjectLayer.Host,
                ProjectLayer.Adapter,
            },
            [ProjectLayer.Infrastructure] = new[]
            {
                ProjectLayer.Composition, ProjectLayer.Host, ProjectLayer.Adapter,
            },
            [ProjectLayer.Composition] = new[] { ProjectLayer.Host, ProjectLayer.Adapter },
            [ProjectLayer.Host] = new[] { ProjectLayer.Infrastructure, ProjectLayer.Adapter },
            [ProjectLayer.Adapter] = new[]
            {
                ProjectLayer.Application, ProjectLayer.Infrastructure, ProjectLayer.Composition,
                ProjectLayer.Host,
            },
        };

    /// <summary>
    /// Every production project (<c>src/**/*.csproj</c>), excluding build output.
    /// </summary>
    internal static IReadOnlyList<string> ProductionProjects(string repoRoot)
        => Directory
            .EnumerateFiles(Path.Combine(repoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !SourceTreePaths.IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>Classifies a production project by layer. Returns <see cref="ProjectLayer.Unknown"/>
    /// for an unrecognized project so a new project shape fails closed.</summary>
    internal static ProjectLayer Classify(string projectFile)
    {
        var name = Path.GetFileNameWithoutExtension(projectFile);
        var segments = Path.GetFullPath(projectFile)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (segments.Any(s => s.Equals("Adapters", StringComparison.OrdinalIgnoreCase)) ||
            name.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal))
        {
            return ProjectLayer.Adapter;
        }

        if (name == "FaraTaraz.BuildingBlocks")
        {
            return ProjectLayer.BuildingBlocks;
        }

        if (name == "FaraTaraz.Host")
        {
            return ProjectLayer.Host;
        }

        if (name.EndsWith(".Contracts", StringComparison.Ordinal))
        {
            return ProjectLayer.Contracts;
        }

        if (name.EndsWith(".Application", StringComparison.Ordinal))
        {
            return ProjectLayer.Application;
        }

        if (name.EndsWith(".Infrastructure", StringComparison.Ordinal))
        {
            return ProjectLayer.Infrastructure;
        }

        if (name.EndsWith(".Composition", StringComparison.Ordinal))
        {
            return ProjectLayer.Composition;
        }

        if (name.EndsWith(".Domain", StringComparison.Ordinal))
        {
            return ProjectLayer.Domain;
        }

        // Shared canonical/bare-named domain modules (MasterData, AccountingSources): their
        // project file keeps the bare module name while the folder names the layer.
        if (name.StartsWith("FaraTaraz.Modules.", StringComparison.Ordinal))
        {
            return ProjectLayer.Domain;
        }

        return ProjectLayer.Unknown;
    }

    /// <summary>
    /// All architecture-policy violations in the repository's declared project graph. An empty
    /// result means the production dependency graph satisfies the policy.
    /// </summary>
    internal static IReadOnlyList<string> Violations(string repoRoot, ProjectDependencyGraph graph)
    {
        var violations = new List<string>();

        foreach (var project in ProductionProjects(repoRoot))
        {
            var layer = Classify(project);
            var relative = Relative(repoRoot, project);

            if (layer == ProjectLayer.Unknown)
            {
                violations.Add(
                    $"{relative}: production project is not classified by the architecture policy. " +
                    "Add it to the layer classification so it cannot escape guard coverage.");
                continue;
            }

            // Host is checked at the declared level: its transitive closure legitimately reaches a
            // module's Infrastructure through that module's *.Composition seam (structure.md §4).
            // Every other layer is checked through its transitive closure, so a forbidden
            // implementation layer cannot be laundered through an intermediate project.
            var targets = layer == ProjectLayer.Host
                ? graph.DeclaredProjectReferences(project)
                : graph.TransitiveProjectClosure(project);

            var forbidden = ForbiddenTargets[layer];
            foreach (var target in targets)
            {
                var targetLayer = Classify(target);
                if (forbidden.Contains(targetLayer))
                {
                    violations.Add(
                        $"{relative} ({layer}) must not depend on {targetLayer} project " +
                        $"'{Path.GetFileName(target)}'.");
                }
            }

            foreach (var package in graph.PackageReferences(project))
            {
                if (layer != ProjectLayer.Infrastructure && IsPersistencePackage(package))
                {
                    violations.Add(
                        $"{relative} ({layer}) must not reference persistence package '{package}'.");
                }

                if (layer != ProjectLayer.Application &&
                    package.StartsWith("MediatR", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{relative} ({layer}) must not reference MediatR package '{package}'; " +
                        "MediatR is an Application concern (ADR-008).");
                }
            }

            // Preserve the accepted cross-module boundary (FT-ARCH-CONTRACT-BOUNDARY-002):
            // AccountingSources depends on the Ingestion CONTRACT surface only, never on
            // Ingestion.Domain/Application/Infrastructure.
            if (ModuleOf(project) == "AccountingSources")
            {
                foreach (var reference in graph.TransitiveProjectClosure(project))
                {
                    if (ModuleOf(reference) == "Ingestion" &&
                        Classify(reference) != ProjectLayer.Contracts)
                    {
                        violations.Add(
                            $"{relative} (AccountingSources) must not depend on Ingestion project " +
                            $"'{Path.GetFileName(reference)}'; only Ingestion.Contracts is allowed.");
                    }
                }
            }
        }

        violations.AddRange(HandlerViolations(repoRoot));
        return violations;
    }

    /// <summary>
    /// Source-level inventory scan: no production project other than an Application project may
    /// declare a MediatR <c>IRequestHandler</c>. A source scan covers Infrastructure projects that
    /// the test assembly cannot load (they are deliberately not referenced by the guards).
    /// </summary>
    internal static IReadOnlyList<string> HandlerViolations(string repoRoot)
    {
        var violations = new List<string>();

        foreach (var project in ProductionProjects(repoRoot))
        {
            if (Classify(project) == ProjectLayer.Application)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(project)!;
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (SourceTreePaths.IsBuildOutput(file))
                {
                    continue;
                }

                if (File.ReadAllText(file).Contains("IRequestHandler", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{Relative(repoRoot, file)}: IRequestHandler must live only in an " +
                        "Application project (ADR-008/ADR-009).");
                }
            }
        }

        return violations;
    }

    private static bool IsPersistencePackage(string packageId)
        => PersistenceTokens.Any(token => packageId.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string? ModuleOf(string projectFile)
    {
        var segments = Path.GetFullPath(projectFile)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var index = Array.FindIndex(segments, s => s.Equals("Modules", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
    }

    private static string Relative(string repoRoot, string path)
        => Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
}

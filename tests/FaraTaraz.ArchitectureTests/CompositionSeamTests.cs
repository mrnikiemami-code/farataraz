namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Guards the module-owned composition seams introduced for D-1 (ADR-010 decision 14).
///
/// A composition seam is a module-owned project at
/// <c>src/Modules/&lt;Module&gt;/&lt;Module&gt;.Composition/…</c> (see <see cref="CompositionSeamPolicy"/>). It is
/// the only sanctioned path by which a module's Infrastructure may enter the Host's dependency
/// closure (see <see cref="HostAuthorityTests"/>). A seam must therefore be DI wiring only:
/// it wires its own module's Infrastructure and nothing else — never a provider adapter, never
/// a handler, never an Application feature type, never business logic.
///
/// The classifier is a pure helper (<see cref="SeamViolations"/>) exercised both against the
/// real production tree and against synthetic inputs, so a seam that drifts is caught rather
/// than silently accepted.
/// </summary>
public class CompositionSeamTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    private static readonly Regex HandlerInterface =
        new(@"IRequestHandler\s*<", RegexOptions.Compiled);

    [Fact]
    public void Every_composition_seam_is_preventive_wiring_only()
    {
        var seams = CompositionSeamProjects().ToList();
        Assert.NotEmpty(seams);

        foreach (var seam in seams)
        {
            var references = ProjectReferences(seam);
            var dir = Path.GetDirectoryName(seam)!;
            var sources = Directory
                .EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Select(File.ReadAllText)
                .ToList();

            var violations = SeamViolations(references, sources);
            Assert.True(
                violations.Count == 0,
                $"Composition seam '{Path.GetRelativePath(RepoRoot, seam)}' is invalid: " +
                string.Join("; ", violations));

            // Ownership: a seam must be a sanctioned, module-owned project, and it must wire ONLY
            // its own module's Infrastructure (no foreign/fake ".Composition" project, no
            // cross-module Infrastructure wiring).
            Assert.True(
                CompositionSeamPolicy.TryGetSeamModule(RepoRoot, seam, out var module),
                $"Composition seam '{Path.GetRelativePath(RepoRoot, seam)}' is not a sanctioned, " +
                "module-owned seam (expected src/Modules/<Module>/<Module>.Composition/…).");

            foreach (var reference in references.Where(CompositionSeamPolicy.IsInfrastructureProject))
            {
                Assert.True(
                    CompositionSeamPolicy.IsOwnModuleInfrastructure(RepoRoot, module, reference),
                    $"Composition seam '{Path.GetRelativePath(RepoRoot, seam)}' wires " +
                    $"'{Path.GetRelativePath(RepoRoot, reference)}', which is not its own module's Infrastructure.");
            }
        }
    }

    /// <summary>
    /// Negative proof: a seam that wires an adapter (instead of its module Infrastructure) or
    /// carries a request handler / MediatR / Application feature code is rejected by the
    /// classifier. The passing real tree is not sufficient evidence on its own.
    /// </summary>
    [Fact]
    public void Seam_classifier_rejects_adapter_reference_and_business_logic()
    {
        var adapter = Path.Combine(
            RepoRoot, "src", "Adapters", "Accounting.Mock",
            "FaraTaraz.Adapters.Accounting.Mock.csproj");
        var infrastructure = Path.Combine(
            RepoRoot, "src", "Modules", "Ingestion", "Ingestion.Infrastructure",
            "FaraTaraz.Modules.Ingestion.Infrastructure.csproj");

        // Wires an adapter instead of the module Infrastructure -> violation.
        var adapterSeam = SeamViolations(
            new[] { adapter },
            new[] { "namespace X; public static class Add { }" });
        Assert.NotEmpty(adapterSeam);

        // Wires correctly but smuggles in a request handler -> violation.
        var smugglerSeam = SeamViolations(
            new[] { infrastructure },
            new[] { "namespace X; class H : IRequestHandler<Q, R> { }" });
        Assert.NotEmpty(smugglerSeam);

        // Correct seam -> no violation.
        var validSeam = SeamViolations(
            new[] { infrastructure },
            new[] { "namespace FaraTaraz.Modules.Ingestion.Composition; public static class Add { }" });
        Assert.Empty(validSeam);
    }

    /// <summary>
    /// Pure classifier: returns the reasons a candidate seam is invalid.
    /// </summary>
    internal static IReadOnlyList<string> SeamViolations(
        IReadOnlyList<string> projectReferences,
        IReadOnlyList<string> sources)
    {
        var violations = new List<string>();

        if (!projectReferences.Any(IsInfrastructure))
        {
            violations.Add("it does not wire a module *.Infrastructure project");
        }

        if (projectReferences.Any(IsAdapter))
        {
            violations.Add("it references a provider adapter");
        }

        if (sources.Any(ContainsBusinessLogic))
        {
            violations.Add("it contains business logic (handler / MediatR / Application feature)");
        }

        return violations;
    }

    private static bool ContainsBusinessLogic(string source)
        => HandlerInterface.IsMatch(source)
           || source.Contains("MediatR", StringComparison.Ordinal)
           || source.Contains(".SynchronizeCustomers", StringComparison.Ordinal);

    private static IEnumerable<string> CompositionSeamProjects()
        => Directory
            .EnumerateFiles(SrcRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Where(file => Path.GetFileNameWithoutExtension(file)
                .EndsWith(".Composition", StringComparison.Ordinal));

    private static IReadOnlyList<string> ProjectReferences(string csprojPath)
        => XDocument
            .Parse(File.ReadAllText(csprojPath))
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .Select(value => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(csprojPath)!, value!)))
            .ToList();

    private static bool IsAdapter(string projectFile)
        => CompositionSeamPolicy.IsAdapterProject(projectFile);

    private static bool IsInfrastructure(string projectFile)
        => CompositionSeamPolicy.IsInfrastructureProject(projectFile);

    private static bool IsBuildOutput(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

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

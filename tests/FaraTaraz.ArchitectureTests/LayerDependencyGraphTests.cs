namespace FaraTaraz.ArchitectureTests;

using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Layer dependency guard (FMCA <c>structure.md</c> §10.3 / §4).
///
/// The Application layer resolves its inward dependencies (a capability port, an ownership
/// oracle, a trusted context) and never depends on a concrete implementation. A concrete
/// provider adapter or an Infrastructure project is an implementation of the capability port;
/// the Application layer must depend only on the port, never on the implementation.
///
/// Enforced at the <c>.csproj</c> level — both the declared project references and the
/// transitive project closure are classified by project ownership, so a declared-but-unused
/// dependency is caught even though the compiler prunes it from the assembly metadata.
/// </summary>
public class LayerDependencyGraphTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ApplicationCsproj = FindApplicationCsproj();
    private static readonly ProjectDependencyGraph Graph = ProjectDependencyGraph.Build(RepoRoot);

    [Fact]
    public void Application_declares_no_adapter_or_infrastructure_project_reference()
    {
        var violations = Graph
            .DeclaredProjectReferences(ApplicationCsproj)
            .Where(IsForbiddenLayer)
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Application layer must not declare a provider adapter or infrastructure project. " +
            $"Found: {detail}. It depends only on the capability port, never on the implementation.");
    }

    [Fact]
    public void Application_effective_dependency_graph_has_no_adapter_or_infrastructure()
    {
        var violations = Graph
            .TransitiveProjectClosure(ApplicationCsproj)
            .Where(IsForbiddenLayer)
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Application layer's transitive project dependency graph must not reach a provider " +
            $"adapter or infrastructure project. Found: {detail}.");
    }

    // --- Regression: an injected Infrastructure reference is detected ---------------------

    [Fact]
    public void Application_infrastructure_project_reference_is_detected()
    {
        // A declared reference to an Infrastructure project (even one the code does not use) must
        // be rejected by the ownership classification. The live Application.csproj has none, so a
        // representative Infrastructure path exercises this classifier.
        var fakeInfrastructure =
            Path.Combine(RepoRoot, "src", "Infrastructure", "FaraTaraz.Infrastructure.csproj");

        Assert.True(
            IsForbiddenLayer(fakeInfrastructure),
            "The guard must classify a declared Infrastructure ProjectReference as forbidden.");
    }

    private static bool IsForbiddenLayer(string projectFile)
    {
        var absolute = Path.GetFullPath(projectFile);
        var name = Path.GetFileNameWithoutExtension(absolute);
        var segments = absolute.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var isAdapter = name.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Adapters", StringComparison.OrdinalIgnoreCase));
        var isInfrastructure = name.StartsWith("FaraTaraz.Infrastructure", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase));

        return isAdapter || isInfrastructure;
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

    private static string FindApplicationCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var csproj = Path.Combine(
                dir.FullName, "src", "Modules", "Ingestion", "Ingestion.Application",
                "FaraTaraz.Modules.Ingestion.Application.csproj");

            if (File.Exists(csproj))
            {
                return csproj;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Application project file not found.");
    }
}

namespace FaraTaraz.ArchitectureTests;

using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Domain/Application boundary guard (FMCA <c>structure.md</c> §5 / §10.3).
///
/// The CQRS direction is one-way: <c>Delivery → ISender → Application → handler → port</c>. The
/// Domain layer declares only the sync contract and source models; it must never reference the
/// Application layer, MediatR, or a capability port. A Domain → Application edge reverses the
/// dependency and collapses the delivery → use-case boundary.
///
/// Enforced at the <c>.csproj</c> level (declared project references + transitive closure) so a
/// declared-but-unused reference is caught even though the compiler prunes it from the assembly
/// metadata.
/// </summary>
public class DomainApplicationBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DomainCsproj = FindDomainCsproj();
    private static readonly ProjectDependencyGraph Graph = ProjectDependencyGraph.Build(RepoRoot);

    [Fact]
    public void Domain_declares_no_application_project_reference()
    {
        var violations = Graph
            .DeclaredProjectReferences(DomainCsproj)
            .Where(IsForbiddenReference)
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Domain layer must not reference the Application layer. Found: {detail}. " +
            "The Domain declares only the sync contract and source models.");
    }

    [Fact]
    public void Domain_effective_dependency_graph_has_no_application_project()
    {
        var violations = Graph
            .TransitiveProjectClosure(DomainCsproj)
            .Where(IsForbiddenReference)
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Domain layer's transitive project dependency graph must not reach the Application " +
            $"layer. Found: {detail}.");
    }

    // --- Regression: an injected Application reference is detected ----------------------

    [Fact]
    public void Domain_application_project_reference_is_detected()
    {
        // A declared reference to the Application layer (even one the code does not use) must be
        // rejected by the ownership classification. The live Domain.csproj has none, so a
        // representative Application path exercises this classifier.
        var injectedApplication =
            Path.Combine(RepoRoot, "src", "Modules", "Ingestion", "Ingestion.Application",
                "FaraTaraz.Modules.Ingestion.Application.csproj");

        Assert.True(
            IsForbiddenReference(injectedApplication),
            "The guard must classify a declared Application ProjectReference as forbidden.");
    }

    private static bool IsForbiddenReference(string projectFile)
    {
        var absolute = Path.GetFullPath(projectFile);
        var segments = absolute.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // The Application layer is the only forbidden target from the Domain.
        return segments.Any(s =>
            s.Equals("Ingestion.Application", StringComparison.OrdinalIgnoreCase));
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

    private static string FindDomainCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var csproj = Path.Combine(
                dir.FullName, "src", "Modules", "Ingestion", "Ingestion.Domain",
                "FaraTaraz.Modules.Ingestion.Domain.csproj");

            if (File.Exists(csproj))
            {
                return csproj;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Domain project file not found.");
    }
}

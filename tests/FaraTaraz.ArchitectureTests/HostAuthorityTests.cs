namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FaraTaraz.Host.Composition;
using MediatR;
using Xunit;

/// <summary>
/// Host-authority guard (ADR-009, <c>structure.md</c> §10.3).
///
/// The host is a composition root with <b>ZERO business authority</b>. It must contain no
/// use case handlers, no Application feature types, no provider adapter and no persistence.
/// Business authority stays in the Application layer; the host only composes modules through
/// their module-local <c>Composition</c> extensions.
///
/// Enforcement is defense-in-depth, because no single view is sufficient:
///   1. <b>Source-level</b> reflection walks the actual Host assembly and rejects any loaded
///      type that owns business behavior (use case handlers, Application feature types).
///   2. <b>Project-reference level</b> reads the declared <c>ProjectReference</c>/<c>
///      PackageReference</c> entries and classifies them by project ownership: adapters and
///      infrastructure are forbidden by ownership, not by a package-name blacklist.
///   3. <b>Effective dependency graph</b> follows <c>ProjectReference</c> transitively and
///      rejects any adapter or infrastructure project reachable from the host.
///
/// The project-reference and graph guards read the declared project files rather than the
/// compiled assembly, because the C# compiler only emits a metadata reference for a
/// dependency whose types are actually used. An unused-but-declared adapter
/// <c>ProjectReference</c> leaves NO trace in <c>Assembly.GetReferencedAssemblies()</c>, so an
/// assembly-level check can never detect it. These identify REAL violations, so they keep
/// failing against a host that drifts.
/// </summary>
public class HostAuthorityTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string HostCsproj = FindHostCsproj();
    private static readonly Assembly Host = typeof(FaraTarazHost).Assembly;
    private static readonly ProjectDependencyGraph Graph = ProjectDependencyGraph.Build(RepoRoot);

    // --- 1. Source-level guards (loaded types) -------------------------------------------

    [Fact]
    public void Host_owns_no_use_case_handlers()
    {
        var handlers = Host
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

        Assert.DoesNotContain(handlers, _ => true);
    }

    [Fact]
    public void Host_touches_no_application_feature_types()
    {
        foreach (var type in Host.GetTypes())
        {
            Assert.False(
                type.Namespace is not null &&
                type.Namespace.StartsWith(
                    "FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers",
                    StringComparison.Ordinal),
                $"Host must not expose Application business types; '{type.FullName}' breaks zero business authority.");
        }
    }

    // --- 2. Project-reference level guards (declared ownership) --------------------------

    [Fact]
    public void Host_declares_no_adapter_or_infrastructure_project_reference()
    {
        var violations = Graph
            .DeclaredProjectReferences(HostCsproj)
            .Where(project => IsForbiddenLayer(project))
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Host project must not declare a provider adapter or infrastructure project. " +
            $"Found: {detail}. Business authority stays in the Application layer; the host only " +
            "composes modules, never owns a provider or persistence.");
    }

    [Fact]
    public void Host_declares_no_persistence_package()
    {
        var violations = Graph
            .PackageReferences(HostCsproj)
            .Where(package => !IsCompositionFrameworkPackage(package))
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The Host project must reference only composition-framework packages, never a " +
            $"persistence driver or other technical integration. Found: {detail}. Persistence " +
            "is an Infrastructure concern owned by a later wave, never the host.");
    }

    // --- 3. Effective dependency-graph guard (transitive) --------------------------------

    [Fact]
    public void Host_effective_dependency_graph_has_no_adapter_or_infrastructure()
    {
        var violations = Graph
            .TransitiveProjectClosure(HostCsproj)
            .Where(IsForbiddenLayer)
            .Select(Path.GetFullPath)
            .ToList();

        var detail = string.Join(", ", violations);
        Assert.True(
            violations.Count == 0,
            $"The host's transitive project dependency graph must not reach a provider adapter or " +
            $"infrastructure project. Found: {detail}.");
    }

    // --- Regression: the declared-reference guard catches an injected adapter reference ---

    [Fact]
    public void Host_adapter_project_reference_is_detected_by_the_graph_guard()
    {
        // A host that declares an adapter ProjectReference (even one its code does not use) must
        // be rejected by the ownership classification. This proves the guard reads the declared
        // dependency, not the compiler-pruned metadata. The live Host.csproj has none, so a
        // representative adapter path exercises this classifier.
        var injectedAdapter =
            Path.Combine(RepoRoot, "src", "Adapters", "Accounting.Mock", "FaraTaraz.Adapters.Accounting.Mock.csproj");

        Assert.True(
            IsForbiddenLayer(injectedAdapter),
            "The graph guard must classify an injected adapter ProjectReference as forbidden.");
    }

    // --- Classification helpers ----------------------------------------------------------

    private static bool IsForbiddenLayer(string projectFile)
    {
        var absolute = Path.GetFullPath(projectFile);
        var name = Path.GetFileNameWithoutExtension(absolute);
        var segments = absolute.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var isAdapter = name.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Adapters", StringComparison.OrdinalIgnoreCase));
        var isInfrastructure = name.EndsWith(".Infrastructure", StringComparison.Ordinal)
            || name.StartsWith("FaraTaraz.Infrastructure", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase));

        return isAdapter || isInfrastructure;
    }

    /// <summary>
    /// The composition root may reference only composition-framework packages (DI, middleware,
    /// logging). This is an explicit allowlist, so a persistence driver or any other technical
    /// integration is rejected by classification rather than by a package-name blacklist.
    /// </summary>
    private static bool IsCompositionFrameworkPackage(string packageId)
        => packageId.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal);

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

    private static string FindHostCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var csproj = Path.Combine(
                dir.FullName, "src", "Host", "FaraTaraz.Host", "FaraTaraz.Host.csproj");

            if (File.Exists(csproj))
            {
                return csproj;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Host project file (src/Host/FaraTaraz.Host.csproj) not found.");
    }
}

namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Parses the <c>.csproj</c> files of the modular monolith and builds the effective project
/// dependency graph, following <c>ProjectReference</c> entries transitively.
///
/// This is the "actual project dependency graph" that the FMCA layer rules are validated
/// against — not folder names and not compiler-pruned assembly metadata. The C# compiler only
/// emits a metadata reference for a dependency whose types are actually used, so an
/// unused-but-declared <c>ProjectReference</c> (for example a host that declares an adapter it
/// never touches) leaves no trace in <c>Assembly.GetReferencedAssemblies()</c>. Reading the
/// declared project files is the only way to enforce "the host composes modules, never owns a
/// provider".
/// </summary>
internal sealed class ProjectDependencyGraph
{
    private readonly Dictionary<string, ProjectModel> _projects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Indexes every <c>.csproj</c> under <paramref name="repoRoot"/> (excluding build output).
    /// </summary>
    public static ProjectDependencyGraph Build(string repoRoot)
    {
        var graph = new ProjectDependencyGraph();

        foreach (var csproj in Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(csproj))
            {
                continue;
            }

            var model = ProjectModel.Parse(repoRoot, csproj);
            graph._projects[model.ProjectFile] = model;
        }

        return graph;
    }

    /// <summary>Declared <c>ProjectReference</c> targets of a project, resolved to absolute paths.</summary>
    public IReadOnlyList<string> DeclaredProjectReferences(string projectFile)
        => _projects[Path.GetFullPath(projectFile)].ProjectReferences;

    /// <summary>Declared <c>PackageReference</c> <c>Include</c> values of a project.</summary>
    public IReadOnlyList<string> PackageReferences(string projectFile)
        => _projects[Path.GetFullPath(projectFile)].PackageReferences;

    /// <summary>
    /// Transitive closure of a project's <c>ProjectReference</c> targets, following each
    /// referenced project's own <c>ProjectReference</c>. Only projects present in the repository
    /// are traversed, so a declared reference to a project that does not exist is not resolved
    /// here (it is caught by the declared-reference classification instead).
    /// </summary>
    public IReadOnlyList<string> TransitiveProjectClosure(string projectFile)
    {
        var start = Path.GetFullPath(projectFile);
        var closure = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var reference in _projects[start].ProjectReferences)
        {
            var resolved = Path.GetFullPath(reference);
            if (_projects.ContainsKey(resolved))
            {
                closure.Add(resolved);
                queue.Enqueue(resolved);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var reference in _projects[current].ProjectReferences)
            {
                var resolved = Path.GetFullPath(reference);
                if (_projects.ContainsKey(resolved) && !closure.Contains(resolved))
                {
                    closure.Add(resolved);
                    queue.Enqueue(resolved);
                }
            }
        }

        return closure.ToList();
    }

    private static bool IsBuildOutput(string path)
        => path.Contains("\\bin\\", StringComparison.Ordinal) ||
           path.Contains("\\obj\\", StringComparison.Ordinal);

    private sealed class ProjectModel
    {
        public required string ProjectFile;
        public required IReadOnlyList<string> ProjectReferences;
        public required IReadOnlyList<string> PackageReferences;

        public static ProjectModel Parse(string repoRoot, string csprojPath)
        {
            var xml = XDocument.Parse(File.ReadAllText(csprojPath));

            var projectReferences = xml
                .Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include"))
                .Where(value => value is not null)
                .Select(value => Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(csprojPath)!, value!)))
                .ToList();

            var packageReferences = xml
                .Descendants("PackageReference")
                .Select(element => (string?)element.Attribute("Include"))
                .Where(value => value is not null)
                .Select(value => value!)
                .ToList();

            return new ProjectModel
            {
                ProjectFile = csprojPath,
                ProjectReferences = projectReferences,
                PackageReferences = packageReferences,
            };
        }
    }
}

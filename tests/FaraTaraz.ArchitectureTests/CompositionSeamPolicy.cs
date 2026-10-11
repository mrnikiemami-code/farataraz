namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Explicit policy for the sanctioned, module-owned composition seams (D-1 hardening).
///
/// A composition seam is the ONLY sanctioned path by which a module's Infrastructure may enter the
/// host's dependency closure. Ownership must not be grantable by <em>naming</em> a project
/// ".Composition": a foreign or fake <c>*.Composition</c> project must grant nothing.
///
/// Enforced rule: a seam is sanctioned only when it is physically module-owned at
/// <c>src/Modules/&lt;Module&gt;/&lt;Module&gt;.Composition/FaraTaraz.Modules.&lt;Module&gt;.Composition.csproj</c>,
/// and it may allow ONLY its own module's Infrastructure
/// (<c>src/Modules/&lt;Module&gt;/&lt;Module&gt;.Infrastructure/…</c>). All other Infrastructure must be
/// reached through an explicit, module-owned seam or it is rejected.
/// </summary>
internal static class CompositionSeamPolicy
{
    /// <summary>True when the project is an Infrastructure project (module-scoped or flat).</summary>
    internal static bool IsInfrastructureProject(string projectFile)
    {
        var absolute = Path.GetFullPath(projectFile);
        var name = Path.GetFileNameWithoutExtension(absolute);
        var segments = absolute.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return name.EndsWith(".Infrastructure", StringComparison.Ordinal)
            || name.StartsWith("FaraTaraz.Infrastructure", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when the project is a provider adapter project.</summary>
    internal static bool IsAdapterProject(string projectFile)
    {
        var absolute = Path.GetFullPath(projectFile);
        var name = Path.GetFileNameWithoutExtension(absolute);
        var segments = absolute.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return name.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal)
            || segments.Any(s => s.Equals("Adapters", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the module that owns a sanctioned composition seam. A seam is sanctioned only when
    /// it is physically module-owned at
    /// <c>src/Modules/&lt;Module&gt;/&lt;Module&gt;.Composition/FaraTaraz.Modules.&lt;Module&gt;.Composition.csproj</c>.
    /// A project that merely ends with ".Composition" (foreign or fake) is NOT sanctioned.
    /// </summary>
    internal static bool TryGetSeamModule(string repoRoot, string projectFile, out string module)
    {
        // src / Modules / <Module> / <Module>.Composition / FaraTaraz.Modules.<Module>.Composition.csproj
        var parts = RelativeParts(repoRoot, projectFile);

        if (parts.Length == 5
            && parts[0] == "src"
            && parts[1] == "Modules"
            && parts[3] == parts[2] + ".Composition"
            && parts[4] == "FaraTaraz.Modules." + parts[2] + ".Composition.csproj")
        {
            module = parts[2];
            return true;
        }

        module = string.Empty;
        return false;
    }

    /// <summary>True when the project is a sanctioned composition seam.</summary>
    internal static bool IsSanctionedSeam(string repoRoot, string projectFile)
        => TryGetSeamModule(repoRoot, projectFile, out _);

    /// <summary>
    /// True when <paramref name="projectFile"/> is exactly the own-module Infrastructure project of
    /// <paramref name="module"/> (<c>src/Modules/&lt;Module&gt;/&lt;Module&gt;.Infrastructure/…</c>).
    /// </summary>
    internal static bool IsOwnModuleInfrastructure(string repoRoot, string module, string projectFile)
    {
        var parts = RelativeParts(repoRoot, projectFile);
        return parts.Length >= 4
            && parts[0] == "src"
            && parts[1] == "Modules"
            && parts[2] == module
            && parts[3] == module + ".Infrastructure";
    }

    /// <summary>
    /// The Infrastructure projects the host closure may reach: ONLY the own-module Infrastructure
    /// declared by a sanctioned, module-owned composition seam. A foreign/fake "*.Composition"
    /// project, or a seam that wires another module's Infrastructure, grants nothing.
    /// </summary>
    internal static IReadOnlySet<string> AllowedInfrastructure(
        string repoRoot,
        IEnumerable<string> closure,
        Func<string, IReadOnlyList<string>> declaredReferences)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seam in closure)
        {
            if (!TryGetSeamModule(repoRoot, seam, out var module))
            {
                continue;
            }

            foreach (var reference in declaredReferences(seam))
            {
                if (IsOwnModuleInfrastructure(repoRoot, module, reference))
                {
                    allowed.Add(Path.GetFullPath(reference));
                }
            }
        }

        return allowed;
    }

    private static string[] RelativeParts(string repoRoot, string projectFile)
        => Path
            .GetRelativePath(repoRoot, Path.GetFullPath(projectFile))
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
}

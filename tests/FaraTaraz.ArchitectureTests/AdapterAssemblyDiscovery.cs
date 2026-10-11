namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MediatR;

/// <summary>
/// Platform-independent discovery of the built provider-adapter assemblies (FT-W2-R15), so
/// <see cref="PhysicalStructureTests.No_adapter_owns_an_Application_handler"/> inspects the real
/// adapter assemblies on every OS and configuration instead of passing on an empty collection.
///
/// RED reproduction (v1): the selector excluded any path containing "ref" or "debug" (a
/// case-insensitive SUBSTRING test). A Debug-only build (the Linux CI Debug phase, or any fresh
/// debug build) puts the adapter at <c>bin/Debug/&lt;tfm&gt;/&lt;asm&gt;.dll</c>, which contains
/// "debug" and was therefore excluded — discovery returned nothing and a forbidden Application
/// handler in an adapter would escape detection.
/// GREEN (v2): accept only a built adapter assembly under a real <c>bin</c> directory SEGMENT,
/// independent of OS separator, and fail closed when an expected adapter cannot be discovered.
/// </summary>
internal static class AdapterAssemblyDiscovery
{
    /// <summary>
    /// Structural adapter projects: each immediate subdirectory of <c>&lt;srcRoot&gt;/Adapters</c>
    /// that holds exactly one csproj, paired with that project's assembly simple name.
    /// </summary>
    internal static IReadOnlyList<(string Directory, string AssemblyName)> AdapterProjects(string srcRoot)
    {
        var adaptersDir = Path.Combine(srcRoot, "Adapters");
        var projects = new List<(string, string)>();

        if (!Directory.Exists(adaptersDir))
        {
            return projects;
        }

        foreach (var dir in Directory.EnumerateDirectories(adaptersDir))
        {
            var csprojs = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).ToList();
            if (csprojs.Count == 1)
            {
                projects.Add((dir, Path.GetFileNameWithoutExtension(csprojs[0])));
            }
        }

        return projects.OrderBy(p => p.Item2, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// True when the path contains a <c>bin</c> directory segment (a real build output),
    /// independent of the OS path separator. <c>obj/.../ref</c> intermediate copies are rejected
    /// because they are not under a <c>bin</c> segment.
    /// </summary>
    internal static bool IsBuiltAdapterAssemblyPath(string path)
        => path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The built <c>&lt;assemblyName&gt;.dll</c> for an adapter project, or <c>null</c> when the
    /// adapter has not been built. Accepts any real <c>bin</c> build output (Debug or Release),
    /// independent of OS separator, and rejects <c>obj</c> intermediates.
    /// </summary>
    internal static string? FindAdapterAssemblyPath(string adapterProjectDir, string assemblyName)
    {
        if (!Directory.Exists(adapterProjectDir))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(adapterProjectDir, assemblyName + ".dll", SearchOption.AllDirectories)
            .Where(IsBuiltAdapterAssemblyPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Discovers and loads every expected adapter assembly. Fail-closed: throws when no adapter
    /// project exists, or when any expected adapter's built assembly cannot be found, so the guard
    /// can never pass on a silently empty collection.
    /// </summary>
    internal static IReadOnlyList<Assembly> DiscoverAdapterAssemblies(string srcRoot)
    {
        var projects = AdapterProjects(srcRoot);
        if (projects.Count == 0)
        {
            throw new InvalidOperationException(
                $"No adapter projects found under '{Path.Combine(srcRoot, "Adapters")}'; " +
                "the adapter guard must not pass on an empty set.");
        }

        var assemblies = new List<Assembly>(projects.Count);
        foreach (var (dir, assemblyName) in projects)
        {
            var dll = FindAdapterAssemblyPath(dir, assemblyName)
                ?? throw new InvalidOperationException(
                    $"Built adapter assembly '{assemblyName}.dll' was not found under '{dir}'. " +
                    "Build the adapter before running the guard (fail-closed).");

            assemblies.Add(LoadByNameOrPath(assemblyName, dll));
        }

        return assemblies;
    }

    /// <summary>True when any of the supplied types implements <c>IRequestHandler&lt;,&gt;</c>.</summary>
    internal static bool ContainsApplicationHandler(IEnumerable<Type> types)
        => types.Any(type => type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

    private static Assembly LoadByNameOrPath(string assemblyName, string path)
        => AppDomain.CurrentDomain
               .GetAssemblies()
               .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal))
           ?? Assembly.LoadFrom(path);
}

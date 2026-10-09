namespace FaraTaraz.ArchitectureTests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MediatR;
using FaraTaraz.Adapters.Accounting.Mock;
using FaraTaraz.BuildingBlocks.Accounting;
using FaraTaraz.BuildingBlocks.Tenancy;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.Modules.MasterData;
using Xunit;

/// <summary>
/// Physical architecture guards (W1-R2, task §5).
///
/// These enforce the physical layout declared in <c>docs/architecture/structure.md</c>:
///   A. exact path-to-namespace mapping (derived from the path, not a hard-coded file list);
///   B. capability-first organization (no generic technical-axis folders);
///   C. single-file leaf folders (explicit, documented allowlist);
///   D. dependency direction (Domain has no MediatR/Application; adapters own no handlers;
///       no persistence in W1-R2);
///   E. CQRS placement (handlers live only in valid Application capability paths).
///
/// The guards identify REAL violations: they walk the actual <c>src/</c> tree and the actual
/// referenced assemblies, so they keep failing against a drifted repository rather than a
/// fixed snapshot.
/// </summary>
public class PhysicalStructureTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    // Generic technical-axis folders that must never host Application requests/handlers.
    private static readonly string[] GenericTechnicalFolders =
    {
        "Commands", "Queries", "Handlers", "Validators",
        "Services", "Helpers", "Utils", "Misc", "Managers"
    };

    // Legitimate single-file leaf folders under src/: each is a single capability/domain
    // boundary that legitimately holds exactly one translation unit. Documented here so a NEW
    // unjustified one-file leaf fails the guard.
    private static readonly string[] AllowedSingleFileLeaves =
    {
        Path.Combine("BuildingBlocks", "Accounting"),
        Path.Combine("BuildingBlocks", "Application"),
        Path.Combine("BuildingBlocks", "Identifiers"),
        Path.Combine("Modules", "MasterData"),
        Path.Combine("Modules", "AccountingSources", "Providers"),
        Path.Combine("Modules", "Ingestion", "Ingestion.Domain", "SourceModel"),
        // Module-local DI registration (no business rules) is a justified single-file leaf.
        Path.Combine("Modules", "Ingestion", "Ingestion.Application", "Composition"),
        // Composition root (zero business authority) is a justified single-file leaf.
        Path.Combine("Host", "FaraTaraz.Host", "Composition"),
    };

    private static readonly Regex NamespaceDeclaration =
        new(@"^\s*namespace\s+([\w\.]+)\s*(;|\{)", RegexOptions.Multiline);

    // Project/module folder (relative to src/) mapped to its namespace root. The Ingestion/
    // folder is a physical container for two projects and is NOT a namespace segment; each
    // project folder is. Remaining sub-directory paths append as namespace segments. This is
    // the project/module mapping from structure.md, not a per-file hard-coded list.
    private static readonly Dictionary<string, string> ProjectNamespaceRoots = new()
    {
        ["BuildingBlocks"] = "FaraTaraz.BuildingBlocks",
        ["Modules/MasterData"] = "FaraTaraz.Modules.MasterData",
        ["Modules/AccountingSources"] = "FaraTaraz.Modules.AccountingSources",
        ["Modules/Ingestion/Ingestion.Domain"] = "FaraTaraz.Modules.Ingestion.Domain",
        ["Modules/Ingestion/Ingestion.Application"] =
            "FaraTaraz.Modules.Ingestion.Application",
        ["Adapters/Accounting.Mock"] = "FaraTaraz.Adapters.Accounting.Mock",
        // Host project folder (relative to src/): the composition root assembly.
        ["Host/FaraTaraz.Host"] = "FaraTaraz.Host",
    };

    // --- A. Exact path-to-namespace mapping -----------------------------------------------

    [Fact]
    public void Every_production_file_declares_its_expected_namespace()
    {
        foreach (var file in ProductionSourceFiles())
        {
            var relative = Path.GetRelativePath(SrcRoot, file).Replace("\\", "/");
            var source = File.ReadAllText(file);

            Assert.True(
                FileNamespaceMatchesDirectory(relative, source),
                $"'{file}' declares '{string.Join(", ", DeclaredNamespaces(source))}' " +
                $"but is located where '{ExpectedNamespace(file)}' belongs. " +
                "Move the file or rename the namespace to match structure.md.");
        }
    }

    [Fact]
    public void No_production_file_mixes_unrelated_namespaces()
    {
        foreach (var file in ProductionSourceFiles())
        {
            var expected = ExpectedNamespace(file);
            var namespaces = DeclaredNamespaces(File.ReadAllText(file));

            var unrelated = namespaces
                .Where(ns => ns != expected && !ns.StartsWith(expected + ".", StringComparison.Ordinal))
                .ToList();

            Assert.Empty(unrelated);
        }
    }

    // --- B. Capability-first organization -------------------------------------------------
    // FMCA <Capability>.Application/ is organized by FEATURE, then by
    // Commands/Queries/Models/Ports (structure.md §10.2). Only FLAT, application-root-level
    // generic folders (e.g. `Ingestion.Application/Queries/`) are rejected; per-feature
    // subfolders (e.g. `.../SynchronizeCustomers/Queries/`) are allowed. This corrects the
    // guard toward the FMCA target without weakening it: flat global folders are still
    // rejected.

    [Fact]
    public void Application_layer_uses_no_generic_technical_axis_folders()
    {
        var applicationRoot = Path.Combine(SrcRoot, "Modules", "Ingestion", "Ingestion.Application");

        var violations = Directory
            .GetDirectories(applicationRoot, "*", SearchOption.AllDirectories)
            .Select(dir => Path.GetRelativePath(applicationRoot, dir).Replace("\\", "/"))
            .Where(rel => {
                var segments = rel.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
                // Reject only a flat generic folder directly under the Application root (one
                // segment). Per-feature subfolders keep the generic segment as a deeper segment.
                return segments.Length == 1 && GenericTechnicalFolders.Contains(segments[0]);
            })
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>
    /// FMCA structure.md §10.2 feature-first placement: every Application handler must live
    /// under its feature's Commands/ or Queries/ folder (one folder = one namespace).
    /// </summary>
    [Fact]
    public void Every_application_handler_lives_in_a_commands_or_queries_namespace()
    {
        var application = typeof(SynchronizeCustomersQuery).Assembly;

        var handlers = application
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

        foreach (var handler in handlers)
        {
            var ns = handler.Namespace;

            Assert.True(
                ns is not null &&
                (ns.EndsWith(".Commands", StringComparison.Ordinal) ||
                 ns.EndsWith(".Queries", StringComparison.Ordinal)),
                $"Application handler '{handler.FullName}' must live under a feature Commands/ or Queries/ folder (FMCA structure.md §10.2).");
        }
    }

    // --- C. Single-file leaf folders ------------------------------------------------------

    [Fact]
    public void Single_file_leaf_folders_are_documented_and_justified()
    {
        foreach (var leaf in SingleFileLeaves())
        {
            var rel = Path.GetRelativePath(SrcRoot, leaf).Replace("\\", "/");

            // Allowlist entries are built with Path.Combine (OS separators); rel is portable
            // forward-slash. Normalize the comparison so documented leaves match across platforms
            // without weakening the guard.
            var justified = AllowedSingleFileLeaves.Any(entry => entry.Replace("\\", "/") == rel);

            Assert.True(
                justified,
                $"Single-file leaf '{rel}' is not in the documented allowlist. Either merge it into " +
                "its parent capability or add a justified entry to the allowlist.");
        }
    }

    // --- D. Dependency direction ----------------------------------------------------------

    [Fact]
    public void Ingestion_Domain_does_not_reference_MediatR()
    {
        var domain = typeof(SyncRequest).Assembly;

        var referenced = domain.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(
            referenced,
            n => n is not null && n.StartsWith("MediatR", StringComparison.Ordinal));
    }

    [Fact]
    public void Ingestion_Domain_does_not_reference_Ingestion_Application()
    {
        var domain = typeof(SyncRequest).Assembly;
        var application = typeof(SynchronizeCustomersQuery).Assembly;

        var referenced = domain.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(
            referenced,
            n => n is not null && n == application.GetName().Name);
    }

    [Fact]
    public void No_adapter_owns_an_Application_handler()
    {
        var applicationHandler = typeof(SynchronizeCustomersHandler);

        foreach (var adapter in AdapterAssemblies())
        {
            var handlers = adapter
                .GetTypes()
                .Where(t => t.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                .ToList();

            Assert.True(
                handlers.Count == 0,
                $"Adapter '{adapter.GetName().Name}' must not own an Application handler.");
        }
    }

    [Fact]
    public void No_persistence_packages_in_W1_R2_platform_modules()
    {
        var persistenceTokens = new[]
        {
            "Npgsql", "PostgreSQL", "Microsoft.EntityFrameworkCore", "EFCore",
            "FluentNpgsql", "Dapper", "SQLite"
        };

        var projects = new[]
        {
            "BuildingBlocks",
            "Modules/MasterData",
            "Modules/AccountingSources",
            "Modules/Ingestion/Ingestion.Domain",
            "Modules/Ingestion/Ingestion.Application",
        };

        foreach (var folder in projects)
        {
            var csproj = Directory
                .EnumerateFiles(Path.Combine(SrcRoot, folder), "*.csproj", SearchOption.TopDirectoryOnly)
                .Single();
            var xml = System.Xml.Linq.XDocument.Parse(File.ReadAllText(csproj));

            var packageIds = xml
                .Descendants("PackageReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(t => t is not null)
                .Select(t => t!);

            var referenced = string.Join(" || ", packageIds);

            foreach (var token in persistenceTokens)
            {
                Assert.False(
                    referenced.Contains(token, StringComparison.OrdinalIgnoreCase),
                    $"'{folder}' must not reference a persistence package containing '{token}'.");
            }
        }
    }

    // --- E. CQRS placement ----------------------------------------------------------------

    [Fact]
    public void MediatR_handlers_live_only_in_the_Application_layer()
    {
        var application = typeof(SynchronizeCustomersQuery).Assembly;
        var domain = typeof(SyncRequest).Assembly;
        var platformModules = new[] { domain, typeof(ExternalCustomerId).Assembly, typeof(IAccountingProvider).Assembly };

        foreach (var module in platformModules)
        {
            if (module == application)
            {
                continue;
            }

            var handlers = module
                .GetTypes()
                .Where(t => t.GetInterfaces()
                    .Any(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

            Assert.Empty(handlers);
        }
    }

    // --- Regression: guards catch real violations -------------------------------

    /// <summary>
    /// The recursive leaf scan must detect a single-file leaf nested below a project folder, not
    /// just the immediate children of <c>src/</c>. A nested leaf that is not in the documented
    /// allowlist is then rejected by the guard.
    /// </summary>
    [Fact]
    public void Nested_unjustified_single_file_leaf_is_detected_and_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "ft-structure-guard", Guid.NewGuid().ToString("N"));
        var leafDir = Path.Combine(root, "Capability", "Deep");
        Directory.CreateDirectory(leafDir);
        File.WriteAllText(Path.Combine(leafDir, "Only.cs"), "namespace Demo;\nclass Only;\n");

        try
        {
            var detected = EnumerateSingleFileLeaves(new[] { root }).ToList();
            Assert.Contains(leafDir, detected);

            var rel = Path.GetRelativePath(root, leafDir).Replace("\\", "/");
            Assert.DoesNotContain(rel, AllowedSingleFileLeaves);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// A file physically located where a directory belongs, but declaring a deeper namespace (an
    /// extra segment beyond its directory), must be rejected by the exact namespace guard.
    /// </summary>
    [Fact]
    public void File_declaring_extra_namespace_segment_is_rejected()
    {
        const string relativePath = "BuildingBlocks/Identifiers/ExtraSegment.cs";
        const string source =
            "namespace FaraTaraz.BuildingBlocks.Identifiers.Sub;\nclass ExtraSegment;\n";

        Assert.False(FileNamespaceMatchesDirectory(relativePath, source));
    }

    // --- Helpers --------------------------------------------------------------------------

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

    private static IEnumerable<string> ProductionSourceFiles()
    {
        foreach (var dir in Directory.EnumerateDirectories(SrcRoot))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("\\bin\\", StringComparison.Ordinal) ||
                    file.Contains("\\obj\\", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static string ExpectedNamespace(string fullPath)
        => ExpectedNamespaceFromRelative(Path.GetRelativePath(SrcRoot, fullPath).Replace("\\", "/"));

    private static string ExpectedNamespaceFromRelative(string relativePath)
    {
        var relativeDir = Path.GetDirectoryName(relativePath)?.Replace("\\", "/") ?? string.Empty;

        // Longest matching project folder prefix.
        string? matched = null;
        foreach (var folder in ProjectNamespaceRoots.Keys)
        {
            if (relativeDir == folder ||
                relativeDir.StartsWith(folder + "/", StringComparison.Ordinal))
            {
                if (matched is null || folder.Length > matched!.Length)
                {
                    matched = folder;
                }
            }
        }

        if (matched is null)
        {
            throw new InvalidOperationException(
                $"File '{relativePath}' is not under a known project folder under src/.");
        }

        var root = ProjectNamespaceRoots[matched!];
        var remainder = relativeDir.Substring(matched!.Length).TrimStart('/');
        var segments = remainder.Split('/', System.StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0 ? root : root + "." + string.Join(".", segments);
    }

    private static IEnumerable<string> DeclaredNamespaces(string source)
    {
        foreach (Match match in NamespaceDeclaration.Matches(source))
        {
            yield return match.Groups[1].Value;
        }
    }

    /// <summary>
    /// Exact path-to-namespace enforcement: a file may declare only the namespace derived from
    /// its directory. A sub-namespace (an extra segment beyond the directory) is rejected, as is
    /// any unrelated namespace. This is the tightened form of the guard in
    /// <see cref="Every_production_file_declares_its_expected_namespace"/>.
    /// </summary>
    private static bool FileNamespaceMatchesDirectory(string relativePath, string source)
    {
        var expected = ExpectedNamespaceFromRelative(relativePath);
        var namespaces = DeclaredNamespaces(source).ToList();

        return namespaces.Count > 0 && namespaces.All(ns => ns == expected);
    }

    private static IEnumerable<string> SingleFileLeaves()
        => EnumerateSingleFileLeaves(new[] { SrcRoot });

    /// <summary>
    /// Recursively scans nested production directories for single-file leaves. <c>bin</c> and
    /// <c>obj</c> build-output directories are excluded. A directory is a single-file leaf when it
    /// holds exactly one <c>.cs</c> file and no sub-directories. Scanning recursively (rather than
    /// only the immediate children of <c>src/</c>) catches leaves nested under project folders.
    /// </summary>
    private static IEnumerable<string> EnumerateSingleFileLeaves(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                var segments = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(s => s == "bin" || s == "obj"))
                {
                    continue;
                }

                var files = Directory.EnumerateFiles(dir, "*.cs").ToList();
                var subdirectories = Directory.EnumerateDirectories(dir).ToList();

                if (files.Count == 1 && subdirectories.Count == 0)
                {
                    yield return dir;
                }
            }
        }
    }

    private static Assembly[] AdapterAssemblies()
    {
        var adaptersDir = Path.Combine(SrcRoot, "Adapters");
        var result = new List<Assembly>();

        foreach (var adapterDir in Directory.EnumerateDirectories(adaptersDir))
        {
            var csproj = Directory.EnumerateFiles(adapterDir, "*.csproj").Single();
            var asmName = Path.GetFileNameWithoutExtension(csproj);

            var dllPath = Directory
                .EnumerateFiles(adapterDir, "*.dll", SearchOption.AllDirectories)
                .FirstOrDefault(p =>
                    Path.GetFileName(p) == asmName + ".dll" &&
                    !p.Contains("ref", StringComparison.OrdinalIgnoreCase) &&
                    !p.Contains("debug", StringComparison.OrdinalIgnoreCase));

            if (dllPath is not null)
            {
                result.Add(Assembly.LoadFrom(dllPath));
            }
        }

        return result.ToArray();
    }
}

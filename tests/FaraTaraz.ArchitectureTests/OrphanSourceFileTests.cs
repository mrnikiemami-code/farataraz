namespace FaraTaraz.ArchitectureTests;

using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

/// <summary>
/// Physical guard: no orphan source files (FMCA <c>structure.md</c> §10).
///
/// Every production <c>.cs</c> must live inside a known project directory. A <c>.cs</c>
/// placed directly under <c>src/</c> — outside every project/module folder — is an orphan
/// that is not compiled into any assembly and signals a file that was never wired into a
/// project. This closes the "no missing or duplicated source files" physical guard.
///
/// The known project directories mirror the solution layout in structure.md §1; keep the
/// two in sync when a new project is added.
/// </summary>
public class OrphanSourceFileTests
{
    // Project/module folders declared in structure.md §1. One folder = one assembly.
    private static readonly string[] KnownProjectDirectories =
    {
        "BuildingBlocks",
        "Modules/MasterData",
        "Modules/AccountingSources",
        "Modules/Ingestion/Ingestion.Domain",
        "Modules/Ingestion/Ingestion.Application",
        "Adapters/Accounting.Mock",
        "Host/FaraTaraz.Host",
    };

    [Fact]
    public void Every_production_file_lives_inside_a_known_project_directory()
    {
        var repoRoot = FindRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src");

        var violations = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => {
                if (file.Contains("\\bin\\", StringComparison.Ordinal) ||
                    file.Contains("\\obj\\", StringComparison.Ordinal))
                {
                    return false; // build output is excluded from the guard
                }

                var relative = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
                return !KnownProjectDirectories.Any(prefix =>
                    relative == prefix ||
                    relative.StartsWith(prefix + "/", StringComparison.Ordinal));
            })
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Orphan source files (not inside a known project directory): " +
            string.Join(", ", violations));
    }

    /// <summary>
    /// Regression: the guard rejects a stray <c>.cs</c> dropped directly under <c>src/</c>
    /// (outside every project directory). A passing guard must actually detect orphans.
    /// </summary>
    [Fact]
    public void Orphan_source_file_directly_under_src_is_detected()
    {
        var root = Path.Combine(Path.GetTempPath(), "ft-orphan-guard", Guid.NewGuid().ToString("N"));
        var stray = Path.Combine(root, "Stray.cs");
        Directory.CreateDirectory(root);
        File.WriteAllText(stray, "namespace Demo;\nclass Stray;\n");

        try
        {
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertOrphanDetected(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void AssertOrphanDetected(string srcRoot)
    {
        var violations = Directory
            .EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => {
                if (file.Contains("\\bin\\", StringComparison.Ordinal) ||
                    file.Contains("\\obj\\", StringComparison.Ordinal))
                {
                    return false;
                }

                var relative = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
                return !KnownProjectDirectories.Any(prefix =>
                    relative == prefix ||
                    relative.StartsWith(prefix + "/", StringComparison.Ordinal));
            })
            .ToList();

        Assert.True(violations.Count == 0, "Expected at least one orphan, found none.");
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
}

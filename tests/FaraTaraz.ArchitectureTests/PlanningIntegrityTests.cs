namespace FaraTaraz.ArchitectureTests;

using System.IO;
using Xunit;

/// <summary>
/// Lightweight guard that the required planning / architecture source-of-truth files exist.
/// This is a presence check, not a documentation framework. It prevents accidental removal
/// of the repository's planning area and architecture constitution.
/// </summary>
public class PlanningIntegrityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

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

    [Fact]
    public void Required_planning_and_architecture_files_exist()
    {
        var required = new[]
        {
            Path.Combine("docs", "architecture", "architecture-constitution.md"),
            Path.Combine("docs", "planning", "CURRENT-STATE.md"),
            Path.Combine("docs", "planning", "ROADMAP.md"),
            Path.Combine("docs", "planning", "DELIVERY-PLAN.md"),
        };

        foreach (var relative in required)
        {
            var full = Path.Combine(RepoRoot, relative);
            Assert.True(File.Exists(full), $"Missing required documentation file: {relative}");
        }
    }

    [Fact]
    public void ADR_directory_exists()
    {
        var adr = Path.Combine(RepoRoot, "docs", "architecture", "adr");
        Assert.True(Directory.Exists(adr), "Missing ADR directory: docs/architecture/adr");
    }
}

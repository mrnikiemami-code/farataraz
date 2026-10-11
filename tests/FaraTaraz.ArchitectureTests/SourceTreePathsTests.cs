namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

/// <summary>
/// Regression tests for cross-platform build-output exclusion (FT-CI-003 / D-6).
///
/// The same source tree must exclude generated <c>bin</c>/<c>obj</c> files whether it is observed
/// with Windows (<c>\</c>) or Unix (<c>/</c>) separators. These tests exercise the shared
/// <see cref="SourceTreePaths.IsBuildOutput"/> predicate with both styles, prove a real production
/// file is still scanned (so forbidden sources remain detectible), and prove generated files are
/// excluded.
/// </summary>
public class SourceTreePathsTests
{
    [Theory]
    [InlineData(@"C:\repo\src\Modules\X\obj\Debug\net10.0\Gen.cs")]
    [InlineData(@"C:\repo\src\Modules\X\bin\Gen.cs")]
    [InlineData(@"C:\repo\src\Modules\X\obj\Gen.cs")]
    [InlineData("/repo/src/Modules/X/obj/Debug/net10.0/Gen.cs")]
    [InlineData("/repo/src/Modules/X/bin/Gen.cs")]
    [InlineData("/repo/src/Modules/X/obj/Gen.cs")]
    [InlineData("C:/repo/src/Modules/X/obj/Debug/net10.0/Gen.cs")]
    [InlineData("C:/repo/src/Modules/X/bin/Gen.cs")]
    public void Generated_file_is_excluded_on_both_separators(string path)
        => Assert.True(SourceTreePaths.IsBuildOutput(path));

    [Theory]
    [InlineData(@"C:\repo\src\Modules\X\Real.cs")]
    [InlineData("/repo/src/Modules/X/Real.cs")]
    [InlineData("C:/repo/src/Modules/X/Real.cs")]
    [InlineData("/repo/src/modules/binance/Real.cs")] // "binance" is not the "bin" segment
    [InlineData("/repo/src/Modules/objo/Real.cs")] // "objo" is not the "obj" segment
    [InlineData("/repo/src/Modules/X/objective/Real.cs")]
    public void Real_source_file_is_not_excluded_on_either_separator(string path)
        => Assert.False(SourceTreePaths.IsBuildOutput(path));

    /// <summary>
    /// End-to-end: enumerating a tree and filtering with the shared predicate keeps the real
    /// production source file and drops generated files, on the running OS. Combined with the
    /// separator theories above, this proves equivalent behavior across separators.
    /// </summary>
    [Fact]
    public void Scan_excludes_generated_files_but_keeps_real_source()
    {
        var root = Path.Combine(Path.GetTempPath(), "ft-paths-" + Guid.NewGuid().ToString("N"));

        try
        {
            WriteFile(root, Path.Combine("src", "Modules", "X", "Real.cs"));
            WriteFile(root, Path.Combine("src", "Modules", "X", "obj", "Debug", "net10.0", "obj.g.cs"));
            WriteFile(root, Path.Combine("src", "Modules", "X", "bin", "bin.g.cs"));

            var scanned = ScannedFileNames(root);

            Assert.Contains("Real.cs", scanned);
            Assert.DoesNotContain("obj.g.cs", scanned);
            Assert.DoesNotContain("bin.g.cs", scanned);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    /// <summary>
    /// Negative: a REAL forbidden source file (a misplaced namespace, the kind the physical guards
    /// reject) is still enumerated by the scan — the cross-platform fix excludes generated files
    /// only and never hides a genuine violation.
    /// </summary>
    [Fact]
    public void Forbidden_real_source_is_still_scanned_so_it_can_be_detected()
    {
        var root = Path.Combine(Path.GetTempPath(), "ft-paths-neg-" + Guid.NewGuid().ToString("N"));

        try
        {
            // A real file under BuildingBlocks/Identifiers declaring the wrong namespace (a
            // forbidden source by the physical-structure rules).
            var forbidden = WriteFile(
                root,
                Path.Combine("src", "BuildingBlocks", "Identifiers", "ExtraSegment.cs"),
                "namespace FaraTaraz.BuildingBlocks.Identifiers.Sub;\nclass ExtraSegment;\n");
            WriteFile(root, Path.Combine("src", "BuildingBlocks", "obj", "gen.cs"));

            var scanned = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !SourceTreePaths.IsBuildOutput(file))
                .ToList();

            Assert.Contains(Path.GetFullPath(forbidden), scanned.Select(Path.GetFullPath));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static IReadOnlyList<string> ScannedFileNames(string root)
        => Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !SourceTreePaths.IsBuildOutput(file))
            .Select(Path.GetFileName)
            .ToList();

    private static string WriteFile(string root, string relative, string content = "namespace X;\nclass X;\n")
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static void DeleteTree(string root)
    {
        try
        {
            Directory.Delete(root, true);
        }
        catch (Exception)
        {
            // Best-effort cleanup.
        }
    }
}

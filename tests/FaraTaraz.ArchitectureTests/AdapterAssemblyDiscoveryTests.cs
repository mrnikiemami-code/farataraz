namespace FaraTaraz.ArchitectureTests;

using System;
using System.Collections.Generic;
using System.IO;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using Xunit;

/// <summary>
/// Regression proofs for <see cref="AdapterAssemblyDiscovery"/> (FT-W2-R15).
///
/// The original selector excluded any path containing "debug", so a Debug-only build (the Linux
/// CI Debug phase) discovered ZERO adapter assemblies and
/// <c>No_adapter_owns_an_Application_handler</c> passed vacuously. These tests pin the corrected,
/// platform-independent, fail-closed behaviour.
/// </summary>
public class AdapterAssemblyDiscoveryTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string SrcRoot = Path.Combine(RepoRoot, "src");

    [Fact]
    public void Debug_built_adapter_assembly_is_discovered()
    {
        WithTempDir(root =>
        {
            var adapter = Path.Combine(root, "Accounting.Mock");
            var built = WriteFile(adapter, Path.Combine("bin", "Debug", "net10.0", "Accounting.Mock.dll"));
            WriteFile(adapter, Path.Combine("obj", "Debug", "net10.0", "ref", "Accounting.Mock.dll"));

            var found = AdapterAssemblyDiscovery.FindAdapterAssemblyPath(adapter, "Accounting.Mock");

            Assert.Equal(Path.GetFullPath(built), Path.GetFullPath(found!));
        });
    }

    [Fact]
    public void Only_obj_intermediate_copies_are_not_discovered()
    {
        WithTempDir(root =>
        {
            var adapter = Path.Combine(root, "Accounting.Mock");
            WriteFile(adapter, Path.Combine("obj", "Debug", "net10.0", "Accounting.Mock.dll"));

            var found = AdapterAssemblyDiscovery.FindAdapterAssemblyPath(adapter, "Accounting.Mock");

            Assert.Null(found);
        });
    }

    [Theory]
    [InlineData(@"C:\repo\src\Adapters\A\bin\Debug\net10.0\A.dll", true)]
    [InlineData("C:/repo/src/Adapters/A/bin/Release/net10.0/A.dll", true)]
    [InlineData("/repo/src/Adapters/A/bin/Debug/net10.0/A.dll", true)]
    [InlineData(@"C:\repo\src\Adapters\A\obj\Debug\net10.0\A.dll", false)]
    [InlineData("/repo/src/Adapters/A/obj/Debug/net10.0/ref/A.dll", false)]
    public void Bin_segment_detection_is_platform_independent(string path, bool expected)
        => Assert.Equal(expected, AdapterAssemblyDiscovery.IsBuiltAdapterAssemblyPath(path));

    [Fact]
    public void Discovery_fails_closed_when_no_adapter_project_exists()
    {
        WithTempDir(root =>
        {
            Directory.CreateDirectory(Path.Combine(root, "src", "Adapters"));

            var ex = Assert.Throws<InvalidOperationException>(
                () => AdapterAssemblyDiscovery.DiscoverAdapterAssemblies(Path.Combine(root, "src")));

            Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Discovery_fails_closed_when_an_adapter_is_not_built()
    {
        WithTempDir(root =>
        {
            var adapter = Path.Combine(root, "src", "Adapters", "NotBuilt");
            Directory.CreateDirectory(adapter);
            File.WriteAllText(Path.Combine(adapter, "FaraTaraz.Adapters.NotBuilt.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            var ex = Assert.Throws<InvalidOperationException>(
                () => AdapterAssemblyDiscovery.DiscoverAdapterAssemblies(Path.Combine(root, "src")));

            Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Real_adapter_assemblies_are_discovered_so_handlers_cannot_escape()
    {
        var adapters = AdapterAssemblyDiscovery.DiscoverAdapterAssemblies(SrcRoot);

        Assert.NotEmpty(adapters);
        Assert.Contains(adapters, a => a.GetName().Name == "FaraTaraz.Adapters.Accounting.Mock");
    }

    [Fact]
    public void Application_handler_predicate_detects_a_handler_so_discovery_gaps_are_the_only_escape()
    {
        Assert.True(AdapterAssemblyDiscovery.ContainsApplicationHandler(
            new[] { typeof(SynchronizeCustomersHandler) }));

        Assert.False(AdapterAssemblyDiscovery.ContainsApplicationHandler(
            new[] { typeof(string) }));
    }

    private static void WithTempDir(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "ft-adapter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            body(root);
        }
        finally
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

    private static string WriteFile(string root, string relative)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
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

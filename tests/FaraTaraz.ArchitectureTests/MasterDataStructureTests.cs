namespace FaraTaraz.ArchitectureTests;

using System.IO;
using System.Linq;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.MasterData;
using Xunit;

/// <summary>Guards the W1 MasterData identity seam without inventing W2 identity-generation rules.</summary>
public sealed class MasterDataStructureTests
{
    [Fact]
    public void MasterData_declarations_remain_separate_and_in_the_module_root()
    {
        var root = FindRepositoryRoot();
        var module = Path.Combine(root, "src", "Modules", "MasterData");
        var expected = new[]
        {
            "CanonicalCustomer.cs", "CanonicalId.cs", "CanonicalProduct.cs",
            "ExternalCustomerId.cs", "ExternalProductId.cs"
        };
        var actual = Directory.GetFiles(module, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(module, file).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Source_and_tenant_identity_boundaries_are_distinct()
    {
        var a = new ExternalCustomerId(new AccountingSourceId("source-a"), "42");
        var b = new ExternalCustomerId(new AccountingSourceId("source-b"), "42");
        Assert.NotEqual(a, b);
        Assert.NotEqual(new ExternalProductId(a.SourceId, a.ExternalCode),
            new ExternalProductId(b.SourceId, b.ExternalCode));
        var canonical = new CanonicalId("platform-owned-1");
        Assert.NotEqual(new CanonicalCustomer(new TenantId("tenant-a"), canonical),
            new CanonicalCustomer(new TenantId("tenant-b"), canonical));
        Assert.NotEqual(new CanonicalProduct(new TenantId("tenant-a"), canonical),
            new CanonicalProduct(new TenantId("tenant-b"), canonical));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FaraTaraz.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("FaraTaraz.sln not found in parent directories.");
    }
}

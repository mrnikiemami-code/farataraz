namespace FaraTaraz.ArchitectureTests;

using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using FaraTaraz.Modules.AccountingSources;
using FaraTaraz.Modules.Ingestion.Domain.Synchronization;
using FaraTaraz.BuildingBlocks.Identifiers;
using Xunit;

/// <summary>
/// Guards the W1 synchronization contract at the type level.
///
/// These assert the shape of the async sync surface and the source-record identity contract
/// that the Mock conformance tests exercise at runtime. They encode the invariants the
/// architecture constitution requires of W1 without coupling to any specific adapter.
/// </summary>
public class SyncContractGuards
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

    /// <summary>
    /// The bounded sync contract is <c>Task&lt;SyncBatch&lt;TRecord&gt;&gt;</c> and always
    /// carries an explicit <c>CancellationToken</c>. Cancellation is a first-class contract
    /// element, never an afterthought.
    /// </summary>
    [Fact]
    public void Async_sync_port_uses_bounded_batch_and_requires_cancellation_token()
    {
        var method = typeof(ISyncablePort<int>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(ISyncablePort<int>.SyncAsync));

        Assert.True(
            typeof(System.Threading.Tasks.Task<SyncBatch<int>>) == method.ReturnType,
            "SyncAsync must return a bounded SyncBatch page, not a raw list.");

        var tokenParameter = method.GetParameters()
            .First(p => p.ParameterType == typeof(System.Threading.CancellationToken));

        Assert.True(
            tokenParameter.HasDefaultValue,
            "The CancellationToken must be a propagatable parameter (default = disabled).");
    }

    /// <summary>
    /// Source-record identity is <c>AccountingSourceId + RecordKind + ExternalId</c>. The
    /// record kind is a platform-level semantic namespace (not a provider field) and is
    /// required so that equal external codes for different record kinds are not confused.
    /// </summary>
    [Fact]
    public void Source_record_id_includes_source_context_and_record_kind()
    {
        var source = typeof(SourceRecordId).GetProperty(nameof(SourceRecordId.SourceId));
        var kind = typeof(SourceRecordId).GetProperty(nameof(SourceRecordId.RecordKind));
        var external = typeof(SourceRecordId).GetProperty(nameof(SourceRecordId.ExternalId));

        Assert.NotNull(source);
        Assert.True(typeof(AccountingSourceId) == source!.PropertyType,
            "SourceRecordId.SourceId must be of type AccountingSourceId.");

        Assert.NotNull(kind);
        Assert.True(typeof(string) == kind!.PropertyType,
            "SourceRecordId.RecordKind must be of type string.");

        Assert.NotNull(external);
        Assert.True(typeof(string) == external!.PropertyType,
            "SourceRecordId.ExternalId must be of type string.");
    }

    /// <summary>
    /// A synchronization request carries ONLY what the capability needs (source + mode +
    /// cursor + batch limit). It deliberately does NOT carry a <c>TenantId</c>: proving the
    /// trusted Tenant owns the source is the orchestration layer's responsibility, never the
    /// adapter's.
    /// </summary>
    [Fact]
    public void SyncRequest_carries_no_tenant_identity()
    {
        var tenantProperties = typeof(SyncRequest)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(TenantId))
            .ToList();

        Assert.Empty(tenantProperties);
    }

    /// <summary>
    /// W1 establishes contracts only. The foundation and every platform module may pull in
    /// a persistence stack (EF Core / Npgsql / PostgreSQL) — that belongs to W2 and must be
    /// gated by an explicit architecture decision.
    /// </summary>
    [Fact]
    public void Platform_declares_no_persistence_packages()
    {
        var persistenceTokens = new[]
        {
            "Npgsql",
            "PostgreSQL",
            "Microsoft.EntityFrameworkCore",
            "EFCore",
            "FluentNpgsql",
            "Dapper",
            "SQLite"
        };

        var projects = new[]
        {
            ("BuildingBlocks", "FaraTaraz.BuildingBlocks.csproj"),
            ("Modules/MasterData", "FaraTaraz.Modules.MasterData.csproj"),
            ("Modules/Ingestion/Ingestion.Domain",
                "FaraTaraz.Modules.Ingestion.Domain.csproj"),
            ("Modules/Ingestion/Ingestion.Application",
                "FaraTaraz.Modules.Ingestion.Application.csproj"),
            ("Modules/AccountingSources", "FaraTaraz.Modules.AccountingSources.csproj")
        };

        foreach (var (folder, csprojName) in projects)
        {
            var csprojPath = Path.Combine(RepoRoot, "src", folder, csprojName);
            var projectXml = XDocument.Parse(File.ReadAllText(csprojPath));

            var packageIds = projectXml
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
}

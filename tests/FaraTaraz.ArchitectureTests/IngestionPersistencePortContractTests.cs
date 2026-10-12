namespace FaraTaraz.ArchitectureTests;

using System;
using System.Linq;
using System.Reflection;
using FaraTaraz.BuildingBlocks.Identifiers;
using FaraTaraz.Modules.Ingestion.Application.Ports;
using Xunit;

/// <summary>
/// Design-contract guards for the Application-owned ingestion persistence ports (task M1/FT-W3-ARCH).
///
/// These pin the shape that lets orchestration move out of Infrastructure later without splitting
/// the W2 atomic transaction: exactly ONE transactional entry point, transaction-scoped session
/// operations with no independent commit, no persistence-framework types on the Application
/// surface, and no caller-supplied tenant id as authority.
/// </summary>
public class IngestionPersistencePortContractTests
{
    private const string PortsNamespace = "FaraTaraz.Modules.Ingestion.Application.Ports";

    private static Type[] PortTypes()
        => typeof(IIngestionWriteStore).Assembly
            .GetTypes()
            .Where(t => t.Namespace == PortsNamespace)
            .ToArray();

    [Fact]
    public void Write_store_has_a_single_transactional_entry_point()
    {
        var methods = typeof(IIngestionWriteStore).GetMethods();

        Assert.Single(methods);
        Assert.Equal("ExecuteAsync", methods[0].Name);
    }

    [Fact]
    public void Write_session_exposes_no_independent_commit_operation()
    {
        // The session must never be able to commit on its own; only the store's ExecuteAsync owns
        // the transaction. (SaveCheckpointAsync is a domain operation, not a unit-of-work commit.)
        foreach (var method in typeof(IIngestionWriteSession).GetMethods())
        {
            Assert.DoesNotContain("Commit", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SaveChanges", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Flush", method.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ports_expose_no_persistence_framework_types()
    {
        var referenced = typeof(IIngestionWriteStore).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referenced, n =>
            n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            n.StartsWith("Npgsql", StringComparison.Ordinal) ||
            n.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
            n.Equals("Dapper", StringComparison.OrdinalIgnoreCase));

        // And no port member may surface an EF/DbContext type by name.
        foreach (var type in PortTypes().SelectMany(t => t.GetMethods()))
        {
            var surface = type.GetParameters().Select(p => p.ParameterType.Name)
                .Append(type.ReturnType.Name);
            foreach (var name in surface)
            {
                Assert.DoesNotContain("DbContext", name, StringComparison.Ordinal);
                Assert.DoesNotContain("EntityFrameworkCore", name, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Ports_do_not_accept_a_caller_supplied_tenant_id()
    {
        // A caller-supplied TenantId must never appear on the persistence port surface: tenant
        // authority comes only from the trusted execution context.
        foreach (var method in PortTypes().SelectMany(t => t.GetMethods()))
        {
            Assert.DoesNotContain(
                method.GetParameters(),
                p => p.ParameterType == typeof(TenantId));
            Assert.NotEqual(typeof(TenantId), method.ReturnType);
        }
    }
}

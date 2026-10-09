namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using FaraTaraz.Host.Composition;
using MediatR;
using Xunit;

/// <summary>
/// Host-authority guard (ADR-009, <c>structure.md</c> §10.3).
///
/// The host is a composition root with <b>ZERO business authority</b>. It must contain no
/// use case handlers, no Application feature types, no provider adapter and no persistence.
/// Business authority stays in the Application layer; the host only composes modules through
/// their module-local <c>Composition</c> extensions.
///
/// These identify REAL violations: they reflect on the actual Host assembly, so they keep
/// failing against a host that drifts toward owning business behavior.
/// </summary>
public class HostAuthorityTests
{
    private static readonly Assembly Host = typeof(FaraTarazHost).Assembly;

    [Fact]
    public void Host_owns_no_use_case_handlers()
    {
        var handlers = Host
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

        Assert.DoesNotContain(handlers, _ => true);
    }

    [Fact]
    public void Host_touches_no_application_feature_types()
    {
        foreach (var type in Host.GetTypes())
        {
            Assert.False(
                type.Namespace is not null &&
                type.Namespace.StartsWith(
                    "FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers",
                    StringComparison.Ordinal),
                $"Host must not expose Application business types; '{type.FullName}' breaks zero business authority.");
        }
    }

    [Fact]
    public void Host_depends_on_no_concrete_provider_adapter()
    {
        var referenced = Host.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(
            referenced,
            n => n is not null && n.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal));
    }

    [Fact]
    public void Host_owns_no_persistence_reference()
    {
        var referenced = Host.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(
            referenced,
            n => n is not null &&
                 (n.StartsWith("Npgsql", StringComparison.Ordinal) ||
                  n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)));
    }
}

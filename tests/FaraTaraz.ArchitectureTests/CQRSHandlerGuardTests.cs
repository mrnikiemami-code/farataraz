namespace FaraTaraz.ArchitectureTests;

using System.Linq;
using System.Reflection;
using MediatR;
using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using Xunit;

/// <summary>
/// CQRS handler-dependency guard (FMCA <c>structure.md</c> §10.2 / ADR-009).
///
/// A request handler is a CQRS <b>leaf</b>: it resolves its inward dependencies (a
/// capability port, an ownership oracle, a trusted context) and does NOT receive an
/// <c>ISender</c>. A handler that depends on <c>ISender</c> can dispatch other handlers,
/// which collapses the delivery → use-case boundary and turns an Application use case
/// into an ad-hoc orchestrator. This is a static, behavior-preserving proxy for the rule
/// "handlers must not invoke <c>ISender</c> to dispatch other handlers": if <c>ISender</c>
/// is a constructor dependency, the handler is structurally able to, so it is rejected.
///
/// Handlers must also depend only on provider-independent ports, never on a concrete
/// provider adapter. Both are checked against the actual handler types, so a drift in
/// either direction fails the guard.
/// </summary>
public class CQRSHandlerGuardTests
{
    /// <summary>
    /// Every <c>IRequestHandler&lt;…&gt;</c> in the Application assembly must have a
    /// constructor whose parameters never include an <c>ISender</c> (MediatR dispatch).
    /// </summary>
    [Fact]
    public void Application_request_handlers_must_not_receive_ISender()
    {
        var application = typeof(SynchronizeCustomersQuery).Assembly;

        var handlers = application
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        var violations = handlers
            .Where(h => h.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Any(p => p.ParameterType is not null &&
                          p.ParameterType.Name.StartsWith("ISender", StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Application request handlers must not depend on ISender (they are CQRS leaves " +
            "and must not dispatch other handlers). Violations: " +
            string.Join("; ", violations.Select(h => h.FullName)));
    }

    /// <summary>
    /// Every <c>IRequestHandler&lt;…&gt;</c> in the Application assembly must depend only on
    /// provider-independent ports/abstractions — never on a concrete provider adapter
    /// (any assembly or namespace under <c>FaraTaraz.Adapters</c>).
    /// </summary>
    [Fact]
    public void Application_request_handlers_must_not_depend_on_concrete_providers()
    {
        var application = typeof(SynchronizeCustomersQuery).Assembly;

        var handlers = application
            .GetTypes()
            .Where(t => t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        var violations = handlers
            .Where(h => h.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.ParameterType?.FullName)
                .Where(f => f is not null &&
                           f.StartsWith("FaraTaraz.Adapters", StringComparison.Ordinal))
                .Any())
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Application request handlers must depend only on provider-independent ports, " +
            "never on a concrete provider adapter. Violations: " +
            string.Join("; ", violations.Select(h => h.FullName)));
    }
}

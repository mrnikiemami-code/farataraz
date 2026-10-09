namespace FaraTaraz.Modules.Ingestion.Application.Composition;

using FaraTaraz.Modules.Ingestion.Application.SynchronizeCustomers.Queries;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Module-local DI registration for <c>Ingestion.Application</c>.
///
/// This is the only place the Application layer wires its own use cases into the mediator.
/// It contains NO business rules: the use cases themselves live under
/// <c>SynchronizeCustomers</c>. Registration is isolated here so the host composes modules
/// rather than behaviors (see ADR-009 and <c>structure.md</c> §10.2 / §10.3). Delivery
/// remains through <c>ISender</c>; this never touches a provider, persistence or the host.
/// </summary>
public static class IngestionApplicationComposition
{
    /// <summary>
    /// Registers the Application use cases (handlers) with the mediator.
    /// </summary>
    public static IServiceCollection AddIngestionApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(SynchronizeCustomersQuery).Assembly));

        return services;
    }
}

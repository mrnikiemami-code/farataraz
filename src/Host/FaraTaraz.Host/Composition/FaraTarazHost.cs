namespace FaraTaraz.Host.Composition;

using FaraTaraz.Modules.Ingestion.Application.Composition;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Composition root for the FaraTaraz host.
///
/// This type has ZERO business authority. It only wires modules together through their
/// module-local <c>Composition</c> extensions. It implements no use case, owns no domain
/// rule, references no provider adapter and no persistence. Business authority remains in
/// the Application layer (see ADR-009 and <c>structure.md</c> §10.3): the host composes
/// modules, never behaviors.
/// </summary>
public static class FaraTarazHost
{
    /// <summary>
    /// Composes the FaraTaraz modules. Each module registers itself through its own
    /// Composition extension; the host only aggregates module Composition and never chooses
    /// or implements business behavior.
    /// </summary>
    public static IServiceCollection ConfigureFaraTaraz(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Modules compose themselves. The host never implements or selects business behavior;
        // provider adapters and persistence are wired by later Infrastructure waves, not here.
        services.AddIngestionApplication();

        return services;
    }
}
